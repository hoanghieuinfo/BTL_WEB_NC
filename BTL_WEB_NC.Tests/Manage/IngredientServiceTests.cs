// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>MH12 – danh mục nguyên liệu (P06 bước 1, R18).</summary>
public class IngredientServiceTests
{
    [Fact]
    public async Task TrungTenTrongQuan_LoiOName_TrungTenQuanKhac_DuocPhep()
    {
        using var testDb = new TestDb();
        InventoryTestData.Seed(testDb);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            new IngredientService(db).CreateAsync(Form(" Sữa đặc ", "ml", 10)));
        Assert.Equal("name", ex.Field);

        // "Trà đen" là nguyên liệu của quán B → quán A vẫn tạo được.
        var id = await new IngredientService(db).CreateAsync(Form("Trà đen", "g", 200));
        var created = await db.Ingredients.SingleAsync(i => i.Id == id);
        Assert.Equal(TestDb.TenantA, created.TenantId);
    }

    [Fact]
    public void MucToiThieuAm_HoacQuaBaSoLe_KhongHopLe()
    {
        Assert.Contains(Validate(Form("Đường", "g", -1)), r => r.MemberNames.Contains(nameof(IngredientFormModel.MinStock)));
        Assert.Contains(Validate(Form("Đường", "g", 1.2345m)), r => r.MemberNames.Contains(nameof(IngredientFormModel.MinStock)));
        Assert.Empty(Validate(Form("Đường", "g", 0)));
    }

    [Fact]
    public async Task XoaNguyenLieuDangCoDinhLuong_BiChan()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IngredientService(db).DeleteAsync(ids.CoffeeA));
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.True(await check.Ingredients.AnyAsync(i => i.Id == ids.CoffeeA));
    }

    [Fact]
    public async Task XoaMem_AnKhoiDanhSach_TaoLaiCungTenDuoc()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new IngredientService(db).DeleteAsync(ids.AvocadoA);
        }

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var list = await new IngredientService(db).ListAsync(new IngredientListQuery());
            Assert.DoesNotContain(list.Items, i => i.Id == ids.AvocadoA);
            await new IngredientService(db).CreateAsync(Form("Bơ", "g", 50)); // UQ lọc IsDeleted → được tạo lại
        }

        // Bản ghi cũ còn trong CSDL (xóa mềm, R42).
        await using var raw = testDb.CreateContext(tenantId: null);
        Assert.Equal(2, await raw.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Ingredients WHERE Name = 'Bơ'").SingleAsync());
    }

    [Fact]
    public async Task DoiDonViKhiDaDung_LoiOUnit_KhiChuaDung_DoiDuoc()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            new IngredientService(db).UpdateAsync(ids.CoffeeA, Form("Cà phê bột", "kg", 100)));
        Assert.Equal("unit", ex.Field);

        // Bơ: chưa có định lượng, tồn, phiếu nhập → đổi được; đổi tên và mức tối thiểu cùng lúc.
        await new IngredientService(db).UpdateAsync(ids.AvocadoA, Form("Bơ sáp", "quả", 5));
        var avocado = await db.Ingredients.AsNoTracking().SingleAsync(i => i.Id == ids.AvocadoA);
        Assert.Equal(("Bơ sáp", "quả", 5m), (avocado.Name, avocado.Unit, avocado.MinStock));

        // Danh sách báo đơn vị bị khóa cho nút Sửa.
        var rows = (await new IngredientService(db).ListAsync(new IngredientListQuery())).Items;
        Assert.True(rows.Single(r => r.Id == ids.CoffeeA).UnitLocked);
        Assert.False(rows.Single(r => r.Id == ids.AvocadoA).UnitLocked);
        Assert.Equal(2, rows.Single(r => r.Id == ids.CoffeeA).ProductCount);
    }

    [Fact]
    public async Task SuaXoaNguyenLieuQuanKhac_404_KhongDoi()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var service = new IngredientService(db);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(ids.TeaB, Form("Đổi tên", "g", 1)));
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(ids.TeaB));
        }

        await using var check = testDb.CreateContext(TestDb.TenantB);
        var tea = await check.Ingredients.SingleAsync(i => i.Id == ids.TeaB);
        Assert.Equal("Trà đen", tea.Name);
    }

    [Fact]
    public async Task DanhSach_TimTheoTen_LocDonVi_ChiCuaQuanMinh()
    {
        using var testDb = new TestDb();
        InventoryTestData.Seed(testDb);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new IngredientService(db);
        Assert.Equal(new[] { "Cà phê bột", "Bơ", "Đá viên" }.OrderBy(x => x),
            (await service.ListAsync(new IngredientListQuery { Unit = "g" })).Items.Select(i => i.Name).OrderBy(x => x));
        Assert.Equal("Sữa đặc", Assert.Single((await service.ListAsync(new IngredientListQuery { Q = "Sữa" })).Items).Name);
        Assert.Equal(new[] { "g", "ml" }, await service.UnitsAsync());
        Assert.Equal(4, await service.CountAsync());
    }

    private static IngredientFormModel Form(string name, string unit, decimal minStock) =>
        new() { Name = name, Unit = unit, MinStock = minStock };

    private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(object model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
