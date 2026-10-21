// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.14 – Danh mục món.</summary>
public class Category : ITenantEntity, ISoftDelete
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
