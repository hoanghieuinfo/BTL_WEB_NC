// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>A08 – định lượng (R15, BM04b).</summary>
public class RecipeTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly MenuIds _ids;

    public RecipeTests()
    {
        _ids = MenuTestData.Seed(_db);
    }

    [Fact]
    public async Task LayDuLieu_ChiCoNguyenLieuCuaTenant()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var data = await MenuTestData.ProductService(db, TestDb.TenantA).GetRecipeAsync(_ids.SoldProductA);

        Assert.Equal(new[] { _ids.IngredientA1, _ids.IngredientA2 }.OrderBy(x => x), data.Ingredients.Select(i => i.Id).OrderBy(x => x));
        Assert.Equal(_ids.IngredientA1, Assert.Single(data.Items).IngredientId);
    }

    [Fact]
    public async Task Luu_NguyenLieuCuaTenantKhac_Tra404_DinhLuongCuGiuNguyen()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = MenuTestData.ProductService(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.SaveRecipeAsync(_ids.SoldProductA, Items((_ids.IngredientA1, 20), (_ids.IngredientB, 5))));
        }

        using var check = _db.CreateContext(tenantId: null);
        var recipes = await check.Recipes.IgnoreQueryFilters().Where(r => r.ProductId == _ids.SoldProductA).ToListAsync();
        var recipe = Assert.Single(recipes);
        Assert.Equal(_ids.IngredientA1, recipe.IngredientId);
        Assert.Equal(15, recipe.Quantity);
    }

    [Fact]
    public async Task Luu_ChoMonCuaTenantKhac_Tra404()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = MenuTestData.ProductService(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.SaveRecipeAsync(_ids.ProductB, Items((_ids.IngredientA1, 5))));
        }

        using var check = _db.CreateContext(TestDb.TenantB);
        Assert.Equal(8, (await check.Recipes.SingleAsync(r => r.ProductId == _ids.ProductB)).Quantity);
    }

    [Fact]
    public async Task Luu_TrungNguyenLieu_LoiTaiDong()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            service.SaveRecipeAsync(_ids.SoldProductA, Items((_ids.IngredientA1, 10), (_ids.IngredientA1, 5))));
        Assert.Equal("items[1].ingredientId", ex.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Luu_DinhLuongKhongDuong_LoiTaiDong(decimal quantity)
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            service.SaveRecipeAsync(_ids.SoldProductA, Items((_ids.IngredientA1, quantity))));
        Assert.Equal("items[0].quantity", ex.Field);
    }

    [Fact]
    public async Task Luu_SuaThemXoaDong()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            // Trước: A1 = 15. Sau: A1 = 20 (sửa), A2 = 30 (thêm).
            await MenuTestData.ProductService(db, TestDb.TenantA)
                .SaveRecipeAsync(_ids.SoldProductA, Items((_ids.IngredientA1, 20), (_ids.IngredientA2, 30.5m)));
        }
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            // Bỏ A1 (xóa dòng).
            await MenuTestData.ProductService(db, TestDb.TenantA).SaveRecipeAsync(_ids.SoldProductA, Items((_ids.IngredientA2, 30.5m)));
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        var recipe = Assert.Single(await check.Recipes.Where(r => r.ProductId == _ids.SoldProductA).ToListAsync());
        Assert.Equal(_ids.IngredientA2, recipe.IngredientId);
        Assert.Equal(30.5m, recipe.Quantity);
        Assert.Equal(TestDb.TenantA, recipe.TenantId);
    }

    [Fact]
    public async Task Luu_DanhSachRong_MonTroThanhChuaDinhLuong()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            await MenuTestData.ProductService(db, TestDb.TenantA).SaveRecipeAsync(_ids.SoldProductA, Items());
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        var missing = await MenuTestData.ProductService(check, TestDb.TenantA).ListAsync(new ProductListQuery { NoRecipe = true });
        Assert.Contains(missing.Items, p => p.Id == _ids.SoldProductA);
    }

    private static List<RecipeItemModel> Items(params (int IngredientId, decimal Quantity)[] items) =>
        items.Select(x => new RecipeItemModel { IngredientId = x.IngredientId, Quantity = x.Quantity }).ToList();

    public void Dispose() => _db.Dispose();
}
