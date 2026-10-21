// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.15 – Nguyên liệu.</summary>
public class Ingredient : ITenantEntity, ISoftDelete
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty; // đơn vị cơ sở
    public decimal MinStock { get; set; }
    public bool IsDeleted { get; set; }
}
