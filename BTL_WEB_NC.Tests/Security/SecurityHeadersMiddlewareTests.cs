// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;

namespace BTL_WEB_NC.Tests.Security;

/// <summary>Header bảo mật (Bảng 3.22): CSP, nosniff, Referrer-Policy gắn vào mọi response.</summary>
public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task GanDuBaHeader()
    {
        var (context, feature) = CreateContext();

        await Run(context, feature, "Production", status: 200);

        var headers = context.Response.Headers;
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public void Csp_KhongChoScriptInline_VaChanNhungTrang()
    {
        var csp = SecurityHeadersMiddleware.ContentSecurityPolicy;

        Assert.Contains("script-src 'self';", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public async Task TrangLoiKhiPhatTrien_KhongGanCsp_NhungVanNosniff()
    {
        var (context, feature) = CreateContext();

        await Run(context, feature, "Development", status: 500);

        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task LoiTrenProduction_VanGanCsp()
    {
        var (context, feature) = CreateContext();

        await Run(context, feature, "Production", status: 500);

        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    private static async Task Run(HttpContext context, CapturingResponseFeature feature, string environment, int status)
    {
        var middleware = new SecurityHeadersMiddleware(ctx => { ctx.Response.StatusCode = status; return Task.CompletedTask; },
            new FakeEnvironment { EnvironmentName = environment });
        await middleware.InvokeAsync(context);
        await feature.FireOnStartingAsync();
    }

    private static (DefaultHttpContext, CapturingResponseFeature) CreateContext()
    {
        var context = new DefaultHttpContext();
        var feature = new CapturingResponseFeature();
        context.Features.Set<IHttpResponseFeature>(feature);
        return (context, feature);
    }

    /// <summary>DefaultHttpContext không tự chạy OnStarting → giữ callback lại và gọi như lúc server bắt đầu gửi response.</summary>
    private sealed class CapturingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = new();

        public override void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

        public async Task FireOnStartingAsync()
        {
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }
        }
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
