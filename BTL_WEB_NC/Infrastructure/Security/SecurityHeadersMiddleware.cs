// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Infrastructure.Security;

/// <summary>
/// Header bảo mật cho mọi response (Bảng 3.22 – XSS, clickjacking). CSP chỉ cho script/style từ chính site
/// (cùng Google Fonts), nên script chèn vào trang qua lỗi XSS cũng không chạy được.
/// Điều kiện: view không có script/style/onclick inline (đã dọn ở Bước 7 cho Area Manage và D3 cho Area System).
/// </summary>
public class SecurityHeadersMiddleware
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        // blob: cho ảnh xem trước khi chọn file ở MH10 (URL.createObjectURL); data: cho ảnh nhúng nhỏ.
        "img-src 'self' data: blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'";

    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext context)
    {
        // OnStarting: gắn header ngay trước khi gửi, kể cả response do middleware phía sau tạo (file tĩnh, 404, 429...).
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Trang lỗi chi tiết khi phát triển (DeveloperExceptionPage) dùng script/style inline → không gắn CSP để còn đọc được lỗi.
            if (!(_isDevelopment && context.Response.StatusCode >= 500))
            {
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
            }
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
