// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Tenancy;

/// <summary>UNIQUE (TenantId, Name) WHERE IsDeleted = 0 (Bảng 3.18).</summary>
public class UniqueIndexTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task TrungTenChuaXoa_TrongCungTenant_BiChan()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            db.Categories.Add(new Category { Name = "Cà phê" });
            await db.SaveChangesAsync();
        }

        using var db2 = _db.CreateContext(TestDb.TenantA);
        db2.Categories.Add(new Category { Name = "Cà phê" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task TrungTenVoiBanGhiDaXoaMem_DuocPhep()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            db.Categories.Add(new Category { Name = "Cà phê", IsDeleted = true });
            await db.SaveChangesAsync();
        }

        using var db2 = _db.CreateContext(TestDb.TenantA);
        db2.Categories.Add(new Category { Name = "Cà phê" });
        await db2.SaveChangesAsync();

        Assert.Equal(1, await db2.Categories.CountAsync());
    }

    [Fact]
    public async Task CungTen_KhacTenant_DuocPhep()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            db.Categories.Add(new Category { Name = "Cà phê" });
            await db.SaveChangesAsync();
        }

        using var dbB = _db.CreateContext(TestDb.TenantB);
        dbB.Categories.Add(new Category { Name = "Cà phê" });
        await dbB.SaveChangesAsync();

        Assert.Equal(1, await dbB.Categories.CountAsync());
    }

    public void Dispose() => _db.Dispose();
}
