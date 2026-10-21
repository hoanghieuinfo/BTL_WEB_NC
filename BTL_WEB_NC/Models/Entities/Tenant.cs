// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.10 – Quán/chuỗi (bảng gốc, không có TenantId).</summary>
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int ServicePlanId { get; set; }
    public TenantStatus Status { get; set; }
    public DateTime? PlanExpiresAt { get; set; }
    public string? LockReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public ServicePlan ServicePlan { get; set; } = null!;
}
