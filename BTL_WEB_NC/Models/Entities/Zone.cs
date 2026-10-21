// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.13 – Khu vực trong chi nhánh (tên bảng Zones, tránh nhầm với Area của MVC).</summary>
public class Zone : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Branch Branch { get; set; } = null!;
    public ICollection<DiningTable> Tables { get; set; } = new List<DiningTable>();
}
