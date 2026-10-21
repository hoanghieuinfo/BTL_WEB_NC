// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>Tên claim ghi vào cookie đăng nhập (mục 3.2.2b).</summary>
public static class TenantClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string BranchId = "branch_id";
    public const string FullName = "full_name";
}

/// <summary>Ba vai trò Identity (mục 3.2.4).</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Staff = "Staff";

    public static readonly string[] All = { SuperAdmin, TenantAdmin, Staff };
}
