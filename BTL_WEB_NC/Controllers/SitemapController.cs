// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BTL_WEB_NC.Controllers;

public class SitemapController : Controller
{
    private readonly AppDbContext _db;

    public SitemapController(AppDbContext db)
    {
        _db = db;
    }

    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        // Bỏ qua bộ lọc Query Filter để đọc danh sách chi nhánh của tất cả tenant đang hoạt động (R32, mục 3.2.2c)
        var activeBranches = await (
            from b in _db.Branches.IgnoreQueryFilters()
            join t in _db.Tenants on b.TenantId equals t.Id
            where b.IsActive && t.Status == TenantStatus.Active
            select new { TenantSlug = t.Slug, BranchSlug = b.Slug }
        ).ToListAsync(ct);

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
        sb.AppendLine($"Sitemap: {Request.Scheme}://{Request.Host}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }
}
