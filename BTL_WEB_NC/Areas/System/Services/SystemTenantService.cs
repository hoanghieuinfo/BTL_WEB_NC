// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.System.Services;

public class SystemTenantService : ISystemTenantService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public SystemTenantService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<TenantPagedResult> GetPagedTenantsAsync(TenantFilterModel filter, CancellationToken ct = default)
    {
        // Area System: bỏ qua bộ lọc Global Query Filter để đọc mọi tenant (mục 3.2.2c)
        var query = _db.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.ServicePlan)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(kw) || t.Slug.ToLower().Contains(kw) || (t.Phone != null && t.Phone.Contains(kw)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.Status == filter.Status.Value);
        }

        if (filter.PlanId.HasValue)
        {
            query = query.Where(t => t.ServicePlanId == filter.PlanId.Value);
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var size = Math.Max(1, filter.PageSize);

        var tenants = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        // Đọc thông tin chủ quán và số chi nhánh/nhân viên
        var owners = await _userManager.Users
            .Where(u => u.TenantId.HasValue && tenantIds.Contains(u.TenantId.Value))
            .ToListAsync(ct);

        var branchCounts = await _db.Branches
            .IgnoreQueryFilters()
            .Where(b => tenantIds.Contains(b.TenantId))
            .GroupBy(b => b.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var staffCounts = await _userManager.Users
            .Where(u => u.TenantId.HasValue && tenantIds.Contains(u.TenantId.Value) && u.IsActive)
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var items = tenants.Select(t =>
        {
            var owner = owners.FirstOrDefault(u => u.TenantId == t.Id);
            return new TenantSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Phone = t.Phone,
                PlanId = t.ServicePlanId,
                PlanName = t.ServicePlan.Name,
                Status = t.Status,
                PlanExpiresAt = t.PlanExpiresAt,
                LockReason = t.LockReason,
                CreatedAt = t.CreatedAt,
                OwnerName = owner?.FullName,
                OwnerEmail = owner?.Email,
                OwnerPhone = owner?.PhoneNumber,
                CurrentBranches = branchCounts.GetValueOrDefault(t.Id, 0),
                CurrentStaff = staffCounts.GetValueOrDefault(t.Id, 0)
            };
        }).ToList();

        return new TenantPagedResult
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size
        };
    }

    public async Task<TenantSummaryDto?> GetTenantDetailAsync(int id, CancellationToken ct = default)
    {
        var t = await _db.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.ServicePlan)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (t == null) return null;

        var owner = await _userManager.Users.FirstOrDefaultAsync(u => u.TenantId == t.Id, ct);
        var branchCount = await _db.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == t.Id, ct);
        var staffCount = await _userManager.Users.CountAsync(u => u.TenantId == t.Id && u.IsActive, ct);

        return new TenantSummaryDto
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Phone = t.Phone,
            PlanId = t.ServicePlanId,
            PlanName = t.ServicePlan.Name,
            Status = t.Status,
            PlanExpiresAt = t.PlanExpiresAt,
            LockReason = t.LockReason,
            CreatedAt = t.CreatedAt,
            OwnerName = owner?.FullName,
            OwnerEmail = owner?.Email,
            OwnerPhone = owner?.PhoneNumber,
            CurrentBranches = branchCount,
            CurrentStaff = staffCount
        };
    }

    public async Task UpdateStatusAsync(int id, TenantStatus status, string? lockReason, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy tenant.");

        if (status == TenantStatus.Locked && string.IsNullOrWhiteSpace(lockReason))
        {
            throw new BusinessRuleException("Bắt buộc phải nhập lý do khóa hoạt động của quán.");
        }

        tenant.Status = status;
        tenant.LockReason = status == TenantStatus.Locked ? lockReason?.Trim() : null;
        await _db.SaveChangesAsync(ct);

        // Khi tenant bị khóa hoặc từ chối: đổi SecurityStamp của toàn bộ tài khoản quán để hủy phiên tức thì (P02, R38)
        if (status == TenantStatus.Locked || status == TenantStatus.Rejected)
        {
            var users = await _userManager.Users.Where(u => u.TenantId == id).ToListAsync(ct);
            foreach (var u in users)
            {
                await _userManager.UpdateSecurityStampAsync(u);
            }
        }
    }

    public async Task ChangePlanAsync(int id, int newPlanId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy tenant.");

        var newPlan = await _db.ServicePlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == newPlanId, ct)
            ?? throw new NotFoundException("Gói dịch vụ không tồn tại.");

        // Không cho phép chuyển xuống gói có giới hạn thấp hơn số chi nhánh/nhân viên hiện có (P02 bước 2, R08)
        var branchCount = await _db.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == id, ct);
        if (branchCount > newPlan.MaxBranches)
        {
            throw new BusinessRuleException($"Không thể hạ xuống gói \"{newPlan.Name}\" vì quán đang có {branchCount} chi nhánh (giới hạn gói mới: {newPlan.MaxBranches}).");
        }

        var staffCount = await _userManager.Users.CountAsync(u => u.TenantId == id && u.IsActive, ct);
        if (staffCount > newPlan.MaxStaff)
        {
            throw new BusinessRuleException($"Không thể hạ xuống gói \"{newPlan.Name}\" vì quán đang có {staffCount} nhân viên (giới hạn gói mới: {newPlan.MaxStaff}).");
        }

        tenant.ServicePlanId = newPlan.Id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SystemDashboardViewModel> GetDashboardOverviewAsync(CancellationToken ct = default)
    {
        var allTenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.ServicePlan)
            .AsNoTracking()
            .ToListAsync(ct);

        var total = allTenants.Count;
        var active = allTenants.Count(t => t.Status == TenantStatus.Active);
        var pending = allTenants.Count(t => t.Status == TenantStatus.PendingActivation);
        var locked = allTenants.Count(t => t.Status == TenantStatus.Locked || t.Status == TenantStatus.Rejected);

        var currentYear = VietnamTime.Now.Year;

        // Thống kê đăng ký mới 12 tháng trong năm hiện tại
        var monthly = Enumerable.Range(1, 12).Select(m =>
        {
            var count = allTenants.Count(t => t.CreatedAt.Year == currentYear && t.CreatedAt.Month == m);
            return new MonthlyRegistrationDto(m, count);
        }).ToList();

        // Thống kê tỷ lệ sử dụng gói
        var planDist = allTenants
            .GroupBy(t => t.ServicePlan.Name)
            .Select(g => new PlanDistributionDto(
                g.Key,
                g.Count(),
                total == 0 ? 0 : Math.Round((double)g.Count() / total * 100, 1)))
            .ToList();

        // 5 quán mới đăng ký cần duyệt nhanh
        var pendingTenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.ServicePlan)
            .Where(t => t.Status == TenantStatus.PendingActivation)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var pendingIds = pendingTenants.Select(p => p.Id).ToList();
        var owners = await _userManager.Users
            .Where(u => u.TenantId.HasValue && pendingIds.Contains(u.TenantId.Value))
            .ToListAsync(ct);

        var recentPending = pendingTenants.Select(t =>
        {
            var owner = owners.FirstOrDefault(u => u.TenantId == t.Id);
            return new TenantSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Phone = t.Phone,
                PlanId = t.ServicePlanId,
                PlanName = t.ServicePlan.Name,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                OwnerName = owner?.FullName,
                OwnerEmail = owner?.Email,
                OwnerPhone = owner?.PhoneNumber
            };
        }).ToList();

        return new SystemDashboardViewModel
        {
            TotalTenants = total,
            ActiveTenants = active,
            PendingTenants = pending,
            LockedTenants = locked,
            MonthlyRegistrations = monthly,
            PlanDistributions = planDist,
            RecentPendingTenants = recentPending
        };
    }
}
