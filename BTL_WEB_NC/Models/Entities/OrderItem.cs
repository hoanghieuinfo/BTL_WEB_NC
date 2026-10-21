// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.17 – Chi tiết đơn. Lưu tên và giá lúc bán để hóa đơn cũ không đổi khi sửa món.</summary>
public class OrderItem : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public long OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
