// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Areas.Pos.Models;


/// <summary>Một bàn trên sơ đồ (MH14). OrderId/OrderTotal chỉ có khi bàn đang có đơn Đang phục vụ.</summary>
public record TableDto(int Id, string Name, int? Seats, TableStatus Status, long? OrderId, decimal? OrderTotal);

/// <summary>Một khu vực của chi nhánh cùng các bàn đang hoạt động trong khu vực đó.</summary>
public record ZoneDto(int Id, string Name, List<TableDto> Tables);

/// <summary>Tên quán và chi nhánh hiện trên thanh đầu trang bán hàng.</summary>
public record PosHeaderDto(string TenantName, string BranchName);

/// <summary>Body của A12. BranchId không có ở đây: lấy từ claim phía server.</summary>
public class OpenOrderModel
{
    [Required(ErrorMessage = "Vui lòng chọn bàn.")]
    [Range(1, int.MaxValue, ErrorMessage = "Bàn không hợp lệ.")]
    public int? TableId { get; set; }

    [Range(1, 99, ErrorMessage = "Số khách phải từ 1 đến 99.")]
    public int? GuestCount { get; set; }
}

public record OpenedOrderDto(long OrderId, string Code);

/// <summary>Một dòng món trong đơn (BM05). Món đã hủy vẫn được giữ lại (R42) nhưng không tính tiền.</summary>
public record OrderLineDto(long Id, int ProductId, string ProductName, decimal UnitPrice, int Quantity, string? Note, bool IsCancelled, string? CancelReason)
{
    public decimal Amount => IsCancelled ? 0 : UnitPrice * Quantity;
}

/// <summary>
/// Dữ liệu trang MH15. RowVersion là base64 của Orders.RowVersion: trình duyệt gửi lại ở các thao tác ghi
/// (đặc biệt A16) để server phát hiện đơn đã bị thiết bị khác thay đổi → 409.
/// </summary>
public record OrderDetailDto(
    long Id, string Code, OrderStatus Status,
    int TableId, string TableName, TableStatus TableStatus, string ZoneName,
    string BranchName, string StaffName, DateTime CreatedAt, int? GuestCount,
    IReadOnlyList<OrderLineDto> Lines, decimal SubTotal, decimal Discount, decimal Total, string RowVersion)
{
    public bool IsOpen => Status == OrderStatus.Serving;
}

/// <summary>Query của A11. Chỉ có từ khóa và danh mục; chi nhánh/quán lấy từ claim, không nhận từ request.</summary>
public class MenuSearchQuery
{
    [MaxLength(100, ErrorMessage = "Từ khóa tối đa 100 ký tự.")]
    public string? Q { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Danh mục không hợp lệ.")]
    public int? CategoryId { get; set; }
}

public record PosCategoryDto(int Id, string Name);

public record PosProductDto(int Id, string Name, decimal Price, string? ImagePath, int CategoryId);

/// <summary>Kết quả A11: danh mục còn món đang bán (để vẽ thanh lọc) và các món khớp bộ lọc.</summary>
public record MenuSearchResult(IReadOnlyList<PosCategoryDto> Categories, IReadOnlyList<PosProductDto> Products);

/// <summary>
/// Body của A13. Hai chế độ: thêm một dòng mới (có ProductId) hoặc sửa một dòng đang có (có ItemId) – đúng một trong hai.
/// Số lượng 1–99; muốn bỏ món thì dùng hủy món (A14, bắt buộc lý do) chứ không giảm về 0. Ghi chú tối đa 100 ký tự (BM05).
/// </summary>
public class SaveOrderItemModel : IValidatableObject
{
    public long? ItemId { get; set; }

    public int? ProductId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
    [Range(1, 99, ErrorMessage = "Số lượng phải từ 1 đến 99.")]
    public int? Quantity { get; set; }

    [MaxLength(100, ErrorMessage = "Ghi chú tối đa 100 ký tự.")]
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ItemId.HasValue == ProductId.HasValue)
        {
            yield return new ValidationResult("Vui lòng chọn món.", new[] { nameof(ProductId) });
        }
        else if (ItemId <= 0 || ProductId <= 0)
        {
            yield return new ValidationResult("Món không hợp lệ.", new[] { ItemId.HasValue ? nameof(ItemId) : nameof(ProductId) });
        }
    }
}

/// <summary>
/// Body của A14 (hủy món). Lý do bắt buộc (R25) và tối đa 255 ký tự (khớp cột OrderItems.CancelReason).
/// Dòng món nằm trên URL, không nhận từ body.
/// </summary>
public class CancelOrderItemModel
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy món.")]
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? Reason { get; set; }
}

/// <summary>
/// Body của A15 (chuyển bàn và gộp bàn). Đơn nằm trên URL, chi nhánh lấy từ claim; chỉ nhận bàn đích.
/// </summary>
public class MoveOrderModel
{
    [Required(ErrorMessage = "Vui lòng chọn bàn.")]
    [Range(1, int.MaxValue, ErrorMessage = "Bàn không hợp lệ.")]
    public int? TargetTableId { get; set; }
}

/// <summary>Kết quả gộp bàn: đơn đích (đơn còn lại) để trình duyệt chuyển sang xem.</summary>
public record MergedOrderDto(long TargetOrderId, string TargetCode, string TargetTableName);

/// <summary>
/// Body của A30 (hủy đơn). Lý do tối đa 255 ký tự (khớp cột Orders.CancelReason). Có bắt buộc hay không phụ thuộc đơn đã có món chưa
/// nên do service quyết định (đơn chưa có món chưa hủy thì không cần lý do); đơn nằm trên URL, chi nhánh lấy từ claim.
/// </summary>
public class CancelOrderModel
{
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? Reason { get; set; }
}

/// <summary>
/// Body của A16 (thanh toán). Đơn nằm trên URL, chi nhánh và thu ngân lấy từ claim. Tổng tiền KHÔNG nhận từ trình duyệt:
/// server tự tính lại từ giá đã lưu trong chi tiết đơn. RowVersion là giá trị gốc (base64) trang MH15 đang giữ, để phát hiện đơn đã
/// bị máy khác sửa hoặc thanh toán (409). Các ràng buộc phụ thuộc tổng tiền (giảm giá ≤ tổng, tiền khách đưa ≥ phải trả) do service kiểm tra.
/// </summary>
public class PayOrderModel : IValidatableObject
{
    [Required(ErrorMessage = "Thiếu phiên bản đơn. Hãy tải lại trang.")]
    [MaxLength(64, ErrorMessage = "Phiên bản đơn không hợp lệ.")]
    public string? RowVersion { get; set; }

    /// <summary>Giảm giá (đồng, số nguyên). Bỏ trống = 0.</summary>
    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Giảm giá không được âm.")]
    public decimal? Discount { get; set; }

    [MaxLength(255, ErrorMessage = "Lý do giảm giá tối đa 255 ký tự.")]
    public string? DiscountReason { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn hình thức thanh toán.")]
    public PaymentMethod? Method { get; set; }

    /// <summary>Tiền khách đưa (đồng, số nguyên). Chỉ dùng khi trả tiền mặt.</summary>
    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Tiền khách đưa không hợp lệ.")]
    public decimal? CashReceived { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method.HasValue && !Enum.IsDefined(Method.Value))
        {
            yield return new ValidationResult("Hình thức thanh toán không hợp lệ.", new[] { nameof(Method) });
        }
        // Cột tiền là decimal(18,0): không nhận phần lẻ.
        if (Discount.HasValue && Discount.Value != decimal.Truncate(Discount.Value))
        {
            yield return new ValidationResult("Giảm giá phải là số tiền nguyên (đồng).", new[] { nameof(Discount) });
        }
        if (CashReceived.HasValue && CashReceived.Value != decimal.Truncate(CashReceived.Value))
        {
            yield return new ValidationResult("Tiền khách đưa phải là số tiền nguyên (đồng).", new[] { nameof(CashReceived) });
        }
    }
}

/// <summary>Kết quả A16: đủ để trình duyệt báo tiền thừa và mở trang in hóa đơn (MH16).</summary>
public record PaymentResultDto(
    long OrderId, string Code, decimal SubTotal, decimal Discount, decimal Total,
    PaymentMethod Method, decimal? CashReceived, decimal Change, DateTime PaidAt);

/// <summary>Một dòng món trên phiếu in (MH16). Chỉ có món chưa hủy: món đã hủy không tính tiền nên không in cho khách.</summary>
public record PrintLineDto(string Name, string? Note, int Quantity, decimal UnitPrice, decimal Amount);

/// <summary>
/// Dữ liệu trang in MH16. Đơn Đang phục vụ in PHIẾU TẠM TÍNH (chưa phải hóa đơn); đơn Đã thanh toán in HÓA ĐƠN theo BM06.
/// Time là giờ thanh toán (hóa đơn) hoặc giờ in (phiếu tạm tính). Giờ đều theo giờ Việt Nam.
/// </summary>
public record PrintOrderDto(
    long Id, string Code, OrderStatus Status,
    string TenantName, string BranchName, string BranchAddress, string? BranchPhone,
    string TableName, string ZoneName, string StaffName, DateTime Time, int? GuestCount,
    IReadOnlyList<PrintLineDto> Lines, decimal SubTotal, decimal Discount, string? DiscountReason, decimal Total,
    PaymentMethod? PaymentMethod, decimal? CashReceived)
{
    public bool IsInvoice => Status == OrderStatus.Paid;

    /// <summary>Tiền thừa trả khách (BM06): chỉ có khi trả tiền mặt.</summary>
    public decimal? Change => PaymentMethod == BTL_WEB_NC.Models.Entities.PaymentMethod.Cash && CashReceived.HasValue ? CashReceived.Value - Total : null;
}

/// <summary>Một hóa đơn trong danh sách MH17. PaidAt là giờ Việt Nam; thu ngân lấy từ CashierId (không có thì để trống).</summary>
public record InvoiceRowDto(
    long Id, string Code, string TableName, string ZoneName, DateTime PaidAt,
    PaymentMethod? Method, string? CashierName, decimal Discount, decimal Total);

/// <summary>Dữ liệu trang MH17: hóa đơn Đã thanh toán của chi nhánh trong một ngày (giờ Việt Nam), mới nhất trước.</summary>
public record InvoiceListDto(DateOnly Day, IReadOnlyList<InvoiceRowDto> Items, decimal TotalRevenue)
{
    public int Count => Items.Count;
}
