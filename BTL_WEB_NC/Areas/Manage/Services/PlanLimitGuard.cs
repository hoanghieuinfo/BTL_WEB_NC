// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

/// <summary>
/// Chống vượt giới hạn gói (R11, R17) khi hai request đồng thời cùng qua bước đếm.
/// Dùng chung cho thêm chi nhánh, thêm nhân viên, mở khóa nhân viên. Cách gọi:
/// <code>
/// await using var tx = await _db.Database.BeginTransactionAsync(ct);
/// await _limits.LockTenantAsync(ct);   // request thứ hai của cùng quán chờ ở đây tới khi tx thứ nhất kết thúc
/// ... đếm suất (PlanUsageService), thêm/cập nhật ...
/// await tx.CommitAsync(ct);
/// </code>
/// </summary>
public interface IPlanLimitGuard
{
    /// <summary>Khóa dòng Tenants của quán hiện tại tới khi transaction đang mở commit/rollback. Phải gọi trong transaction.</summary>
    Task LockTenantAsync(CancellationToken ct = default);
}

public class PlanLimitGuard : IPlanLimitGuard
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public PlanLimitGuard(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task LockTenantAsync(CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("LockTenantAsync phải được gọi bên trong transaction.");
        }
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Không xác định được tenant.");

        // SQLite (test) không có UPDLOCK; SQLite vốn khóa cả CSDL khi ghi nên không cần.
        if (!_db.Database.IsSqlServer())
        {
            return;
        }

        // UPDLOCK: chỉ một transaction giữ khóa cập nhật trên dòng này; HOLDLOCK: giữ tới hết transaction.
        // Chỉ khóa MỘT dòng của quán hiện tại → quán khác không bị ảnh hưởng. Tham số hóa, không nối chuỗi SQL (R37).
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
    }
}
