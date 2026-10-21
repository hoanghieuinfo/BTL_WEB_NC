// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.14 – Món.</summary>
public class Product : ITenantEntity, ISoftDelete
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImagePath { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
