// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
// [AI] Claude hỗ trợ – Phụ lục Z.7 (T1: nơi duy nhất phục vụ robots.txt, sitemap.xml; danh sách chi nhánh lấy từ PublicMenuService)
using BTL_WEB_NC.Areas.Public.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace BTL_WEB_NC.Controllers;

/// <summary>
/// R32, R33: robots.txt và sitemap.xml. Chỉ controller này khai báo hai đường dẫn đó (trước đây Area Public có SeoController
/// trùng route; action có [HttpGet] được ưu tiên nên bản này bị che mà không báo lỗi).
/// </summary>
public class SitemapController : Controller
{
    private readonly IPublicMenuService _publicMenu;
    private readonly IConfiguration _config;

    public SitemapController(IPublicMenuService publicMenu, IConfiguration config)
    {
        _publicMenu = publicMenu;
        _config = config;
    }

    /// <summary>
    /// PublicSite:BaseUrl nếu có cấu hình, không thì scheme://host của request – cùng quy tắc với mã QR (BranchQrService),
    /// để link trong sitemap trùng với link in trên QR đặt tại bàn.
    /// </summary>
    private string BaseUrl =>
        (_config["PublicSite:BaseUrl"] is { Length: > 0 } configured ? configured : $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = BaseUrl;

        // Chi nhánh đang mở của tenant đang hoạt động. Truy vấn bỏ bộ lọc tenant (mục 3.2.2c) nằm trong PublicMenuService.
        var activeBranches = await _publicMenu.ListSitemapEntriesAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // 1. Landing Page
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{baseUrl}/</loc>");
        sb.AppendLine("    <changefreq>weekly</changefreq>");
        sb.AppendLine("    <priority>1.0</priority>");
        sb.AppendLine("  </url>");

        // 2. Thực đơn công khai từng quán/chi nhánh. Chỉ hai loại trang này được lập chỉ mục (SPEC mục 3.2.5),
        // nên không đưa /dang-ky, /dang-nhap vào sitemap.
        foreach (var b in activeBranches)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}/{b.TenantSlug}/thuc-don/{b.BranchSlug}</loc>");
            sb.AppendLine("    <changefreq>daily</changefreq>");
            sb.AppendLine("    <priority>0.9</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    // [AI] Claude hỗ trợ – Phụ lục Z.7
    /// <summary>
    /// robots.txt sinh từ action (thay file tĩnh) để dòng Sitemap theo đúng tên miền đang chạy, không ghi cứng (R32, R33).
    /// Chặn khu quản trị, bán hàng, API và các trang tài khoản.
    /// </summary>
    [Route("robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        // /dang-ky không chặn ở đây mà dùng meta noindex: chặn thì máy tìm kiếm không đọc được thẻ noindex.
        foreach (var path in new[] { "/system/", "/manage/", "/pos/", "/api/", "/tai-khoan/", "/dang-nhap" })
        {
            sb.AppendLine($"Disallow: {path}");
        }
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {BaseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }
}
