// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
// [AI] Claude hỗ trợ – Phụ lục Z.7 (D2: đếm nhân viên theo vai trò, quy tắc chuyển trạng thái, hết hạn/gia hạn gói)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.System.Services;

public class SystemTenantService : ISystemTenantService
{
    /// <summary>Chuyển trạng thái hợp lệ (P01 bước 5, P02 bước 3–4). Trạng thái Từ chối là kết thúc.</summary>
    private static readonly Dictionary<TenantStatus, TenantStatus[]> AllowedTransitions = new()
    {
        [TenantStatus.PendingActivation] = new[] { TenantStatus.Active, TenantStatus.Rejected },
        [TenantStatus.Active] = new[] { TenantStatus.Locked },
        [TenantStatus.Locked] = new[] { TenantStatus.Active },
        [TenantStatus.Rejected] = Array.Empty<TenantStatus>()
    };

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

        // Hết hạn gói (P02 bước 3): lọc trong SQL. PlanExpiresAt < 0h hôm nay ⇔ ngày hết hạn trước hôm nay.
        if (filter.Expired == true)
        {
            var today = VietnamTime.Now.Date;
            query = query.Where(t => t.PlanExpiresAt != null && t.PlanExpiresAt < today);
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

        var owners = await LoadOwnersAsync(tenantIds, ct);

        var branchCounts = await _db.Branches
            .IgnoreQueryFilters()
            .Where(b => tenantIds.Contains(b.TenantId))
            .GroupBy(b => b.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var staffCounts = await ActiveStaff()
            .Where(u => tenantIds.Contains(u.TenantId!.Value))
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var items = tenants
            .Select(t => ToSummary(t, owners.GetValueOrDefault(t.Id),
                branchCounts.GetValueOrDefault(t.Id, 0), staffCounts.GetValueOrDefault(t.Id, 0)))
            .ToList();

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

        var owners = await LoadOwnersAsync(new List<int> { t.Id }, ct);
        var branchCount = await _db.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == t.Id, ct);
        var staffCount = await ActiveStaff().CountAsync(u => u.TenantId == t.Id, ct);

        return ToSummary(t, owners.GetValueOrDefault(t.Id), branchCount, staffCount);
    }

    public async Task UpdateStatusAsync(int id, TenantStatus status, string? lockReason, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy tenant.");

        // Enum nhận từ JSON có thể là số bất kỳ (ví dụ 99) → kiểm tra trước khi xét quy tắc chuyển.
        if (!Enum.IsDefined(status) || !AllowedTransitions[tenant.Status].Contains(status))
        {
            throw new BusinessRuleException("Không thể chuyển quán sang trạng thái này từ trạng thái hiện tại.");
        }

        // P01 bước 5 (từ chối) và P02 bước 3 (khóa) đều phải ghi lý do.
        var needsReason = status is TenantStatus.Locked or TenantStatus.Rejected;
        if (needsReason && string.IsNullOrWhiteSpace(lockReason))
        {
            throw new BusinessRuleException(status == TenantStatus.Locked
                ? "Bắt buộc phải nhập lý do khóa hoạt động của quán."
                : "Bắt buộc phải nhập lý do từ chối quán.");
        }

        tenant.Status = status;
        tenant.LockReason = needsReason ? lockReason!.Trim() : null;
        await _db.SaveChangesAsync(ct);

        // Khi tenant bị khóa hoặc từ chối: đổi SecurityStamp của toàn bộ tài khoản quán để hủy phiên tức thì (P02, R38)
        if (needsReason)
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

        var staffCount = await ActiveStaff().CountAsync(u => u.TenantId == id, ct);
        if (staffCount > newPlan.MaxStaff)
        {
            throw new BusinessRuleException($"Không thể hạ xuống gói \"{newPlan.Name}\" vì quán đang có {staffCount} nhân viên (giới hạn gói mới: {newPlan.MaxStaff}).");
        }

        tenant.ServicePlanId = newPlan.Id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ExtendExpiryAsync(int id, DateTime newExpiry, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy tenant.");

        // Chỉ gia hạn quán đã được duyệt; quán chờ kích hoạt/bị từ chối chưa dùng dịch vụ.
        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Locked))
        {
            throw new BusinessRuleException("Chỉ gia hạn được quán đang hoạt động hoặc đang bị khóa.");
        }

        if (newExpiry.Date <= VietnamTime.Now.Date)
        {
            throw new FieldValidationException("planExpiresAt", "Ngày hết hạn mới phải sau hôm nay.");
        }

        tenant.PlanExpiresAt = newExpiry.Date;
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
        var expired = allTenants.Count(t => IsExpired(t.PlanExpiresAt));

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
        var pendingTenants = allTenants
            .Where(t => t.Status == TenantStatus.PendingActivation)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToList();

        var owners = await LoadOwnersAsync(pendingTenants.Select(p => p.Id).ToList(), ct);
        var recentPending = pendingTenants
            .Select(t => ToSummary(t, owners.GetValueOrDefault(t.Id), 0, 0))
            .ToList();

        return new SystemDashboardViewModel
        {
            TotalTenants = total,
            ActiveTenants = active,
            PendingTenants = pending,
            LockedTenants = locked,
            ExpiredTenants = expired,
            MonthlyRegistrations = monthly,
            PlanDistributions = planDist,
            RecentPendingTenants = recentPending
        };
    }

    /// <summary>
    /// Nhân viên chiếm suất của gói: vai trò Staff và đang làm (CLAUDE.md mục 6, giống PlanUsageService của Module 2).
    /// Chủ quán (TenantAdmin) và nhân viên đã nghỉ không tính. Lọc vai trò bằng subquery nên chỉ một truy vấn SQL.
    /// </summary>
    private IQueryable<AppUser> ActiveStaff()
    {
        var staffRoleId = _db.Roles.Where(r => r.Name == AppRoles.Staff).Select(r => r.Id);
        return _db.Users.Where(u =>
            u.TenantId != null
            && u.IsActive
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && staffRoleId.Contains(ur.RoleId)));
    }

    /// <summary>Chủ quán = tài khoản vai trò TenantAdmin của tenant (không phải tài khoản bất kỳ của quán).</summary>
    private async Task<Dictionary<int, AppUser>> LoadOwnersAsync(List<int> tenantIds, CancellationToken ct)
    {
        if (tenantIds.Count == 0) return new Dictionary<int, AppUser>();

        var adminRoleId = _db.Roles.Where(r => r.Name == AppRoles.TenantAdmin).Select(r => r.Id);
        var owners = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId != null
                && tenantIds.Contains(u.TenantId.Value)
                && _db.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleId.Contains(ur.RoleId)))
            .ToListAsync(ct);

        // Mỗi quán chỉ có một chủ; nếu dữ liệu lỗi có hai thì sắp theo Id để lần nào cũng hiện cùng một người.
        return owners
            .GroupBy(u => u.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.Id, StringComparer.Ordinal).First());
    }

    private static bool IsExpired(DateTime? planExpiresAt) =>
        planExpiresAt.HasValue && planExpiresAt.Value.Date < VietnamTime.Now.Date;

    private static TenantSummaryDto ToSummary(Tenant t, AppUser? owner, int branchCount, int staffCount) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        Phone = t.Phone,
        PlanId = t.ServicePlanId,
        PlanName = t.ServicePlan.Name,
        Status = t.Status,
        PlanExpiresAt = t.PlanExpiresAt,
        IsExpired = IsExpired(t.PlanExpiresAt),
        LockReason = t.LockReason,
        CreatedAt = t.CreatedAt,
        OwnerName = owner?.FullName,
        OwnerEmail = owner?.Email,
        OwnerPhone = owner?.PhoneNumber,
        CurrentBranches = branchCount,
        CurrentStaff = staffCount
    };
}
