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

        // 2. Trang Đăng ký
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{baseUrl}/dang-ky</loc>");
        sb.AppendLine("    <changefreq>monthly</changefreq>");
        sb.AppendLine("    <priority>0.8</priority>");
        sb.AppendLine("  </url>");

        // 3. Thực đơn công khai từng quán/chi nhánh
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
}
