// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// A16 – thanh toán và trừ kho (UC16, R26, R30, CLAUDE.md mục 8). Mọi món giá 30.000. Quán A – Cầu Giấy:
///   Bàn 01 Đang phục vụ, đơn có Cà phê × 2 (60.000; định lượng 20 g cà phê + 30 ml sữa/phần) + dòng ĐÃ HỦY (Bạc xỉu × 3);
///   Bàn 02 Chờ thanh toán, đơn Bạc xỉu × 1 (cột SubTotal cố ý để 0 – server phải tự tính lại từ dòng món);
///   Bàn 03 Đang phục vụ, đơn CHƯA CÓ MÓN; Bàn 04 Đang phục vụ, đơn chỉ còn dòng ĐÃ HỦY;
///   Bàn 05 Trống, có đơn ĐÃ THANH TOÁN; Bàn 06 Trống, có đơn ĐÃ HỦY.
///   Tồn kho Cầu Giấy: cà phê bột 1000 g, sữa đặc 500 ml. Hoàn Kiếm: Bàn 07 Đang phục vụ (đơn riêng). Quán B: Bàn 01 Đang phục vụ (đơn riêng).
/// </summary>
public class PaymentServiceTests
{
    private sealed record Ids(
        int CauGiay, int CoffeeIngredient, int MilkIngredient, string Staff,
        long Order01, int Table01, long Order02, int Table02, long OrderEmpty, int Table03, long OrderAllCancelled, int Table04,
        long OrderPaid, long OrderCancelled, long OrderHk, int TableHk, long OrderB, int TableB);

    private static Ids Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var staffA = NewUser("nv-a", a);
        var staffB = NewUser("nv-b", b);
        db.Users.AddRange(staffA, staffB);

        var cg = NewBranch(a, "Cầu Giấy", "cau-giay", "CG");
        var hk = NewBranch(a, "Hoàn Kiếm", "hoan-kiem", "HK");
        var tx = NewBranch(b, "Thanh Xuân", "thanh-xuan", "TX");
        var zoneCg = new Zone { TenantId = a, Branch = cg, Name = "Tầng 1", SortOrder = 1 };
        var zoneHk = new Zone { TenantId = a, Branch = hk, Name = "Tầng 1", SortOrder = 1 };
        var zoneTx = new Zone { TenantId = b, Branch = tx, Name = "Trong nhà", SortOrder = 1 };
        var ban01 = NewTable(a, zoneCg, "Bàn 01", TableStatus.Serving);
        var ban02 = NewTable(a, zoneCg, "Bàn 02", TableStatus.AwaitingPayment);
        var ban03 = NewTable(a, zoneCg, "Bàn 03", TableStatus.Serving);
        var ban04 = NewTable(a, zoneCg, "Bàn 04", TableStatus.Serving);
        var ban05 = NewTable(a, zoneCg, "Bàn 05", TableStatus.Empty);
        var ban06 = NewTable(a, zoneCg, "Bàn 06", TableStatus.Empty);
        var ban07 = NewTable(a, zoneHk, "Bàn 07", TableStatus.Serving);
        var banB1 = NewTable(b, zoneTx, "Bàn 01", TableStatus.Serving);

        var coffee = new Ingredient { TenantId = a, Name = "Cà phê bột", Unit = "g", MinStock = 100 };
        var milk = new Ingredient { TenantId = a, Name = "Sữa đặc", Unit = "ml", MinStock = 100 };
        var tea = new Ingredient { TenantId = b, Name = "Trà đen", Unit = "g", MinStock = 100 };

        var catA = new Category { TenantId = a, Name = "Cà phê" };
        var catB = new Category { TenantId = b, Name = "Trà" };
        var caPhe = new Product { TenantId = a, Category = catA, Name = "Cà phê sữa", Price = 30000 };
        caPhe.Recipes.Add(new Recipe { TenantId = a, Ingredient = coffee, Quantity = 20 });
        caPhe.Recipes.Add(new Recipe { TenantId = a, Ingredient = milk, Quantity = 30 });
        var bacXiu = new Product { TenantId = a, Category = catA, Name = "Bạc xỉu", Price = 30000 };
        var traB = new Product { TenantId = b, Category = catB, Name = "Trà đen đá", Price = 30000 };
        traB.Recipes.Add(new Recipe { TenantId = b, Ingredient = tea, Quantity = 8 });

        var now = new DateTime(2026, 11, 1, 9, 0, 0);
        db.BranchStocks.AddRange(
            new BranchStock { TenantId = a, Branch = cg, Ingredient = coffee, Quantity = 1000, UpdatedAt = now },
            new BranchStock { TenantId = a, Branch = cg, Ingredient = milk, Quantity = 500, UpdatedAt = now },
            new BranchStock { TenantId = b, Branch = tx, Ingredient = tea, Quantity = 300, UpdatedAt = now });

        var order01 = NewOrder(a, cg, ban01, "HD-CG-261101-001", staffA.Id);
        order01.Items.Add(Line(a, caPhe, 2));
        order01.Items.Add(Line(a, bacXiu, 3, cancelled: true));
        order01.SubTotal = 60000;
        order01.Total = 60000;

        var order02 = NewOrder(a, cg, ban02, "HD-CG-261101-002", staffA.Id);
        order02.Items.Add(Line(a, bacXiu, 1));                                 // SubTotal/Total để 0 cố ý

        var orderEmpty = NewOrder(a, cg, ban03, "HD-CG-261101-003", staffA.Id);

        var orderAllCancelled = NewOrder(a, cg, ban04, "HD-CG-261101-004", staffA.Id);
        orderAllCancelled.Items.Add(Line(a, caPhe, 1, cancelled: true));

        var paid = NewOrder(a, cg, ban05, "HD-CG-261101-005", staffA.Id);
        paid.Status = OrderStatus.Paid;
        paid.Items.Add(Line(a, caPhe, 1));

        var cancelled = NewOrder(a, cg, ban06, "HD-CG-261101-006", staffA.Id);
        cancelled.Status = OrderStatus.Cancelled;
        cancelled.Items.Add(Line(a, caPhe, 1));

        var orderHk = NewOrder(a, hk, ban07, "HD-HK-261101-001", staffA.Id);
        orderHk.Items.Add(Line(a, caPhe, 1));

        var orderB = NewOrder(b, tx, banB1, "HD-TX-261101-001", staffB.Id);
        orderB.Items.Add(Line(b, traB, 1));

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban02, ban03, ban04, ban05, ban06, ban07, banB1,
            coffee, milk, tea, catA, catB, caPhe, bacXiu, traB);
        db.Orders.AddRange(order01, order02, orderEmpty, orderAllCancelled, paid, cancelled, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, coffee.Id, milk.Id, staffA.Id,
            order01.Id, ban01.Id, order02.Id, ban02.Id, orderEmpty.Id, ban03.Id, orderAllCancelled.Id, ban04.Id,
            paid.Id, cancelled.Id, orderHk.Id, ban07.Id, orderB.Id, banB1.Id);
    }

    private static PaymentService Service(AppDbContext db) => new(db, new InventoryDeductionService(db));

    private static async Task<string> RowVersionAsync(TestDb testDb, long orderId)
    {
        using var db = testDb.CreateContext(TestDb.TenantA);
        return Convert.ToBase64String((await db.Orders.SingleAsync(o => o.Id == orderId)).RowVersion);
    }

    private static async Task<PayOrderModel> CashAsync(TestDb testDb, long orderId, decimal received, decimal? discount = null, string? reason = null) => new()
    {
        RowVersion = await RowVersionAsync(testDb, orderId), Method = PaymentMethod.Cash,
        CashReceived = received, Discount = discount, DiscountReason = reason
    };

    private static async Task<decimal> StockAsync(TestDb testDb, int branchId, int ingredientId)
    {
        using var db = testDb.CreateContext(TestDb.TenantA);
        return (await db.BranchStocks.SingleAsync(s => s.BranchId == branchId && s.IngredientId == ingredientId)).Quantity;
    }

    // Sau một lần thanh toán bị từ chối: đơn còn Đang phục vụ, bàn không đổi, kho không đổi (không có gì được ghi).
    private static async Task AssertNothingChangedAsync(TestDb testDb, Ids ids, long orderId, int tableId, TableStatus tableStatus)
    {
        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Serving, order.Status);
        Assert.Null(order.PaidAt);
        Assert.Null(order.CashierId);
        Assert.Null(order.PaymentMethod);
        Assert.Equal(tableStatus, (await check.DiningTables.SingleAsync(t => t.Id == tableId)).Status);
        Assert.Equal(1000m, await StockAsync(testDb, ids.CauGiay, ids.CoffeeIngredient));
        Assert.Equal(500m, await StockAsync(testDb, ids.CauGiay, ids.MilkIngredient));
    }

    // ------------------------------------------------------------ Thanh toán thành công

    [Fact]
    public async Task Pay_Cash_WithDiscount_MarksPaid_DeductsStock_FreesTable()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 100000, discount: 10000, reason: "  Khách quen  ");
        using var db = testDb.CreateContext(TestDb.TenantA);

        var before = VietnamTime.Now;
        var result = await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model);

        Assert.Equal(60000m, result.SubTotal);       // chỉ món chưa hủy
        Assert.Equal(10000m, result.Discount);
        Assert.Equal(50000m, result.Total);
        Assert.Equal(100000m, result.CashReceived);
        Assert.Equal(50000m, result.Change);

        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == ids.Order01);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(50000m, order.Total);
        Assert.Equal(10000m, order.Discount);
        Assert.Equal("Khách quen", order.DiscountReason);                      // đã cắt khoảng trắng
        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
        Assert.Equal(100000m, order.CashReceived);
        Assert.Equal(ids.Staff, order.CashierId);
        Assert.NotNull(order.PaidAt);
        Assert.InRange(order.PaidAt!.Value, before.AddSeconds(-1), VietnamTime.Now.AddSeconds(1));   // giờ Việt Nam
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);

        // Cà phê sữa × 2 → 40 g cà phê, 60 ml sữa; dòng Bạc xỉu đã hủy không bị trừ.
        Assert.Equal(960m, await StockAsync(testDb, ids.CauGiay, ids.CoffeeIngredient));
        Assert.Equal(440m, await StockAsync(testDb, ids.CauGiay, ids.MilkIngredient));
    }

    [Fact]
    public async Task Pay_Transfer_IgnoresCashReceived_AndHasNoChange()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = new PayOrderModel
        {
            RowVersion = await RowVersionAsync(testDb, ids.Order01), Method = PaymentMethod.BankTransfer, CashReceived = 5
        };
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model);

        Assert.Equal(60000m, result.Total);
        Assert.Null(result.CashReceived);
        Assert.Equal(0m, result.Change);

        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == ids.Order01);
        Assert.Equal(PaymentMethod.BankTransfer, order.PaymentMethod);
        Assert.Null(order.CashReceived);
        Assert.Null(order.DiscountReason);                                      // không giảm giá → không lưu lý do
    }

    [Fact]
    public async Task Pay_RecomputesTotalFromItems_NotFromOrderColumns()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order02, received: 30000);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Cột Orders.SubTotal của đơn 02 đang là 0 (lệch); tổng phải lấy từ dòng món × đơn giá đã lưu.
        var result = await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order02, model);

        Assert.Equal(30000m, result.SubTotal);
        Assert.Equal(30000m, result.Total);

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(30000m, (await check.Orders.SingleAsync(o => o.Id == ids.Order02)).Total);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table02)).Status);   // trả thẳng từ Chờ thanh toán
    }

    [Fact]
    public async Task Pay_DiscountEqualsSubTotal_IsAllowed_TotalZero()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 0, discount: 60000, reason: "Quà tặng");
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model);

        Assert.Equal(0m, result.Total);
        Assert.Equal(0m, result.Change);
    }

    // ------------------------------------------------------------ Validate (UC16 3a, 3b)

    [Theory]
    [InlineData(60001, "x", "Discount")]       // lớn hơn tổng tiền hàng
    [InlineData(10000, null, "DiscountReason")] // giảm giá > 0 mà không có lý do
    [InlineData(10000, "   ", "DiscountReason")]
    public async Task Pay_InvalidDiscount_ThrowsFieldValidation_AndChangesNothing(int discount, string? reason, string field)
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 1_000_000, discount: discount, reason: reason);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));

        Assert.Equal(field, ex.Field);
        await AssertNothingChangedAsync(testDb, ids, ids.Order01, ids.Table01, TableStatus.Serving);
    }

    [Fact]
    public async Task Pay_NegativeDiscount_ThrowsFieldValidation()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 1_000_000, discount: -1, reason: "x");
        using var db = testDb.CreateContext(TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));

        Assert.Equal("Discount", ex.Field);
        await AssertNothingChangedAsync(testDb, ids, ids.Order01, ids.Table01, TableStatus.Serving);
    }

    [Theory]
    [InlineData(null)]      // thiếu tiền khách đưa
    [InlineData(59999)]     // thiếu 1 đồng
    public async Task Pay_CashNotEnough_ThrowsFieldValidation_AndChangesNothing(int? received)
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = new PayOrderModel
        {
            RowVersion = await RowVersionAsync(testDb, ids.Order01), Method = PaymentMethod.Cash, CashReceived = received
        };
        using var db = testDb.CreateContext(TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));

        Assert.Equal("CashReceived", ex.Field);
        await AssertNothingChangedAsync(testDb, ids, ids.Order01, ids.Table01, TableStatus.Serving);
    }

    [Fact]
    public async Task Pay_CashMustCoverTotalAfterDiscount()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Tổng 60.000 − giảm 10.000 = 50.000: khách đưa 49.999 thì thiếu, 50.000 thì đủ.
        await Assert.ThrowsAsync<FieldValidationException>(async () =>
            await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, await CashAsync(testDb, ids.Order01, 49999, 10000, "Voucher")));

        var result = await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, await CashAsync(testDb, ids.Order01, 50000, 10000, "Voucher"));
        Assert.Equal(0m, result.Change);
    }

    [Fact]
    public async Task Pay_OrderWithoutActiveItems_ThrowsBusinessRule_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Đơn 03 chưa có dòng nào; đơn 04 chỉ còn dòng đã hủy.
        foreach (var (orderId, tableId) in new[] { (ids.OrderEmpty, ids.Table03), (ids.OrderAllCancelled, ids.Table04) })
        {
            var model = new PayOrderModel { RowVersion = await RowVersionAsync(testDb, orderId), Method = PaymentMethod.BankTransfer };
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, orderId, model));
            await AssertNothingChangedAsync(testDb, ids, orderId, tableId, TableStatus.Serving);
        }
    }

    [Fact]
    public async Task Pay_InvalidRowVersionText_ThrowsBusinessRule_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var text in new[] { "khong-phai-base64!", "", null })
        {
            var model = new PayOrderModel { RowVersion = text, Method = PaymentMethod.BankTransfer };
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));
        }

        await AssertNothingChangedAsync(testDb, ids, ids.Order01, ids.Table01, TableStatus.Serving);
    }

    // ------------------------------------------------------------ Thanh toán hai lần, xung đột (409)

    [Fact]
    public async Task Pay_Twice_SecondThrowsConflict_AndStockDeductedOnlyOnce()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 60000);

        using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model);
        }

        using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Assert.ThrowsAsync<ConflictException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));
        }

        Assert.Equal(960m, await StockAsync(testDb, ids.CauGiay, ids.CoffeeIngredient));   // chỉ trừ một lần
        Assert.Equal(440m, await StockAsync(testDb, ids.CauGiay, ids.MilkIngredient));
    }

    [Fact]
    public async Task Pay_PaidOrCancelledOrder_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderPaid, ids.OrderCancelled })
        {
            var model = new PayOrderModel { RowVersion = await RowVersionAsync(testDb, orderId), Method = PaymentMethod.BankTransfer };
            await Assert.ThrowsAsync<ConflictException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, orderId, model));
        }

        Assert.Equal(1000m, await StockAsync(testDb, ids.CauGiay, ids.CoffeeIngredient));
    }

    [Fact]
    public async Task Pay_WithStaleRowVersion_ThrowsConcurrency_AndRollsBackEverything()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 60000);   // trang MH15 đã vẽ với RowVersion này

        // Máy khác sửa đơn (ví dụ gọi thêm món) → RowVersion đổi trong lúc nhân viên đang mở hộp thoại thanh toán.
        using (var other = testDb.CreateContext(TestDb.TenantA))
        {
            await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE Orders SET RowVersion = randomblob(8) WHERE Id = {ids.Order01}");
        }

        using var db = testDb.CreateContext(TestDb.TenantA);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));

        await AssertNothingChangedAsync(testDb, ids, ids.Order01, ids.Table01, TableStatus.Serving);
    }

    [Fact]
    public async Task Pay_WhenStockConflicts_RollsBackOrderAndTableToo()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        var model = await CashAsync(testDb, ids.Order01, received: 60000);

        using var db = testDb.CreateContext(TestDb.TenantA);
        // Request này đã đọc dòng tồn (RowVersion cũ) ...
        await db.BranchStocks.Where(s => s.BranchId == ids.CauGiay).ToListAsync();
        // ... thì một request khác (ví dụ phiếu nhập) sửa cùng dòng.
        using (var other = testDb.CreateContext(TestDb.TenantA))
        {
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE BranchStocks SET Quantity = 2000, RowVersion = randomblob(8) WHERE BranchId = {ids.CauGiay} AND IngredientId = {ids.CoffeeIngredient}");
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, ids.Order01, model));

        // Đơn đã được SaveChanges trước khi trừ kho, nhưng transaction hoàn tác: đơn vẫn Đang phục vụ, bàn chưa Trống, tồn là giá trị của request kia.
        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == ids.Order01);
        Assert.Equal(OrderStatus.Serving, order.Status);
        Assert.Null(order.PaidAt);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
        Assert.Equal(2000m, await StockAsync(testDb, ids.CauGiay, ids.CoffeeIngredient));
        Assert.Equal(500m, await StockAsync(testDb, ids.CauGiay, ids.MilkIngredient));
    }

    // ------------------------------------------------------------ Cô lập tenant / chi nhánh (404)

    [Fact]
    public async Task Pay_OrderOfOtherBranchOtherTenantOrUnknown_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            var model = new PayOrderModel { RowVersion = Convert.ToBase64String(new byte[8]), Method = PaymentMethod.BankTransfer };
            await Assert.ThrowsAsync<NotFoundException>(() => Service(db).PayAsync(ids.CauGiay, ids.Staff, orderId, model));
        }

        using var check = testDb.CreateContext(tenantId: null);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == ids.OrderHk)).Status);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == ids.OrderB)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableHk)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableB)).Status);
        Assert.Equal(300m, (await check.BranchStocks.IgnoreQueryFilters().SingleAsync(s => s.TenantId == TestDb.TenantB)).Quantity);
    }

    // ------------------------------------------------------------ Validate body (PayOrderModel)

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(10000.0, true)]
    [InlineData(-1.0, false)]        // giảm giá âm
    [InlineData(100.5, false)]       // phần lẻ: cột tiền là decimal(18,0)
    public void PayOrderModel_ValidatesDiscount(double discount, bool valid)
    {
        var model = new PayOrderModel { RowVersion = "AAAAAAAAB9E=", Method = PaymentMethod.Cash, Discount = (decimal)discount };

        Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void PayOrderModel_RejectsMethodOutsideEnum()
    {
        var model = new PayOrderModel { RowVersion = "AAAAAAAAB9E=", Method = (PaymentMethod)99 };

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void PayOrderModel_RequiresRowVersionAndMethod()
    {
        var model = new PayOrderModel();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PayOrderModel.RowVersion)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PayOrderModel.Method)));
    }

    [Fact]
    public void PayOrderModel_RejectsFractionalOrNegativeCashReceived()
    {
        foreach (var received in new[] { -1m, 100.5m })
        {
            var model = new PayOrderModel { RowVersion = "AAAAAAAAB9E=", Method = PaymentMethod.Cash, CashReceived = received };
            Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
        }
    }

    // ------------------------------------------------------------ Dữ liệu mẫu

    private static OrderItem Line(int tenantId, Product product, int quantity, bool cancelled = false) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity,
        IsCancelled = cancelled, CancelReason = cancelled ? "Nhầm món" : null
    };

    private static Order NewOrder(int tenantId, Branch branch, DiningTable table, string code, string userId) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = OrderStatus.Serving,
        CreatedById = userId, CreatedAt = new DateTime(2026, 11, 1, 9, 30, 0)
    };

    private static AppUser NewUser(string id, int tenantId) => new()
    {
        Id = id, UserName = id + "@test", NormalizedUserName = (id + "@TEST").ToUpperInvariant(), Email = id + "@test",
        NormalizedEmail = (id + "@TEST").ToUpperInvariant(), FullName = "Nhân viên " + id, TenantId = tenantId, IsActive = true
    };

    private static Branch NewBranch(int tenantId, string name, string slug, string code) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Code = code, Address = "Hà Nội", Phone = "0241234567",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static DiningTable NewTable(int tenantId, Zone zone, string name, TableStatus status) =>
        new() { TenantId = tenantId, Zone = zone, Name = name, Seats = 4, Status = status, IsActive = true };
}
