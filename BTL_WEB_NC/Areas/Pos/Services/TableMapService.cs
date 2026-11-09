// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Pos.Services;

public interface ITableMapService
{
    /// <summary>A10: sơ đồ bàn của một chi nhánh, nhóm theo khu vực. Chỉ gồm bàn đang hoạt động.</summary>
    Task<List<ZoneDto>> GetTablesAsync(int branchId, CancellationToken ct = default);

    /// <summary>Tên quán và chi nhánh cho thanh đầu trang; không tìm thấy thì để chuỗi rỗng.</summary>
    Task<PosHeaderDto> GetHeaderAsync(int tenantId, int branchId, CancellationToken ct = default);
}

/// <summary>
/// MH14/A10 – Sơ đồ bàn (UC14, R22). Mọi truy vấn đi qua DbSet đã có Global Query Filter theo TenantId
/// nên bàn/đơn của quán khác không bao giờ lọt vào kết quả; branchId do controller lấy từ claim, không từ request.
/// </summary>
public class TableMapService : ITableMapService
{
    private readonly AppDbContext _db;

    public TableMapService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ZoneDto>> GetTablesAsync(int branchId, CancellationToken ct = default)
    {
        var zones = await _db.Zones.AsNoTracking()
            .Where(z => z.BranchId == branchId)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Id)
            .Select(z => new
            {
                z.Id,
                z.Name,
                Tables = z.Tables
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name, t.Seats, t.Status })
                    .ToList()
            })
            .ToListAsync(ct);

        // Đơn đang phục vụ của chi nhánh; một bàn có nhiều đơn (dữ liệu lỗi) thì lấy đơn mới nhất cho kết quả ổn định.
        var openOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Serving)
            .OrderByDescending(o => o.Id)
            .Select(o => new { o.Id, o.TableId, o.SubTotal })
            .ToListAsync(ct);
        var byTable = openOrders
            .GroupBy(o => o.TableId)
            .ToDictionary(g => g.Key, g => g.First());

        return zones
            .Select(z => new ZoneDto(z.Id, z.Name, z.Tables.Select(t =>
            {
                byTable.TryGetValue(t.Id, out var order);
                return new TableDto(t.Id, t.Name, t.Seats, t.Status, order?.Id, order?.SubTotal);
            }).ToList()))
            .ToList();
    }

    public async Task<PosHeaderDto> GetHeaderAsync(int tenantId, int branchId, CancellationToken ct = default)
    {
        // Tenants không phải ITenantEntity (không bị Global Query Filter) nên lọc theo Id lấy từ claim.
        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "";
        var branchName = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(ct) ?? "";
        return new PosHeaderDto(tenantName, branchName);
    }
}
