// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.16 – Chi tiết phiếu nhập. Tồn được cộng Quantity × ConversionRate.</summary>
public class StockReceiptItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int StockReceiptId { get; set; }
    public int IngredientId { get; set; }
    public string PurchaseUnit { get; set; } = string.Empty;
    public decimal ConversionRate { get; set; }
    public decimal Quantity { get; set; } // theo đơn vị nhập
    public decimal UnitPrice { get; set; }

    public StockReceipt StockReceipt { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
