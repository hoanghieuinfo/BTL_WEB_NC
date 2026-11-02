// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Public.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Public.Services;

public interface IPublicMenuService
{
    Task<PublicMenuResult> GetAsync(string tenantSlug, string branchSlug, CancellationToken ct = default);
    Task<IReadOnlyList<SitemapEntry>> ListSitemapEntriesAsync(CancellationToken ct = default);
}

/// <summary>
/// MH04 / P09 / R29 – thực đơn công khai. Món chỉ gắn với quán (Product không có BranchId), nên mọi chi nhánh
/// của quán hiện cùng danh sách món Đang bán; chi nhánh cung cấp tên, địa chỉ, SĐT, giờ mở cửa.
/// </summary>
public class PublicMenuService : IPublicMenuService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public PublicMenuService(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PublicMenuResult> GetAsync(string tenantSlug, string branchSlug, CancellationToken ct = default)
    {
        // Tenants không phải ITenantEntity nên không bị Global Query Filter.
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Slug == tenantSlug)
            .Select(t => new { t.Id, t.Name, t.Status })
            .FirstOrDefaultAsync(ct);

        // Chưa kích hoạt / bị từ chối: coi như không tồn tại.
        if (tenant is null || tenant.Status is TenantStatus.PendingActivation or TenantStatus.Rejected)
        {
            return new(PublicMenuStatus.NotFound);
        }

        // Tenant bị khóa → thông báo tạm ngưng.
        if (tenant.Status == TenantStatus.Locked)
        {
            return new(PublicMenuStatus.Suspended, TenantName: tenant.Name);
        }

        // Khách ẩn danh có TenantId null → bộ lọc trả rỗng. Gán tenant theo slug cho riêng request này.
        // CHỈ ĐỌC trong service này; không SaveChanges.
        _tenant.TenantId = tenant.Id;

        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Slug == branchSlug && b.IsActive)
            .Select(b => new { b.Name, b.Slug, b.Address, b.Phone, b.OpenTime, b.CloseTime })
            .FirstOrDefaultAsync(ct);
        if (branch is null)
        {
            return new(PublicMenuStatus.NotFound);
        }

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsAvailable)
            .Select(p => new { p.CategoryId, p.Name, p.Price, p.ImagePath, p.Description })
            .ToListAsync(ct);

        var byCategory = products.ToLookup(p => p.CategoryId);
        var groups = categories
            .Select(c => new PublicMenuCategory(
                c.Name,
                byCategory[c.Id]
                    .OrderBy(p => p.Name, StringComparer.CurrentCulture)
                    .Select(p => new PublicMenuProduct(p.Name, p.Price, p.ImagePath, p.Description))
                    .ToList()))
            .Where(g => g.Products.Count > 0)
            .ToList();

        return new(PublicMenuStatus.Found, new PublicMenuPage(
            tenant.Name, tenantSlug, branch.Name, branch.Slug, branch.Address, branch.Phone,
            branch.OpenTime, branch.CloseTime, groups));
    }

    public async Task<IReadOnlyList<SitemapEntry>> ListSitemapEntriesAsync(CancellationToken ct = default)
    {
        // SPEC 3.2.2c cho phép bỏ bộ lọc tenant khi sinh sitemap.xml. Branch không có xóa mềm.
        var rows = await _db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.IsActive)
            .Join(_db.Tenants.Where(t => t.Status == TenantStatus.Active),
                  b => b.TenantId, t => t.Id,
                  (b, t) => new { TenantSlug = t.Slug, BranchSlug = b.Slug })
            .ToListAsync(ct);

        return rows.OrderBy(r => r.TenantSlug).ThenBy(r => r.BranchSlug)
                   .Select(r => new SitemapEntry(r.TenantSlug, r.BranchSlug))
                   .ToList();
    }
}
