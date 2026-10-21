// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BTL_WEB_NC.Infrastructure.Ajax;

/// <summary>
/// Request AJAX chưa đăng nhập/không đủ quyền nhận 401/403 kèm JSON thay vì 302 về trang đăng nhập,
/// để ajax.js xử lý được (mục 3.2.6). Request thường vẫn chuyển hướng như mặc định.
/// </summary>
public static class AjaxCookieEvents
{
    public static Task OnRedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        Respond(context, StatusCodes.Status401Unauthorized, "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.");

    public static Task OnRedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        Respond(context, StatusCodes.Status403Forbidden, "Bạn không có quyền thực hiện thao tác này.");

    private static Task Respond(RedirectContext<CookieAuthenticationOptions> context, int statusCode, string message)
    {
        if (context.Request.IsAjaxRequest())
        {
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsJsonAsync(ApiResult.Fail(message));
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
