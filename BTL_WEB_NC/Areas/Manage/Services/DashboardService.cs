// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IDashboardService
{
    Task<DashboardModel> GetAsync(int? branchId, CancellationToken ct = default);
}

/// <summary>
/// MH08 – Tổng quan: số liệu hôm nay so với hôm qua (R20), nguyên liệu âm kho/dưới mức (R19), gói dịch vụ và mức dùng (R11, R17).
/// Dùng lại ReportService (cùng quy tắc "đơn Đã thanh toán, theo PaidAt") và PlanUsageService.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly IReportService _reports;
    private readonly IPlanUsageService _usage;
    private readonly IInventoryService _inventory;

    public DashboardService(AppDbContext db, IReportService reports, IPlanUsageService usage, IInventoryService inventory)
    {
        _db = db;
        _reports = reports;
        _usage = usage;
        _inventory = inventory;
    }

    public async Task<DashboardModel> GetAsync(int? branchId, CancellationToken ct = default)
    {
        var branches = await _inventory.BranchesAsync(ct);
        // Chi nhánh lạ (của quán khác hoặc không tồn tại) trên URL → coi như "Tất cả chi nhánh", không lộ gì thêm.
        if (branchId.HasValue && branches.All(b => b.Id != branchId.Value))
        {
            branchId = null;
        }

        var today = VietnamTime.Today;
        var todaySummary = await _reports.SummaryAsync(today, today, branchId, ct);
        var yesterdaySummary = await _reports.SummaryAsync(today.AddDays(-1), today.AddDays(-1), branchId, ct);

        var lowStock = await LowStockAsync(branchId, ct);
        return new DashboardModel(today, branches, branchId, todaySummary, yesterdaySummary,
            lowStock.Take(DashboardModel.LowStockLimit).ToList(), lowStock.Count, lowStock.Count(r => r.Quantity < 0),
            await _usage.GetAsync(ct));
    }

    /// <summary>
    /// Dòng tồn có Quantity &lt; MinStock (Bảng 3.15: cảnh báo tính khi hiển thị). Đi qua BranchStocks và Ingredients
    /// đã lọc tenant (nguyên liệu xóa mềm tự bị loại). Âm kho lên trước, rồi thiếu nhiều (theo tỷ lệ) lên trước.
    /// </summary>
    private async Task<List<LowStockRow>> LowStockAsync(int? branchId, CancellationToken ct)
    {
        var stocks = _db.BranchStocks.AsNoTracking();
        if (branchId.HasValue)
        {
            stocks = stocks.Where(s => s.BranchId == branchId.Value);
        }

        var rows = await (
            from s in stocks
            join i in _db.Ingredients on s.IngredientId equals i.Id
            join b in _db.Branches on s.BranchId equals b.Id
            where s.Quantity < i.MinStock
            select new LowStockRow(i.Name, b.Name, i.Unit, s.Quantity, i.MinStock))
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Status)
            .ThenBy(r => r.MinStock == 0 ? 0 : r.Quantity / r.MinStock)
            .ThenBy(r => r.IngredientName, StringComparer.CurrentCulture)
            .ToList();
    }
}
