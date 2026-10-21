// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Tenancy;

/// <summary>Quy tắc TenantId khi SaveChanges (mục 3.2.2c).</summary>
public class TenantSaveRulesTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task ThemMoi_TuGanTenantHienTai()
    {
        int id;
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var category = new Category { Name = "Sinh tố" }; // không gán TenantId
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            id = category.Id;
        }

        using var check = _db.CreateContext(tenantId: null);
        var saved = await check.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.Equal(TestDb.TenantA, saved.TenantId);
    }

    [Fact]
    public async Task ThemMoi_GanTenantKhac_BiTuChoi()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        db.Categories.Add(new Category { TenantId = TestDb.TenantB, Name = "Lén" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        await AssertNoCategoriesAsync();
    }

    [Fact]
    public void DoiTenantId_BiTuChoi_SaveChanges()
    {
        var id = SeedCategory(TestDb.TenantA);

        using var db = _db.CreateContext(TestDb.TenantA);
        var category = db.Categories.Single(c => c.Id == id);
        category.TenantId = TestDb.TenantB;

        Assert.Throws<TenantViolationException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task DoiTenantId_BiTuChoi_SaveChangesAsync()
    {
        var id = SeedCategory(TestDb.TenantA);

        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var category = await db.Categories.SingleAsync(c => c.Id == id);
            category.TenantId = TestDb.TenantB;

            await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        }

        using var check = _db.CreateContext(tenantId: null);
        var saved = await check.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.Equal(TestDb.TenantA, saved.TenantId);
    }

    [Fact]
    public async Task UpdateDoiTuongTuDungCuaTenantKhac_BiTuChoi()
    {
        var idOfB = SeedCategory(TestDb.TenantB);

        // Kẻ tấn công dựng đối tượng với ID của B rồi gọi Update() (không đọc qua bộ lọc).
        using var db = _db.CreateContext(TestDb.TenantA);
        db.Categories.Update(new Category { Id = idOfB, TenantId = TestDb.TenantB, Name = "Bị sửa" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task KhongCoTenant_KhongLuuDuoc()
    {
        using var db = _db.CreateContext(tenantId: null);
        db.Categories.Add(new Category { Name = "Mồ côi" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        await AssertNoCategoriesAsync();
    }

    private int SeedCategory(int tenantId)
    {
        using var db = _db.CreateContext(tenantId);
        var category = new Category { Name = "Cà phê" };
        db.Categories.Add(category);
        db.SaveChanges();
        return category.Id;
    }

    private async Task AssertNoCategoriesAsync()
    {
        using var check = _db.CreateContext(tenantId: null);
        Assert.Equal(0, await check.Categories.IgnoreQueryFilters().CountAsync());
    }

    public void Dispose() => _db.Dispose();
}
