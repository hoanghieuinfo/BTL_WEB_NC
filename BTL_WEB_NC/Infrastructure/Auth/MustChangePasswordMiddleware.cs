// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Infrastructure.Auth;

/// <summary>
/// R03: tài khoản còn mật khẩu khởi tạo chỉ được vào trang đổi mật khẩu, đăng xuất và trang báo lỗi quyền.
/// Chạy sau UseAuthentication (cần User) và trước UseAuthorization. File tĩnh đã được UseStaticFiles trả trước đó.
/// </summary>
public class MustChangePasswordMiddleware
{
    public const string ChangePasswordPath = "/tai-khoan/doi-mat-khau";

    private static readonly PathString[] AllowedPaths =
    {
        ChangePasswordPath, "/dang-xuat", "/khong-co-quyen"
    };

    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(c => c.Type == TenantClaimTypes.MustChangePassword)
            && !AllowedPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            if (context.Request.IsAjaxRequest())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiResult.Fail("Bạn cần đổi mật khẩu trước khi tiếp tục."));
            }
            else
            {
                context.Response.Redirect(ChangePasswordPath);
            }
            return;
        }

        await _next(context);
    }
}
