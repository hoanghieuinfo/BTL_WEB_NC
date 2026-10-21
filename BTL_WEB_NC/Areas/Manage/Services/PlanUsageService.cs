// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public record PlanUsage(
    string TenantName, string PlanName, DateTime? PlanExpiresAt,
    int BranchCount, int MaxBranches, int StaffCount, int MaxStaff);

public interface IPlanUsageService
{
    /// <summary>Thông tin quán, gói và mức dùng chi nhánh/nhân viên của tenant hiện tại (R11, R17).</summary>
    Task<PlanUsage> GetAsync(CancellationToken ct = default);
}

public class PlanUsageService : IPlanUsageService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public PlanUsageService(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PlanUsage> GetAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Không xác định được tenant của request.");

        var info = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Name, PlanName = t.ServicePlan.Name, t.PlanExpiresAt, t.ServicePlan.MaxBranches, t.ServicePlan.MaxStaff })
            .SingleAsync(ct);

        // Branches có Global Query Filter → chỉ đếm chi nhánh của tenant này (kể cả đang tạm ngưng).
        var branchCount = await _db.Branches.CountAsync(ct);

        // AppUser không có bộ lọc tự động → lọc TenantId tường minh. Chỉ đếm vai trò Staff đang làm
        // (IsActive); nhân viên đã nghỉ không chiếm suất của gói (CLAUDE.md mục 6).
        var staffRoleId = _db.Roles.Where(r => r.Name == AppRoles.Staff).Select(r => r.Id);
        var staffCount = await _db.Users.CountAsync(u =>
            u.TenantId == tenantId
            && u.IsActive
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && staffRoleId.Contains(ur.RoleId)), ct);

        return new PlanUsage(info.Name, info.PlanName, info.PlanExpiresAt,
            branchCount, info.MaxBranches, staffCount, info.MaxStaff);
    }
}
