// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.12 – Chi nhánh.</summary>
public class Branch : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
}
