// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IReportService
{
    /// <summary>Kiểm tra khoảng ngày (from ≤ to, tối đa 366 ngày); thiếu thì lấy 14 ngày gần nhất.</summary>
    ReportPeriod ResolvePeriod(ReportQuery query);

    Task<RevenueReport> RevenueAsync(ReportQuery query, CancellationToken ct = default);
    Task<ReportOrderList> OrdersAsync(ReportQuery query, CancellationToken ct = default);
    Task<OrderDetail> OrderDetailAsync(long id, CancellationToken ct = default);
    Task<RevenueSummary> SummaryAsync(DateOnly from, DateOnly to, int? branchId, CancellationToken ct = default);
}

/// <summary>
/// Báo cáo doanh thu (P08, R20, BM09). Đọc qua _db.Orders đã lọc tenant nên không lẫn số liệu quán khác (R38);
/// chỉ tính đơn Đã thanh toán, theo PaidAt (giờ Việt Nam – CLAUDE.md mục 9).
/// Tổng hợp nặng (cộng tiền theo ngày × chi nhánh, theo món) làm trên SQL; điền ngày trống, tính %, xếp hạng trong bộ nhớ
/// vì kết quả sau GROUP BY chỉ còn vài trăm dòng.
/// </summary>
public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public ReportPeriod ResolvePeriod(ReportQuery query)
    {
        var today = VietnamTime.Today;
        var to = query.To ?? (query.From.HasValue ? query.From.Value.AddDays(ReportQuery.DefaultDays - 1) : today);
        var from = query.From ?? to.AddDays(-(ReportQuery.DefaultDays - 1));
        if (from > to)
        {
            throw new FieldValidationException("to", "Đến ngày phải bằng hoặc sau Từ ngày.");
        }
        if (to.DayNumber - from.DayNumber + 1 > ReportQuery.MaxDays)
        {
            throw new FieldValidationException("from", $"Khoảng thời gian tối đa {ReportQuery.MaxDays} ngày.");
        }
        return new ReportPeriod(from, to);
    }

    // ------------------------------------------------------------------ A09

    public async Task<RevenueReport> RevenueAsync(ReportQuery query, CancellationToken ct = default)
    {
        var period = ResolvePeriod(query);
        await EnsureBranchAsync(query.BranchId, ct);

        // Một truy vấn cho cả hai kỳ: GROUP BY (ngày, chi nhánh).
        var groups = await PaidOrders(period.PreviousFrom, period.To, query.BranchId)
            .GroupBy(o => new { Day = o.PaidAt!.Value.Date, o.BranchId })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.BranchId,
                Revenue = g.Sum(o => o.Total),
                Orders = g.Count(),
                Discount = g.Sum(o => o.Discount),
                Discounted = g.Count(o => o.Discount > 0)
            })
            .ToListAsync(ct);

        var current = groups.Where(g => DateOnly.FromDateTime(g.Day) >= period.From).ToList();
        var previous = groups.Where(g => DateOnly.FromDateTime(g.Day) < period.From).ToList();

        static RevenueSummary Sum<T>(IEnumerable<T> rows, Func<T, decimal> revenue, Func<T, int> orders,
            Func<T, decimal> discount, Func<T, int> discounted)
        {
            var list = rows.ToList();
            return new RevenueSummary(list.Sum(revenue), list.Sum(orders), list.Sum(discount), list.Sum(discounted));
        }

        var summary = Sum(current, g => g.Revenue, g => g.Orders, g => g.Discount, g => g.Discounted);
        var prevSummary = Sum(previous, g => g.Revenue, g => g.Orders, g => g.Discount, g => g.Discounted);
        var change = new Comparison(
            Comparison.Percent(summary.Revenue, prevSummary.Revenue),
            Comparison.Percent(summary.Orders, prevSummary.Orders),
            Comparison.Percent(summary.Average, prevSummary.Average));

        // Đủ mọi ngày của kỳ, ngày không bán = 0; ngày thứ i của kỳ trước đặt cạnh ngày thứ i của kỳ này.
        var byDay = current.GroupBy(g => DateOnly.FromDateTime(g.Day)).ToDictionary(g => g.Key, g => (Revenue: g.Sum(x => x.Revenue), Orders: g.Sum(x => x.Orders)));
        var prevByDay = previous.GroupBy(g => DateOnly.FromDateTime(g.Day)).ToDictionary(g => g.Key, g => g.Sum(x => x.Revenue));
        var days = Enumerable.Range(0, period.Days)
            .Select(i =>
            {
                var day = period.From.AddDays(i);
                var today = byDay.GetValueOrDefault(day);
                return new DayRevenue(day, today.Revenue, prevByDay.GetValueOrDefault(period.PreviousFrom.AddDays(i)), today.Orders);
            })
            .ToList();

        var branchNames = await _db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var branches = current
            .GroupBy(g => g.BranchId)
            .Select(g => new { BranchId = g.Key, Revenue = g.Sum(x => x.Revenue), Orders = g.Sum(x => x.Orders) })
            .OrderByDescending(b => b.Revenue)
            .Select(b => new BranchRevenue(b.BranchId, branchNames.GetValueOrDefault(b.BranchId, "Chi nhánh"), b.Revenue, b.Orders,
                summary.Revenue == 0 ? 0 : Math.Round(b.Revenue * 100 / summary.Revenue, 1, MidpointRounding.AwayFromZero)))
            .ToList();

        // Top 10 món theo tên lúc bán (giữ đúng tên trên hóa đơn cũ dù món đã đổi tên/xóa), bỏ món hủy.
        // Doanh thu món = đơn giá × số lượng, chưa trừ giảm giá theo hóa đơn.
        var orderIds = PaidOrders(period.From, period.To, query.BranchId).Select(o => o.Id);
        var products = await _db.OrderItems.AsNoTracking()
            .Where(i => !i.IsCancelled && orderIds.Contains(i.OrderId))
            .GroupBy(i => i.ProductName)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
            .ToListAsync(ct);
        var top = products
            .OrderByDescending(p => p.Revenue).ThenByDescending(p => p.Quantity).ThenBy(p => p.Name)
            .Take(10)
            .Select((p, i) => new TopProduct(i + 1, p.Name, p.Quantity, p.Revenue))
            .ToList();

        return new RevenueReport(period.From, period.To, period.PreviousFrom, period.PreviousTo, query.BranchId,
            summary, prevSummary, change, days, branches, top);
    }

    public async Task<RevenueSummary> SummaryAsync(DateOnly from, DateOnly to, int? branchId, CancellationToken ct = default)
    {
        var rows = await PaidOrders(from, to, branchId)
            .Select(o => new { o.Total, o.Discount })
            .ToListAsync(ct);
        return new RevenueSummary(rows.Sum(r => r.Total), rows.Count, rows.Sum(r => r.Discount), rows.Count(r => r.Discount > 0));
    }

    // ------------------------------------------------------------------ Hóa đơn trong kỳ

    public async Task<ReportOrderList> OrdersAsync(ReportQuery query, CancellationToken ct = default)
    {
        var period = ResolvePeriod(query);
        await EnsureBranchAsync(query.BranchId, ct);

        var orders = PaidOrders(period.From, period.To, query.BranchId);
        var total = await orders.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)ReportQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);

        var items = await orders
            .OrderByDescending(o => o.PaidAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * ReportQuery.PageSize)
            .Take(ReportQuery.PageSize)
            .Select(o => new ReportOrderRow(o.Id, o.Code, o.PaidAt!.Value, o.Branch.Name, o.Table.Name,
                o.Cashier != null ? o.Cashier.FullName : null, o.Discount, o.Total))
            .ToListAsync(ct);

        return new ReportOrderList(items, page, total);
    }

    /// <summary>Chi tiết hóa đơn (BM06). Đơn của quán khác không qua được bộ lọc → 404.</summary>
    public async Task<OrderDetail> OrderDetailAsync(long id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id, o.Code, o.Status, BranchName = o.Branch.Name, o.Branch.Address, o.Branch.Phone, o.TenantId,
                o.CreatedAt, o.PaidAt, TableName = o.Table.Name, ZoneName = o.Table.Zone.Name,
                CashierName = o.Cashier != null ? o.Cashier.FullName : null,
                o.SubTotal, o.Discount, o.DiscountReason, o.Total, o.PaymentMethod, o.CashReceived
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy hóa đơn.");

        var tenantName = await _db.Tenants.AsNoTracking().Where(t => t.Id == order.TenantId).Select(t => t.Name).FirstAsync(ct);
        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == id)
            .OrderBy(i => i.Id)
            .Select(i => new OrderDetailLine(i.ProductName, i.Quantity, i.UnitPrice, i.Note, i.IsCancelled, i.CancelReason))
            .ToListAsync(ct);

        return new OrderDetail(order.Id, order.Code, order.Status, tenantName, order.BranchName, order.Address, order.Phone,
            order.CreatedAt, order.PaidAt, order.TableName, order.ZoneName, order.CashierName, lines,
            order.SubTotal, order.Discount, order.DiscountReason, order.Total, order.PaymentMethod, order.CashReceived);
    }

    // ------------------------------------------------------------------ Hàm phụ

    /// <summary>Đơn Đã thanh toán có from ≤ PaidAt &lt; to + 1 ngày (giờ VN), tùy chọn một chi nhánh.</summary>
    private IQueryable<Order> PaidOrders(DateOnly from, DateOnly to, int? branchId)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.PaidAt >= start && o.PaidAt < end);
        return branchId.HasValue ? orders.Where(o => o.BranchId == branchId.Value) : orders;
    }

    /// <summary>branchId trong query phải thuộc quán hiện tại; của quán khác → 404 (không âm thầm trả số 0).</summary>
    private async Task EnsureBranchAsync(int? branchId, CancellationToken ct)
    {
        if (branchId.HasValue && !await _db.Branches.AnyAsync(b => b.Id == branchId.Value, ct))
        {
            throw new NotFoundException("Không tìm thấy chi nhánh.");
        }
    }
}
