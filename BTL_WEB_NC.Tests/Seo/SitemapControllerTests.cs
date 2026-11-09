// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Reflection;
using BTL_WEB_NC.Areas.Public.Models;
using BTL_WEB_NC.Areas.Public.Services;
using BTL_WEB_NC.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;

namespace BTL_WEB_NC.Tests.Seo;

/// <summary>
/// R32, R33 – robots.txt và sitemap.xml. Lỗi đã gặp khi gộp Module 3: hai controller cùng khai báo hai đường dẫn này,
/// action có [HttpGet] được chọn nên bản kia bị che mà không báo lỗi (sitemap thiếu trang chủ, robots thiếu /dang-nhap).
/// </summary>
public class SitemapControllerTests
{
    [Theory]
    [InlineData("robots.txt")]
    [InlineData("sitemap.xml")]
    public void ChiMotActionKhaiBaoDuongDan(string path)
    {
        var actions = AllRouteTemplates().Where(r => r.Template == path).Select(r => r.Action).ToList();

        var only = Assert.Single(actions);
        Assert.StartsWith(nameof(SitemapController) + ".", only);
    }

    [Fact]
    public void KhongCoFileTinhRobotsTxt()
    {
        // File tĩnh wwwroot/robots.txt được UseStaticFiles trả trước khi tới routing → che action Robots.
        var root = FindRepoRoot();
        Assert.False(File.Exists(Path.Combine(root, "BTL_WEB_NC", "wwwroot", "robots.txt")));
        Assert.False(File.Exists(Path.Combine(root, "BTL_WEB_NC", "wwwroot", "sitemap.xml")));
    }

    [Fact]
    public async Task Sitemap_CoTrangChuVaThucDonChiNhanh_ChiMotLanMoiUrl()
    {
        var controller = Create(entries: new[] { new SitemapEntry("ca-phe-may", "cau-giay"), new SitemapEntry("tra-gio", "thanh-xuan") });

        var xml = Content(await controller.Sitemap(CancellationToken.None));

        Assert.Contains("<loc>https://cafesaas.test/</loc>", xml);
        Assert.Contains("<loc>https://cafesaas.test/ca-phe-may/thuc-don/cau-giay</loc>", xml);
        Assert.Contains("<loc>https://cafesaas.test/tra-gio/thuc-don/thanh-xuan</loc>", xml);
        Assert.Equal(3, CountOf(xml, "<loc>"));
        Assert.DoesNotContain("/dang-ky", xml);
    }

    [Fact]
    public async Task Sitemap_UuTienPublicSiteBaseUrl_GiongMaQr()
    {
        var controller = Create(new[] { new SitemapEntry("ca-phe-may", "cau-giay") }, baseUrl: "https://menu.cafesaas.vn/");

        var xml = Content(await controller.Sitemap(CancellationToken.None));

        Assert.Contains("<loc>https://menu.cafesaas.vn/</loc>", xml);
        Assert.Contains("<loc>https://menu.cafesaas.vn/ca-phe-may/thuc-don/cau-giay</loc>", xml);
    }

    [Fact]
    public void Robots_ChanKhuQuanTriVaTaiKhoan_TroToiSitemap()
    {
        var lines = Content(Create(Array.Empty<SitemapEntry>()).Robots()).Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        foreach (var path in new[] { "/system/", "/manage/", "/pos/", "/api/", "/tai-khoan/", "/dang-nhap" })
        {
            Assert.Contains($"Disallow: {path}", lines);
        }
        Assert.DoesNotContain("Disallow: /dang-ky", lines);   // dùng meta noindex thay vì chặn
        Assert.Contains("Sitemap: https://cafesaas.test/sitemap.xml", lines);
    }

    // ---------------------------------------------------------------- Hỗ trợ

    /// <summary>Mọi route khai báo bằng attribute trên action (gộp với [Route] của controller nếu có), dạng không có "/" đầu.</summary>
    private static IEnumerable<(string Action, string Template)> AllRouteTemplates()
    {
        var controllers = typeof(SitemapController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var prefix = controller.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault()?.Template?.Trim('/');
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var provider in method.GetCustomAttributes().OfType<IRouteTemplateProvider>())
                {
                    if (provider.Template is null) continue;
                    var template = provider.Template.StartsWith('/') || string.IsNullOrEmpty(prefix)
                        ? provider.Template.Trim('/')
                        : $"{prefix}/{provider.Template.Trim('/')}";
                    yield return ($"{controller.Name}.{method.Name}", template.ToLowerInvariant());
                }
            }
        }
    }

    private static SitemapController Create(IReadOnlyList<SitemapEntry> entries, string? baseUrl = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(baseUrl is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["PublicSite:BaseUrl"] = baseUrl })
            .Build();
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("cafesaas.test");
        return new SitemapController(new FakePublicMenu(entries), config)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static string Content(IActionResult result) => Assert.IsType<ContentResult>(result).Content!;

    private static int CountOf(string text, string value) => (text.Length - text.Replace(value, "").Length) / value.Length;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BTL_WNC.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc chứa BTL_WNC.sln.");
    }

    private sealed class FakePublicMenu : IPublicMenuService
    {
        private readonly IReadOnlyList<SitemapEntry> _entries;

        public FakePublicMenu(IReadOnlyList<SitemapEntry> entries) => _entries = entries;

        public Task<PublicMenuResult> GetAsync(string tenantSlug, string branchSlug, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SitemapEntry>> ListSitemapEntriesAsync(CancellationToken ct = default) => Task.FromResult(_entries);
    }
}
