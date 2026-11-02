// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Public.Models;
using BTL_WEB_NC.Areas.Public.Services;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.PublicMenu;

/// <summary>
/// MH04 – thực đơn công khai. Quán A: chi nhánh Cầu Giấy (cau-giay, đang hoạt động) và Đống Đa (dong-da, tạm ngưng);
/// danh mục Cà phê (Cà phê sữa đá, Bạc xỉu, "Cà phê cũ" ĐÃ XÓA), Trà (Trà đào cam sả), Tráng miệng (Bánh flan NGỪNG BÁN).
/// Quán B: chi nhánh Thanh Xuân (thanh-xuan), danh mục Sinh tố (Sinh tố bơ).
/// </summary>
public class PublicMenuServiceTests
{
    private static void Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        db.Branches.AddRange(
            NewBranch(a, "Cầu Giấy", "cau-giay", true),
            NewBranch(a, "Đống Đa", "dong-da", false),
            NewBranch(b, "Thanh Xuân", "thanh-xuan", true));

        var caPhe = new Category { TenantId = a, Name = "Cà phê", SortOrder = 1 };
        var tra = new Category { TenantId = a, Name = "Trà", SortOrder = 2 };
        var trangMieng = new Category { TenantId = a, Name = "Tráng miệng", SortOrder = 3 };
        var sinhTo = new Category { TenantId = b, Name = "Sinh tố", SortOrder = 1 };
        db.Categories.AddRange(caPhe, tra, trangMieng, sinhTo);
        db.Products.AddRange(
            new Product { TenantId = a, Category = caPhe, Name = "Cà phê sữa đá", Price = 29000 },
            new Product { TenantId = a, Category = caPhe, Name = "Bạc xỉu", Price = 32000 },
            new Product { TenantId = a, Category = caPhe, Name = "Cà phê cũ", Price = 20000, IsDeleted = true },
            new Product { TenantId = a, Category = tra, Name = "Trà đào cam sả", Price = 45000 },
            new Product { TenantId = a, Category = trangMieng, Name = "Bánh flan", Price = 25000, IsAvailable = false },
            new Product { TenantId = b, Category = sinhTo, Name = "Sinh tố bơ", Price = 49000 });
        db.SaveChanges();
    }

    private static Branch NewBranch(int tenantId, string name, string slug, bool active) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Code = "CN", Address = "1 Phố Test",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = active
    };

    private static void SetStatus(TestDb testDb, int tenantId, TenantStatus status)
    {
        using var db = testDb.CreateContext(tenantId: null);
        db.Tenants.Single(t => t.Id == tenantId).Status = status;
        db.SaveChanges();
    }

    /// <summary>Khách ẩn danh: TenantContext trống, dùng chung giữa DbContext và service như trong một request thật.</summary>
    private static (PublicMenuService Service, IDisposable Db) Create(TestDb testDb)
    {
        var tenant = new TenantContext();
        var db = testDb.CreateContextFor(tenant);
        return (new PublicMenuService(db, tenant), db);
    }

    [Fact]
    public async Task Get_ValidSlugs_ReturnsOnlyAvailableOwnProducts_InCategoryOrder()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        var (service, db) = Create(testDb);
        using var _ = db;

        var result = await service.GetAsync("quan-a", "cau-giay");

        Assert.Equal(PublicMenuStatus.Found, result.Status);
        var page = result.Page!;
        Assert.Equal("Quán A", page.TenantName);
        Assert.Equal("Cầu Giấy", page.BranchName);
        Assert.Equal(new[] { "Cà phê", "Trà" }, page.Categories.Select(c => c.Name));   // Tráng miệng không còn món đang bán
        Assert.Equal(new[] { "Bạc xỉu", "Cà phê sữa đá" }, page.Categories[0].Products.Select(p => p.Name));
        var all = page.Categories.SelectMany(c => c.Products).Select(p => p.Name).ToList();
        Assert.DoesNotContain("Bánh flan", all);      // ngừng bán
        Assert.DoesNotContain("Cà phê cũ", all);      // xóa mềm
        Assert.DoesNotContain("Sinh tố bơ", all);     // món của quán khác
    }

    [Theory]
    [InlineData("khong-co", "cau-giay")]   // slug quán sai
    [InlineData("quan-a", "khong-co")]     // slug chi nhánh sai
    [InlineData("quan-a", "thanh-xuan")]   // chi nhánh của quán B nhưng gõ slug quán A
    [InlineData("quan-a", "dong-da")]      // chi nhánh tạm ngưng
    public async Task Get_InvalidOrInactive_ReturnsNotFound(string tenantSlug, string branchSlug)
    {
        using var testDb = new TestDb();
        Seed(testDb);
        var (service, db) = Create(testDb);
        using var _ = db;

        var result = await service.GetAsync(tenantSlug, branchSlug);

        Assert.Equal(PublicMenuStatus.NotFound, result.Status);
        Assert.Null(result.Page);
    }

    [Fact]
    public async Task Get_LockedTenant_ReturnsSuspendedWithName()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        SetStatus(testDb, TestDb.TenantA, TenantStatus.Locked);
        var (service, db) = Create(testDb);
        using var _ = db;

        var result = await service.GetAsync("quan-a", "cau-giay");

        Assert.Equal(PublicMenuStatus.Suspended, result.Status);
        Assert.Equal("Quán A", result.TenantName);
        Assert.Null(result.Page);
    }

    [Theory]
    [InlineData(TenantStatus.PendingActivation)]
    [InlineData(TenantStatus.Rejected)]
    public async Task Get_PendingOrRejectedTenant_ReturnsNotFound(TenantStatus status)
    {
        using var testDb = new TestDb();
        Seed(testDb);
        SetStatus(testDb, TestDb.TenantA, status);
        var (service, db) = Create(testDb);
        using var _ = db;

        var result = await service.GetAsync("quan-a", "cau-giay");

        Assert.Equal(PublicMenuStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Get_TenantWithoutAvailableProducts_ReturnsEmptyCategories()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using (var seed = testDb.CreateContext(tenantId: null))
        {
            foreach (var p in seed.Products.IgnoreQueryFilters().Where(p => p.TenantId == TestDb.TenantA))
            {
                p.IsAvailable = false;
            }
            seed.SaveChanges();
        }
        var (service, db) = Create(testDb);
        using var _ = db;

        var result = await service.GetAsync("quan-a", "cau-giay");

        Assert.Equal(PublicMenuStatus.Found, result.Status);
        Assert.Empty(result.Page!.Categories);
    }

    [Fact]
    public async Task ListSitemapEntries_OnlyActiveTenantsAndActiveBranches()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        SetStatus(testDb, TestDb.TenantB, TenantStatus.Locked);
        var (service, db) = Create(testDb);
        using var _ = db;

        var entries = await service.ListSitemapEntriesAsync();

        Assert.Equal(new[] { new SitemapEntry("quan-a", "cau-giay") }, entries);   // dong-da tạm ngưng, quán B bị khóa
    }
}
