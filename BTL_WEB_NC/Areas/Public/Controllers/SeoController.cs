// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Text;
using System.Xml.Linq;
using BTL_WEB_NC.Areas.Public.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Public.Controllers;

/// <summary>R32: robots.txt và sitemap.xml. Không tạo file tĩnh wwwroot/robots.txt (sẽ che action này).</summary>
public class SeoController : PublicControllerBase
{
    private readonly IPublicMenuService _menu;

    public SeoController(IPublicMenuService menu)
    {
        _menu = menu;
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Disallow: /system")
            .AppendLine("Disallow: /manage")
            .AppendLine("Disallow: /pos")
            .AppendLine("Disallow: /api")
            .AppendLine()
            .AppendLine($"Sitemap: {BaseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var entries = await _menu.ListSitemapEntriesAsync(ct);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        // TODO khi Đạt xong MH01: thêm URL trang chủ "{BaseUrl}/" (R32 yêu cầu landing page cũng được lập chỉ mục).
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                entries.Select(e => new XElement(ns + "url",
                    new XElement(ns + "loc", $"{BaseUrl}/{e.TenantSlug}/thuc-don/{e.BranchSlug}")))));
        return Content(doc.Declaration + Environment.NewLine + doc.ToString(), "application/xml; charset=utf-8");
    }
}
