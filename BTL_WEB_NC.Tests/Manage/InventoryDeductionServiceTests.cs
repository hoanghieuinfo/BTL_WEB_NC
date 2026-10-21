// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Việc 12 – trừ kho khi thanh toán (P07, R30, CLAUDE.md mục 8).</summary>
public class InventoryDeductionServiceTests
{
    [Fact]
    public async Task TruDungDinhLuongNhanSoLuong_GopDongCungMon_BoMonHuy()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderA);
        }

        // Cà phê sữa 2 + 1 = 3 phần, Bạc xỉu 1 phần (3 phần đã hủy không tính), Matcha chưa định lượng.
        // Cà phê bột: 3×20 + 1×10 = 70 → 1000 − 70 = 930. Sữa đặc: 3×30 + 1×40 = 130 → 500 − 130 = 370.
        Assert.Equal(930m, await StockAsync(testDb, ids.BranchA, ids.CoffeeA));
        Assert.Equal(370m, await StockAsync(testDb, ids.BranchA, ids.MilkA));
        Assert.Equal(50m, await StockAsync(testDb, ids.BranchA, ids.IceA)); // không món nào dùng → không đổi
    }

    [Fact]
    public async Task MonChuaDinhLuong_BoQua_KhongTaoDongTon()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        long orderId;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == ids.OrderA);
            orderId = order.Id;
            // Chỉ giữ Matcha (chưa định lượng).
            db.OrderItems.RemoveRange(order.Items.Where(i => i.ProductId != ids.ProductMatcha));
            await db.SaveChangesAsync();
        }
        int rowsBefore;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            rowsBefore = await db.BranchStocks.CountAsync();
            await new InventoryDeductionService(db).DeductForOrderAsync(orderId);
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(rowsBefore, await check.BranchStocks.CountAsync());
        Assert.Equal(1000m, await StockAsync(testDb, ids.BranchA, ids.CoffeeA));
    }

    [Fact]
    public async Task ChiNhanhChuaCoDongTon_TaoMoiRoiTru_ChoPhepAm()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderA2);
        }

        // Hoàn Kiếm chưa có dòng tồn: tạo từ 0 rồi trừ 1 phần Cà phê sữa → âm.
        Assert.Equal(-20m, await StockAsync(testDb, ids.BranchA2, ids.CoffeeA));
        Assert.Equal(-30m, await StockAsync(testDb, ids.BranchA2, ids.MilkA));
    }

    [Fact]
    public async Task TonKhongDu_VanTru_ThanhSoAm()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var stock = await db.BranchStocks.FirstAsync(s => s.BranchId == ids.BranchA && s.IngredientId == ids.MilkA);
            stock.Quantity = 100;
            await db.SaveChangesAsync();
        }

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderA);
        }

        Assert.Equal(-30m, await StockAsync(testDb, ids.BranchA, ids.MilkA)); // 100 − 130
    }

    [Fact]
    public async Task XungDotRowVersion_NemDbUpdateConcurrencyException_KhongDoiTon()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        // Request này đã đọc dòng tồn (RowVersion cũ) ...
        await db.BranchStocks.Where(s => s.BranchId == ids.BranchA).ToListAsync();

        // ... thì một request khác (ví dụ phiếu nhập) cập nhật cùng dòng → RowVersion đổi.
        await using (var other = testDb.CreateContext(TestDb.TenantA))
        {
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE BranchStocks SET Quantity = 2000, RowVersion = randomblob(8) WHERE BranchId = {ids.BranchA} AND IngredientId = {ids.CoffeeA}");
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderA));
        Assert.Equal(2000m, await StockAsync(testDb, ids.BranchA, ids.CoffeeA)); // không bị ghi đè
    }

    [Fact]
    public async Task KhongTuMoTransaction_NoiGoiRollback_TonKhongDoi()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            // Giống A16: nơi gọi mở transaction; nếu service tự Begin/Commit thì dòng dưới sẽ lỗi hoặc không rollback được.
            await using var transaction = await db.Database.BeginTransactionAsync();
            await new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderA);
            Assert.Same(transaction, db.Database.CurrentTransaction);
            await transaction.RollbackAsync();
        }

        Assert.Equal(1000m, await StockAsync(testDb, ids.BranchA, ids.CoffeeA));
    }

    [Fact]
    public async Task DonCuaQuanKhac_404_TonQuanKhacKhongDoi()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new InventoryDeductionService(db).DeductForOrderAsync(ids.OrderB));
        }

        Assert.Equal(300m, await StockAsync(testDb, ids.BranchB, ids.TeaB, TestDb.TenantB));
    }

    private static async Task<decimal?> StockAsync(TestDb testDb, int branchId, int ingredientId, int tenantId = TestDb.TenantA)
    {
        await using AppDbContext db = testDb.CreateContext(tenantId);
        return await db.BranchStocks.Where(s => s.BranchId == branchId && s.IngredientId == ingredientId)
            .Select(s => (decimal?)s.Quantity).FirstOrDefaultAsync();
    }
}
