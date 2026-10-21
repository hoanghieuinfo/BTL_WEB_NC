// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>
/// Tenant của request hiện tại (Scoped). Middleware gán từ claim tenant_id, branch_id phía server;
/// không bao giờ lấy từ dữ liệu request gửi lên. TenantId null = chưa đăng nhập hoặc Super Admin.
/// </summary>
public class TenantContext
{
    public int? TenantId { get; set; }
    public int? BranchId { get; set; }
    public string? Role { get; set; }
}
