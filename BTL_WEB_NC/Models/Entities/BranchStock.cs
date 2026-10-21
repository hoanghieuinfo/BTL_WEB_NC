// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.15 – Tồn kho theo chi nhánh. PK (BranchId, IngredientId); Quantity có thể âm.</summary>
public class BranchStock : ITenantEntity
{
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public int TenantId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!; // chống ghi đè tồn kho khi trừ đồng thời

    public Branch Branch { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
