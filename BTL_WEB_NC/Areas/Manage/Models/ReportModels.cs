// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Models.Entities;

namespace BTL_WEB_NC.Areas.Manage.Models;

// MH13 – Báo cáo doanh thu (P08, R20, BM09) và MH08 – Tổng quan. Mọi số liệu chỉ từ hóa đơn Đã thanh toán, lọc theo PaidAt.

/// <summary>Tham số A09 và danh sách hóa đơn (query string). Thiếu ngày → 14 ngày gần nhất.</summary>
public class ReportQuery
{
    public const int MaxDays = 366;
    public const int DefaultDays = 14;
    public const int PageSize = 10;

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? BranchId { get; set; }
    public int Page { get; set; } = 1;
}

/// <summary>Khoảng ngày đã kiểm tra: kỳ này và kỳ trước cùng độ dài, liền trước.</summary>
public record ReportPeriod(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;
    public DateOnly PreviousFrom => From.AddDays(-Days);
    public DateOnly PreviousTo => From.AddDays(-1);
}

public record RevenueSummary(decimal Revenue, int Orders, decimal Discount, int DiscountedOrders)
{
    public static readonly RevenueSummary Empty = new(0, 0, 0, 0);

    /// <summary>Trung bình mỗi hóa đơn, làm tròn về đồng.</summary>
    public decimal Average => Orders == 0 ? 0 : Math.Round(Revenue / Orders, 0, MidpointRounding.AwayFromZero);
}

/// <summary>% thay đổi so với kỳ trước, 1 chữ số thập phân; null khi kỳ trước bằng 0 (hiện "Mới phát sinh").</summary>
public record Comparison(decimal? Revenue, decimal? Orders, decimal? Average)
{
    public static decimal? Percent(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100 / previous, 1, MidpointRounding.AwayFromZero);
}

public record DayRevenue(DateOnly Date, decimal Revenue, decimal PreviousRevenue, int Orders);

public record BranchRevenue(int BranchId, string Name, decimal Revenue, int Orders, decimal Share);

public record TopProduct(int Rank, string Name, int Quantity, decimal Revenue);

/// <summary>Kết quả A09 (JSON cho Chart.js).</summary>
public record RevenueReport(
    DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, int? BranchId,
    RevenueSummary Summary, RevenueSummary Previous, Comparison Change,
    IReadOnlyList<DayRevenue> Days, IReadOnlyList<BranchRevenue> Branches, IReadOnlyList<TopProduct> TopProducts);

// ------------------------------------------------------------------ Hóa đơn trong kỳ

public record ReportOrderRow(
    long Id, string Code, DateTime PaidAt, string BranchName, string TableName, string? CashierName, decimal Discount, decimal Total);

public record ReportOrderList(IReadOnlyList<ReportOrderRow> Items, int Page, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)ReportQuery.PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * ReportQuery.PageSize + 1;
    public int To => Math.Min(Page * ReportQuery.PageSize, TotalCount);
}

public record OrderDetailLine(string Name, int Quantity, decimal UnitPrice, string? Note, bool IsCancelled, string? CancelReason)
{
    public decimal Amount => IsCancelled ? 0 : UnitPrice * Quantity;
}

/// <summary>Hóa đơn theo BM06.</summary>
public record OrderDetail(
    long Id, string Code, OrderStatus Status, string TenantName, string BranchName, string BranchAddress, string? BranchPhone,
    DateTime CreatedAt, DateTime? PaidAt, string TableName, string ZoneName, string? CashierName,
    IReadOnlyList<OrderDetailLine> Lines, decimal SubTotal, decimal Discount, string? DiscountReason, decimal Total,
    PaymentMethod? PaymentMethod, decimal? CashReceived)
{
    public decimal? ChangeDue => PaymentMethod == BTL_WEB_NC.Models.Entities.PaymentMethod.Cash && CashReceived.HasValue ? CashReceived - Total : null;
}

public record ReportPageModel(IReadOnlyList<InventoryBranchOption> Branches, DateOnly Today);

// ------------------------------------------------------------------ MH08 – Tổng quan

public record LowStockRow(string IngredientName, string BranchName, string Unit, decimal Quantity, decimal MinStock)
{
    public StockStatus Status => Quantity < 0 ? StockStatus.Negative : StockStatus.Low;
}

public record DashboardModel(
    DateOnly Today, IReadOnlyList<InventoryBranchOption> Branches, int? BranchId,
    RevenueSummary TodaySummary, RevenueSummary YesterdaySummary,
    IReadOnlyList<LowStockRow> LowStock, int LowStockTotal, int NegativeCount, PlanUsage Usage)
{
    public const int LowStockLimit = 8;

    public int? PlanDaysLeft => Usage.PlanExpiresAt is { } expires ? DateOnly.FromDateTime(expires).DayNumber - Today.DayNumber : null;
}
