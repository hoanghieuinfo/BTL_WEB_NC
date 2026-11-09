// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Threading.RateLimiting;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.RateLimiting;

namespace BTL_WEB_NC.Infrastructure.Auth;

/// <summary>
/// Bảng 3.22 – "Dò email, slug qua API công khai": Rate Limiting theo IP cho A01, A02 và trang đăng nhập; vượt ngưỡng trả 429.
/// Đếm theo IP vì người dò chưa đăng nhập, không có tài khoản để đếm; khóa theo tài khoản (5 lần sai, R36) không chặn được
/// việc thử nhiều email khác nhau. Lưu ý: nếu chạy sau reverse proxy phải bật ForwardedHeaders, nếu không mọi request
/// mang IP của proxy và dùng chung một hạn mức.
/// </summary>
public static class AccountRateLimits
{
    public const string AccountCheckPolicy = "account-check";
    public const string LoginPolicy = "login";

    public const int AccountCheckPermitsPerMinute = 20;
    public const int LoginPermitsPerMinute = 10;

    public const string RejectedMessage = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.";

    public static IServiceCollection AddAccountRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AccountCheckPolicy, context => PerIp(context, AccountCheckPermitsPerMinute));
            options.AddPolicy(LoginPolicy, context => PerIp(context, LoginPermitsPerMinute));
            options.OnRejected = (context, ct) => new ValueTask(WriteRejectedAsync(context.HttpContext, ct));
        });

    /// <summary>Cửa sổ cố định 1 phút, mỗi IP một bộ đếm riêng.</summary>
    public static RateLimitPartition<string> PerIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    /// <summary>429: AJAX nhận JSON ApiResult (ajax.js hiện thông báo); form đăng nhập nhận một dòng chữ.</summary>
    public static async Task WriteRejectedAsync(HttpContext context, CancellationToken ct)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Request.IsAjaxRequest())
        {
            await context.Response.WriteAsJsonAsync(ApiResult.Fail(RejectedMessage), ct);
        }
        else
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(RejectedMessage, ct);
        }
    }
}
