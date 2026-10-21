// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Danh mục món (R13).</summary>
public class CategoryServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly MenuIds _ids;

    public CategoryServiceTests()
    {
        _ids = MenuTestData.Seed(_db);
    }

    [Fact]
    public async Task Them_TenTrung_LoiTaiOTen()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            new CategoryService(db).CreateAsync(new CategoryFormModel { Name = "Cà phê", SortOrder = 2 }));

        Assert.Equal("name", ex.Field);
    }

    [Fact]
    public async Task Xoa_DanhMucConMon_BiChan()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        await Assert.ThrowsAsync<BusinessRuleException>(() => new CategoryService(db).DeleteAsync(_ids.CategoryA));
    }

    [Fact]
    public async Task Xoa_DanhMucRong_XoaMem()
    {
        int id;
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = new CategoryService(db);
            id = await service.CreateAsync(new CategoryFormModel { Name = "Bánh", SortOrder = 2 });
            await service.DeleteAsync(id);
        }

        using var check = _db.CreateContext(tenantId: null);
        Assert.True((await check.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == id)).IsDeleted);
    }

    [Fact]
    public async Task SuaXoa_DanhMucCuaTenantKhac_Tra404()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = new CategoryService(db);
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.UpdateAsync(_ids.CategoryB, new CategoryFormModel { Name = "Đổi", SortOrder = 1 }));
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(_ids.CategoryB));
        }

        using var check = _db.CreateContext(TestDb.TenantB);
        var category = await check.Categories.SingleAsync(c => c.Id == _ids.CategoryB);
        Assert.Equal("Trà", category.Name);
    }

    [Fact]
    public async Task BoDem_ChiTinhTenantHienTai_KhongTinhMonDaXoa()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            await MenuTestData.ProductService(db, TestDb.TenantA).DeleteAsync(_ids.SoldProductA); // xóa mềm
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        var summary = await new CategoryService(check).GetSummaryAsync();

        Assert.Equal(1, summary.TotalProducts);
        Assert.Equal(1, summary.MissingRecipeCount); // Matcha latte
        var category = Assert.Single(summary.Categories);
        Assert.Equal("Cà phê", category.Name);
        Assert.Equal(1, category.ProductCount);
    }

    public void Dispose() => _db.Dispose();
}
