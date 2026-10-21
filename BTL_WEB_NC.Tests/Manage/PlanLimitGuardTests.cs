// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// PlanLimitGuard (R11, R17 khi request đồng thời). SQLite không có UPDLOCK nên ở đây chỉ kiểm tra được hợp đồng:
/// phải gọi trong transaction, và service gọi khóa TRƯỚC bước đếm, bên trong transaction. Khóa thật thử trên SQL Server.
/// </summary>
public class PlanLimitGuardTests
{
    [Fact]
    public async Task GoiNgoaiTransaction_NemLoi()
    {
        using var testDb = new TestDb();
        await using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PlanLimitGuard(db, new TenantContext { TenantId = TestDb.TenantA }).LockTenantAsync());
    }

    [Fact]
    public async Task GoiTrongTransaction_ChayDuoc()
    {
        using var testDb = new TestDb();
        await using var db = testDb.CreateContext(TestDb.TenantA);
        await using var transaction = await db.Database.BeginTransactionAsync();

        await new PlanLimitGuard(db, new TenantContext { TenantId = TestDb.TenantA }).LockTenantAsync();
    }

    [Fact]
    public async Task ThemChiNhanh_KhoaTrongTransactionTruocKhiDem()
    {
        using var testDb = new TestDb();
        await using var db = testDb.CreateContext(TestDb.TenantA);
        var tenant = new TenantContext { TenantId = TestDb.TenantA };
        var spy = new SpyGuard(db);

        await new BranchService(db, new PlanUsageService(db, tenant), spy).CreateBranchAsync(new BranchFormModel
        {
            Name = "Cầu Giấy", Slug = "cau-giay", Address = "Hà Nội",
            OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
        });

        Assert.Equal(1, spy.Calls);
        Assert.True(spy.InTransaction);
        Assert.Null(db.Database.CurrentTransaction); // đã commit
    }

    private sealed class SpyGuard : IPlanLimitGuard
    {
        private readonly AppDbContext _db;

        public SpyGuard(AppDbContext db)
        {
            _db = db;
        }

        public int Calls { get; private set; }
        public bool InTransaction { get; private set; }

        public Task LockTenantAsync(CancellationToken ct = default)
        {
            Calls++;
            InTransaction = _db.Database.CurrentTransaction is not null;
            return Task.CompletedTask;
        }
    }
}
