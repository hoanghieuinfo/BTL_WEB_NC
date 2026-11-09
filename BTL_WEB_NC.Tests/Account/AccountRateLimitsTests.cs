// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using BTL_WEB_NC.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace BTL_WEB_NC.Tests.Account;

/// <summary>Bảng 3.22: giới hạn tần suất theo IP cho A01, A02, đăng nhập; vượt ngưỡng trả 429.</summary>
public class AccountRateLimitsTests
{
    [Fact]
    public void A01_QuaNguongTrongMotPhut_BiTuChoi()
    {
        using var limiter = CreateLimiter(AccountRateLimits.AccountCheckPermitsPerMinute);
        var context = ContextFromIp("10.0.0.1");

        for (var i = 0; i < AccountRateLimits.AccountCheckPermitsPerMinute; i++)
        {
            using var lease = limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }

        using var rejected = limiter.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void IpKhac_CoHanMucRieng()
    {
        using var limiter = CreateLimiter(AccountRateLimits.LoginPermitsPerMinute);
        var first = ContextFromIp("10.0.0.1");
        for (var i = 0; i < AccountRateLimits.LoginPermitsPerMinute; i++)
        {
            limiter.AttemptAcquire(first).Dispose();
        }

        using var lease = limiter.AttemptAcquire(ContextFromIp("10.0.0.2"));

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task TuChoiAjax_Tra429JsonApiResult()
    {
        var context = ContextFromIp("10.0.0.1");
        context.Request.Path = "/api/account/check-email";
        context.Response.Body = new MemoryStream();

        await AccountRateLimits.WriteRejectedAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(AccountRateLimits.RejectedMessage, json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task TuChoiFormDangNhap_Tra429VanBan()
    {
        var context = ContextFromIp("10.0.0.1");
        context.Request.Path = "/dang-nhap";
        context.Response.Body = new MemoryStream();

        await AccountRateLimits.WriteRejectedAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.StartsWith("text/plain", context.Response.ContentType);
    }

    private static PartitionedRateLimiter<HttpContext> CreateLimiter(int permitsPerMinute) =>
        PartitionedRateLimiter.Create<HttpContext, string>(ctx => AccountRateLimits.PerIp(ctx, permitsPerMinute));

    private static DefaultHttpContext ContextFromIp(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
