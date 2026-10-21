// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BTL_WEB_NC.Areas.Manage.Models;

// MH12 – Kho (P06, R18, R19, BM07). Không model nào có TenantId: AppDbContext tự gán khi thêm mới.

// ------------------------------------------------------------------ Nguyên liệu (R18)

public class IngredientFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên nguyên liệu.")]
    [StringLength(100, ErrorMessage = "Tên nguyên liệu tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đơn vị cơ sở.")]
    [StringLength(20, ErrorMessage = "Đơn vị tối đa 20 ký tự.")]
    public string Unit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mức tồn tối thiểu.")]
    [Range(0, 999_999_999, ErrorMessage = "Mức tồn tối thiểu phải từ 0 trở lên.")]
    [ThreeDecimals]
    public decimal? MinStock { get; set; }
}

public class IngredientListQuery
{
    public const int PageSize = 10;

    public string? Q { get; set; }
    public string? Unit { get; set; }
    public int Page { get; set; } = 1;

    public bool HasFilter => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Unit);
}

/// <param name="UnitLocked">Đã có định lượng, tồn khác 0 hoặc phiếu nhập → không đổi được đơn vị cơ sở.</param>
public record IngredientRow(int Id, string Name, string Unit, decimal MinStock, int ProductCount, bool UnitLocked);

public record IngredientListResult(IReadOnlyList<IngredientRow> Items, int Page, int TotalCount, IngredientListQuery Query)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)IngredientListQuery.PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * IngredientListQuery.PageSize + 1;
    public int To => Math.Min(Page * IngredientListQuery.PageSize, TotalCount);
}

// ------------------------------------------------------------------ Tồn kho theo chi nhánh (R19)

/// <summary>Tình trạng tính khi hiển thị (Bảng 3.15), không lưu CSDL.</summary>
public enum StockStatus
{
    Negative = 0,   // Âm kho
    Low = 1,        // Dưới mức
    Enough = 2      // Đủ
}

public class StockListQuery
{
    public const int PageSize = 10;

    public int? BranchId { get; set; }
    public string? Q { get; set; }

    /// <summary>Chỉ xem nguyên liệu cần nhập (Âm kho + Dưới mức).</summary>
    public bool NeedOnly { get; set; }

    public int Page { get; set; } = 1;
}

/// <param name="UpdatedAt">null = chi nhánh chưa có dòng tồn của nguyên liệu này ("Chưa nhập lần nào").</param>
public record StockRow(int IngredientId, string Name, string Unit, decimal Quantity, decimal MinStock, DateTime? UpdatedAt)
{
    public StockStatus Status => Quantity < 0 ? StockStatus.Negative : Quantity < MinStock ? StockStatus.Low : StockStatus.Enough;
}

public record StockSummary(int Total, int Negative, int Low)
{
    public int Enough => Total - Negative - Low;
    public int NeedRestock => Negative + Low;
}

public record StockListResult(
    BranchOption Branch, IReadOnlyList<StockRow> Items, int Page, int TotalCount, StockSummary Summary, StockListQuery Query)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)StockListQuery.PageSize));
}

public record StockUsage(string ProductName, decimal Quantity);

public record StockHistoryLine(int ReceiptId, string Code, DateOnly ReceivedDate, string PurchaseUnit, decimal Quantity, decimal ConversionRate)
{
    public decimal BaseQuantity => Quantity * ConversionRate;
}

public record StockDetail(BranchOption Branch, StockRow Stock, IReadOnlyList<StockUsage> UsedIn, IReadOnlyList<StockHistoryLine> History);

// ------------------------------------------------------------------ Phiếu nhập kho (BM07)

public class ReceiptFormModel
{
    public const int MaxLines = 50;

    [Required(ErrorMessage = "Vui lòng chọn chi nhánh nhập.")]
    public int? BranchId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày nhập.")]
    public DateOnly? ReceivedDate { get; set; }

    [StringLength(100, ErrorMessage = "Tên nhà cung cấp tối đa 100 ký tự.")]
    public string? Supplier { get; set; }

    public List<ReceiptLineModel> Lines { get; set; } = new();
}

public class ReceiptLineModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên liệu.")]
    public int IngredientId { get; set; }

    [Required(ErrorMessage = "Nhập đơn vị.")]
    [StringLength(20, ErrorMessage = "Tối đa 20 ký tự.")]
    public string PurchaseUnit { get; set; } = string.Empty;

    [Range(0.001, 1_000_000, ErrorMessage = "Phải lớn hơn 0.")]
    [ThreeDecimals]
    public decimal ConversionRate { get; set; }

    [Range(0.001, 1_000_000, ErrorMessage = "Phải lớn hơn 0.")]
    [ThreeDecimals]
    public decimal Quantity { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Đơn giá từ 0 đến 1.000.000.000 đ.")]
    [WholeNumber(ErrorMessage = "Đơn giá là số nguyên.")]
    public decimal UnitPrice { get; set; }
}

public class ReceiptListQuery
{
    public const int PageSize = 10;

    public int? BranchId { get; set; }

    /// <summary>Tháng theo ngày nhập, dạng yyyy-MM; rỗng = mọi thời gian.</summary>
    public string? Month { get; set; }

    public string? Q { get; set; }
    public int Page { get; set; } = 1;

    public bool HasFilter => BranchId.HasValue || !string.IsNullOrWhiteSpace(Q);

    public DateOnly? MonthStart =>
        DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

public record ReceiptRow(int Id, string Code, DateOnly ReceivedDate, string BranchName, string? Supplier, string CreatedBy, decimal TotalAmount);

public record ReceiptListResult(IReadOnlyList<ReceiptRow> Items, int Page, int TotalCount, decimal PeriodTotal, ReceiptListQuery Query)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)ReceiptListQuery.PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * ReceiptListQuery.PageSize + 1;
    public int To => Math.Min(Page * ReceiptListQuery.PageSize, TotalCount);
}

public record ReceiptDetailLine(string IngredientName, string BaseUnit, string PurchaseUnit, decimal ConversionRate, decimal Quantity, decimal UnitPrice)
{
    public decimal Amount => InventoryMath.LineAmount(Quantity, UnitPrice);
    public decimal BaseQuantity => Quantity * ConversionRate;
}

public record ReceiptDetail(
    int Id, string Code, DateOnly ReceivedDate, string BranchName, string? Supplier, string CreatedBy, decimal TotalAmount,
    IReadOnlyList<ReceiptDetailLine> Lines);

public record ReceiptCreatedResult(int Id, string Code);

// ------------------------------------------------------------------ Dữ liệu trang

public record InventoryBranchOption(int Id, string Name, bool IsActive);

public record InventoryHeaderModel(string ActiveTab, int IngredientCount);

public record StockPageModel(InventoryHeaderModel Header, IReadOnlyList<InventoryBranchOption> Branches);

public record IngredientPageModel(InventoryHeaderModel Header, IReadOnlyList<string> Units);

public record ReceiptPageModel(InventoryHeaderModel Header, IReadOnlyList<InventoryBranchOption> Branches, string CurrentMonth);

public record NewReceiptPageModel(
    IReadOnlyList<InventoryBranchOption> Branches, IReadOnlyList<IngredientOption> Ingredients,
    int? BranchId, int? IngredientId, string NextCode, DateOnly Today);

/// <summary>Dãy nút trang dùng chung cho ba danh sách của MH12.</summary>
public record PagerModel(int Page, int TotalPages, string Label);

// ------------------------------------------------------------------ Phép tính và định dạng dùng chung

public static class InventoryMath
{
    /// <summary>Thành tiền một dòng: làm tròn về đồng (tiền là decimal(18,0)). JS dùng đúng công thức này.</summary>
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);
}

public static class InventoryText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Qty(decimal value) => value.ToString("#,##0.###", Vi);

    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " đ";

    /// <summary>"16:12 hôm nay", "08:30 hôm qua" hoặc "dd/MM/yyyy" (giờ Việt Nam).</summary>
    public static string Updated(DateTime? value, DateOnly today)
    {
        if (value is null) return "Chưa nhập lần nào";
        var day = DateOnly.FromDateTime(value.Value);
        if (day == today) return value.Value.ToString("HH:mm") + " hôm nay";
        if (day == today.AddDays(-1)) return value.Value.ToString("HH:mm") + " hôm qua";
        return value.Value.ToString("dd/MM/yyyy");
    }
}

/// <summary>Số lượng/định lượng lưu decimal(18,3) → tối đa 3 chữ số thập phân.</summary>
public sealed class ThreeDecimalsAttribute : ValidationAttribute
{
    public ThreeDecimalsAttribute() : base("Tối đa 3 chữ số thập phân.")
    {
    }

    public override bool IsValid(object? value) => value is not decimal d || d == Math.Round(d, 3);
}

/// <summary>Tiền là decimal(18,0) → số nguyên.</summary>
public sealed class WholeNumberAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not decimal d || d == decimal.Truncate(d);
}
