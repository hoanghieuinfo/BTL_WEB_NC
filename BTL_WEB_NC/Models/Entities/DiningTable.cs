// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.13 – Bàn.</summary>
public class DiningTable : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Seats { get; set; }
    public TableStatus Status { get; set; }
    public bool IsActive { get; set; } = true;

    public Zone Zone { get; set; } = null!;
}
