// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.16 – Phiếu nhập kho.</summary>
public class StockReceipt : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    public Branch Branch { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
    public ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
}
