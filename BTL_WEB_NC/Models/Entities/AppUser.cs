// [AI] Claude hỗ trợ – Phụ lục Z.7
using Microsoft.AspNetCore.Identity;

namespace BTL_WEB_NC.Models.Entities;

/// <summary>
/// Bảng 3.11 – Tài khoản. Có TenantId nhưng KHÔNG cài ITenantEntity: lúc đăng nhập chưa có tenant
/// nên không thể áp bộ lọc tự động; service quản lý nhân viên tự lọc theo TenantId của chủ quán.
/// </summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? TenantId { get; set; }   // null với Super Admin
    public int? BranchId { get; set; }   // bắt buộc (ở service) với vai trò Staff
    public DateOnly? StartDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
}
