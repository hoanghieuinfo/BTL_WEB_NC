// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.17 – Đơn hàng / hóa đơn.</summary>
public class Order : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public int TableId { get; set; }
    public string Code { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public int? GuestCount { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? DiscountReason { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? CashReceived { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? CashierId { get; set; }
    public string? CancelReason { get; set; }
    public byte[] RowVersion { get; set; } = null!; // chống thanh toán trùng

    public Branch Branch { get; set; } = null!;
    public DiningTable Table { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
    public AppUser? Cashier { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
