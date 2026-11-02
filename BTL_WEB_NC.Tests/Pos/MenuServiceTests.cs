// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// A11 – tìm, lọc món. Quán A: danh mục Cà phê (thứ tự 1: Cà phê sữa đá, Bạc xỉu, "Cà phê cũ" ĐÃ XÓA), Trà (2: Trà đào cam sả),
/// Tráng miệng (3: Bánh flan NGỪNG BÁN → danh mục không hiện ở thanh lọc). Quán B: danh mục Sinh tố với Sinh tố bơ.
/// </summary>
public class MenuServiceTests
{
    private sealed record Ids(int CaPhe, int Tra, int TrangMieng, int CatB);

    private static Ids Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

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
        return new Ids(caPhe.Id, tra.Id, trangMieng.Id, sinhTo.Id);
    }

    [Fact]
    public async Task Search_NoFilter_ReturnsOnlyAvailableOwnProducts_InCategoryOrder()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new MenuService(db).SearchAsync(null, null);

        Assert.Equal(new[] { "Bạc xỉu", "Cà phê sữa đá", "Trà đào cam sả" }, result.Products.Select(p => p.Name));
        Assert.Equal(new[] { "Cà phê", "Trà" }, result.Categories.Select(c => c.Name));   // Tráng miệng không còn món đang bán
        Assert.DoesNotContain(result.Products, p => p.Name is "Bánh flan" or "Cà phê cũ" or "Sinh tố bơ");
    }

    [Theory]
    [InlineData("ca phe", "Cà phê sữa đá")]        // không dấu
    [InlineData("CÀ PHÊ", "Cà phê sữa đá")]        // hoa/thường
    [InlineData("  sua  da ", "Cà phê sữa đá")]    // khoảng trắng thừa, nhiều từ
    [InlineData("xiu", "Bạc xỉu")]
    [InlineData("dao", "Trà đào cam sả")]          // chữ đ
    public async Task Search_Keyword_IsAccentAndCaseInsensitive(string keyword, string expected)
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new MenuService(db).SearchAsync(keyword, null);

        Assert.Equal(expected, Assert.Single(result.Products).Name);
    }

    [Fact]
    public async Task Search_ByCategory_ReturnsOnlyThatCategory()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new MenuService(db).SearchAsync(null, ids.Tra);

        Assert.Equal("Trà đào cam sả", Assert.Single(result.Products).Name);
        Assert.Equal(2, result.Categories.Count);   // thanh lọc không đổi theo bộ lọc
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyProducts_ButKeepsCategories()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new MenuService(db).SearchAsync("khong co mon nay", null);

        Assert.Empty(result.Products);
        Assert.Equal(2, result.Categories.Count);
    }

    [Fact]
    public async Task Search_CategoryWithoutAvailableProducts_ReturnsEmptyNotError()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new MenuService(db).SearchAsync(null, ids.TrangMieng);

        Assert.Empty(result.Products);
    }

    [Fact]
    public async Task Search_CategoryOfOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new MenuService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(null, ids.CatB));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(null, 99999));
    }

    [Fact]
    public async Task Search_TenantB_SeesOnlyOwnMenu()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantB);

        var result = await new MenuService(db).SearchAsync(null, null);

        Assert.Equal("Sinh tố bơ", Assert.Single(result.Products).Name);
    }

    [Theory]
    [InlineData("Cà Phê", "ca phe")]
    [InlineData("  Đá   Đào ", "da dao")]
    [InlineData(null, "")]
    public void TextSearch_Normalize_RemovesAccentsCaseAndExtraSpaces(string? input, string expected) =>
        Assert.Equal(expected, TextSearch.Normalize(input));
}
