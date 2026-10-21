// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Tenancy;

/// <summary>Global Query Filter (mục 3.2.2c, R38): đọc chỉ thấy dữ liệu của tenant hiện tại.</summary>
public class TenantFilterTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly int _categoryOfB;
    private readonly int _productOfB;

    public TenantFilterTests()
    {
        // Seed với TenantId tường minh, không có tenant hiện tại (được phép cho seed).
        using var db = _db.CreateContext(tenantId: null);
        var catA = new Category { TenantId = TestDb.TenantA, Name = "Cà phê", SortOrder = 1 };
        var catB = new Category { TenantId = TestDb.TenantB, Name = "Trà", SortOrder = 1 };
        var deletedA = new Category { TenantId = TestDb.TenantA, Name = "Đã xóa", SortOrder = 2, IsDeleted = true };
        db.Categories.AddRange(catA, catB, deletedA);
        db.Products.AddRange(
            new Product { TenantId = TestDb.TenantA, Category = catA, Name = "Bạc xỉu", Price = 30000 },
            new Product { TenantId = TestDb.TenantB, Category = catB, Name = "Trà đào", Price = 35000 });
        db.SaveChanges();

        _categoryOfB = catB.Id;
        _productOfB = db.Products.IgnoreQueryFilters().Single(p => p.Name == "Trà đào").Id;
    }

    [Fact]
    public async Task TenantA_ChiThayDuLieuCuaA()
    {
        using var db = _db.CreateContext(TestDb.TenantA);

        var categories = await db.Categories.Select(c => c.Name).ToListAsync();
        var products = await db.Products.Select(p => p.Name).ToListAsync();

        Assert.Equal(new[] { "Cà phê" }, categories);
        Assert.Equal(new[] { "Bạc xỉu" }, products);
    }

    [Fact]
    public async Task TenantA_TraIdCuaB_KhongThay()
    {
        using var db = _db.CreateContext(TestDb.TenantA);

        // Service sẽ chuyển null thành 404.
        Assert.Null(await db.Categories.FirstOrDefaultAsync(c => c.Id == _categoryOfB));
        Assert.Null(await db.Products.FindAsync(_productOfB));
    }

    [Fact]
    public async Task BanGhiXoaMem_BiAn()
    {
        using var db = _db.CreateContext(TestDb.TenantA);

        Assert.False(await db.Categories.AnyAsync(c => c.Name == "Đã xóa"));
    }

    [Fact]
    public async Task KhongCoTenant_KhongThayGi()
    {
        using var db = _db.CreateContext(tenantId: null);

        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.Products.ToListAsync());
    }

    public void Dispose() => _db.Dispose();
}
