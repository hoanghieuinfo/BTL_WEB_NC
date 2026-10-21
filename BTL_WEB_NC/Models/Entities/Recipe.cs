// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.15 – Định lượng cho một phần món. PK (ProductId, IngredientId).</summary>
public class Recipe : ITenantEntity
{
    public int ProductId { get; set; }
    public int IngredientId { get; set; }
    public int TenantId { get; set; }
    public decimal Quantity { get; set; } // theo đơn vị cơ sở của nguyên liệu

    public Product Product { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
