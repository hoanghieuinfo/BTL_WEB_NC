// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>MH10 – A06, A07 và bật/tắt Đang bán. Trọng tâm: cô lập tenant (R38) và xóa mềm món đã bán (R42).</summary>
public class ProductServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly MenuIds _ids;

    public ProductServiceTests()
    {
        _ids = MenuTestData.Seed(_db);
    }

    // ------------------------------------------------------------------ A06

    [Fact]
    public async Task A06_ChiTraMonCuaTenantHienTai()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var result = await MenuTestData.ProductService(db, TestDb.TenantA).ListAsync(new ProductListQuery());

        Assert.Equal(new[] { "Bạc xỉu", "Matcha latte" }, result.Items.Select(p => p.Name));
        Assert.DoesNotContain(result.Items, p => p.Id == _ids.ProductB);
    }

    [Fact]
    public async Task A06_LocDanhMucCuaTenantKhac_KhongTraGi()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var result = await MenuTestData.ProductService(db, TestDb.TenantA)
            .ListAsync(new ProductListQuery { CategoryId = _ids.CategoryB });

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task A06_LocChuaDinhLuong_TrangThai_TimKiem()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);

        var noRecipe = await service.ListAsync(new ProductListQuery { NoRecipe = true });
        var stopped = await service.ListAsync(new ProductListQuery { Status = "stopped" });
        var selling = await service.ListAsync(new ProductListQuery { Status = "selling" });
        var search = await service.ListAsync(new ProductListQuery { Q = "xỉu" });

        Assert.Equal("Matcha latte", Assert.Single(noRecipe.Items).Name);
        Assert.Equal("Matcha latte", Assert.Single(stopped.Items).Name);
        Assert.Equal("Bạc xỉu", Assert.Single(selling.Items).Name);
        var row = Assert.Single(search.Items);
        Assert.Equal(1, row.RecipeCount);
        Assert.Equal(1, row.OrderCount); // dùng cho câu "đã có trong 1 hóa đơn" khi xác nhận xóa
    }

    [Fact]
    public async Task A06_PhanTrang6MonMotTrang_VaKepSoTrangVuotQua()
    {
        using (var seed = _db.CreateContext(TestDb.TenantA))
        {
            for (var i = 1; i <= 8; i++)
            {
                seed.Products.Add(new Product { CategoryId = _ids.CategoryA, Name = $"Món {i:00}", Price = 10_000 });
            }
            await seed.SaveChangesAsync();
        }

        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);
        var page1 = await service.ListAsync(new ProductListQuery { Page = 1 });
        var page2 = await service.ListAsync(new ProductListQuery { Page = 2 });
        var page99 = await service.ListAsync(new ProductListQuery { Page = 99 });

        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(6, page1.Items.Count);
        Assert.Equal(4, page2.Items.Count);
        Assert.Equal(2, page99.Page);
        Assert.Equal(2, page1.TotalPages);
    }

    // ------------------------------------------------------------------ A07: thêm

    [Fact]
    public async Task A07_Them_TuGanTenant()
    {
        int id;
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            id = await MenuTestData.ProductService(db, TestDb.TenantA).CreateAsync(Form(_ids.CategoryA, "Cà phê muối"));
        }

        using var check = _db.CreateContext(tenantId: null);
        var saved = await check.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == id);
        Assert.Equal(TestDb.TenantA, saved.TenantId);
        Assert.Equal(35_000, saved.Price);
    }

    [Fact]
    public async Task A07_Them_VoiCategoryIdCuaTenantKhac_Tra404_KhongTaoMon()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = MenuTestData.ProductService(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Form(_ids.CategoryB, "Món lén")));
        }

        using var check = _db.CreateContext(tenantId: null);
        Assert.False(await check.Products.IgnoreQueryFilters().AnyAsync(p => p.Name == "Món lén"));
    }

    [Fact]
    public async Task A07_Them_TenTrungTrongTenant_LoiTaiOTen()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => service.CreateAsync(Form(_ids.CategoryA, "  Bạc xỉu ")));
        Assert.Equal("name", ex.Field);
    }

    [Fact]
    public async Task A07_Them_TrungTenMonCuaTenantKhac_DuocPhep()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var id = await MenuTestData.ProductService(db, TestDb.TenantA).CreateAsync(Form(_ids.CategoryA, "Trà đào"));

        Assert.True(id > 0);
    }

    [Fact]
    public async Task A07_Them_AnhKhongHopLe_LoiTaiOAnh_KhongTaoMon()
    {
        var images = new FakeImageUploadService { Error = "Nội dung file không phải ảnh hợp lệ." };
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var model = Form(_ids.CategoryA, "Có ảnh giả");
            model.Image = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "image", "gia.jpg");

            var ex = await Assert.ThrowsAsync<FieldValidationException>(
                () => MenuTestData.ProductService(db, TestDb.TenantA, images).CreateAsync(model));
            Assert.Equal("image", ex.Field);
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        Assert.False(await check.Products.AnyAsync(p => p.Name == "Có ảnh giả"));
    }

    // ------------------------------------------------------------------ A07: sửa, xóa, bật/tắt với ID của tenant khác

    [Fact]
    public async Task A07_SuaXoaBatTat_IdCuaTenantKhac_Tra404_DuLieuBKhongDoi()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = MenuTestData.ProductService(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(_ids.ProductB, Form(_ids.CategoryA, "Đổi tên")));
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(_ids.ProductB));
            await Assert.ThrowsAsync<NotFoundException>(() => service.SetAvailabilityAsync(_ids.ProductB, false));
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetRecipeAsync(_ids.ProductB));
        }

        using var check = _db.CreateContext(TestDb.TenantB);
        var productB = await check.Products.SingleAsync(p => p.Id == _ids.ProductB);
        Assert.Equal("Trà đào", productB.Name);
        Assert.True(productB.IsAvailable);
        Assert.False(productB.IsDeleted);
    }

    [Fact]
    public async Task A07_Sua_ChuyenSangDanhMucCuaTenantKhac_Tra404()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = MenuTestData.ProductService(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(_ids.SoldProductA, Form(_ids.CategoryB, "Bạc xỉu")));
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        Assert.Equal(_ids.CategoryA, (await check.Products.SingleAsync(p => p.Id == _ids.SoldProductA)).CategoryId);
    }

    [Fact]
    public async Task A07_Sua_DoiAnh_XoaAnhCuSauKhiLuu()
    {
        var images = new FakeImageUploadService();
        using (var seed = _db.CreateContext(TestDb.TenantA))
        {
            (await seed.Products.SingleAsync(p => p.Id == _ids.SoldProductA)).ImagePath = "/uploads/1/cu.jpg";
            await seed.SaveChangesAsync();
        }

        using var db = _db.CreateContext(TestDb.TenantA);
        var model = Form(_ids.CategoryA, "Bạc xỉu");
        model.Image = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "image", "moi.jpg");
        await MenuTestData.ProductService(db, TestDb.TenantA, images).UpdateAsync(_ids.SoldProductA, model);

        Assert.Equal(new[] { "/uploads/1/cu.jpg" }, images.Deleted);
        Assert.Equal(images.Saved.Single(), (await db.Products.SingleAsync(p => p.Id == _ids.SoldProductA)).ImagePath);
    }

    [Fact]
    public async Task A07_XoaMonDaBan_ChiXoaMem()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var result = await MenuTestData.ProductService(db, TestDb.TenantA).DeleteAsync(_ids.SoldProductA);
            Assert.True(result.SoftDeleted);
        }

        using var check = _db.CreateContext(tenantId: null);
        var product = await check.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == _ids.SoldProductA);
        Assert.True(product.IsDeleted);
        Assert.Equal(1, await check.OrderItems.IgnoreQueryFilters().CountAsync(i => i.ProductId == _ids.SoldProductA));

        using var tenantA = _db.CreateContext(TestDb.TenantA);
        Assert.False(await tenantA.Products.AnyAsync(p => p.Id == _ids.SoldProductA)); // không còn trên thực đơn
    }

    [Fact]
    public async Task A07_XoaMonChuaBan_XoaHanCungDinhLuong()
    {
        using (var seed = _db.CreateContext(TestDb.TenantA))
        {
            seed.Recipes.Add(new Recipe { ProductId = _ids.UnsoldProductA, IngredientId = _ids.IngredientA2, Quantity = 20 });
            await seed.SaveChangesAsync();
        }

        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var result = await MenuTestData.ProductService(db, TestDb.TenantA).DeleteAsync(_ids.UnsoldProductA);
            Assert.False(result.SoftDeleted);
        }

        using var check = _db.CreateContext(tenantId: null);
        Assert.False(await check.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == _ids.UnsoldProductA));
        Assert.False(await check.Recipes.IgnoreQueryFilters().AnyAsync(r => r.ProductId == _ids.UnsoldProductA));
    }

    [Fact]
    public async Task A07_TenMonDaXoaMem_DuocDungLai()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = MenuTestData.ProductService(db, TestDb.TenantA);
        await service.DeleteAsync(_ids.SoldProductA); // đã bán → xóa mềm

        var id = await service.CreateAsync(Form(_ids.CategoryA, "Bạc xỉu"));
        Assert.NotEqual(_ids.SoldProductA, id);
    }

    [Fact]
    public async Task BatTatDangBan()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            await MenuTestData.ProductService(db, TestDb.TenantA).SetAvailabilityAsync(_ids.UnsoldProductA, true);
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        Assert.True((await check.Products.SingleAsync(p => p.Id == _ids.UnsoldProductA)).IsAvailable);
    }

    private static ProductFormModel Form(int categoryId, string name) => new()
    {
        CategoryId = categoryId, Name = name, Price = 35_000, Description = "Mô tả", IsAvailable = true
    };

    public void Dispose() => _db.Dispose();
}
