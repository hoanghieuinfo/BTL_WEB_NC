// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Infrastructure.Auth;

/// <summary>Chọn trang đích sau khi đăng nhập hoặc đổi mật khẩu (R02, R03). Tách thành hàm thuần để test được.</summary>
public static class PostLoginRedirect
{
    /// <param name="mustChangePassword">Tài khoản còn mật khẩu khởi tạo.</param>
    /// <param name="localReturnUrl">returnUrl đã được kiểm tra là URL nội bộ (Url.IsLocalUrl); null nếu không có hoặc không hợp lệ.</param>
    /// <param name="roles">Vai trò của tài khoản.</param>
    public static string Resolve(bool mustChangePassword, string? localReturnUrl, IEnumerable<string> roles)
    {
        // Xét trước returnUrl: nếu không, link /dang-nhap?returnUrl=/manage cho phép bỏ qua bước đổi mật khẩu.
        if (mustChangePassword) return MustChangePasswordMiddleware.ChangePasswordPath;

        if (!string.IsNullOrEmpty(localReturnUrl)) return localReturnUrl;

        var roleSet = roles.ToHashSet();
        if (roleSet.Contains(AppRoles.SuperAdmin)) return "/system";
        if (roleSet.Contains(AppRoles.TenantAdmin)) return "/manage";
        if (roleSet.Contains(AppRoles.Staff)) return "/pos";
        return "/";
    }
}
