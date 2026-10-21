// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Models.Entities;

/// <summary>Bảng 3.9 – Gói dịch vụ (không thuộc tenant).</summary>
public class ServicePlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxBranches { get; set; }
    public int MaxStaff { get; set; }
    public decimal PricePerMonth { get; set; }
    public int? TrialDays { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}
