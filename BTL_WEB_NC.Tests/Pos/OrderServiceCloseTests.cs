// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// A29 (tạm tính) và A30 (hủy đơn) – P05 bước 5 và ngoại lệ, R25. Quán A – Cầu Giấy:
///   Bàn 01 Đang phục vụ, đơn có Cà phê × 2 (58.000) + 1 dòng ĐÃ HỦY;
///   Bàn 02 Chờ thanh toán, đơn có Bạc xỉu × 1 (32.000);
///   Bàn 03 Đang phục vụ, đơn CHƯA CÓ MÓN; Bàn 04 Đang phục vụ, đơn chỉ còn dòng ĐÃ HỦY;
///   Bàn 05 Trống, có đơn ĐÃ THANH TOÁN; Bàn 06 Trống, có đơn ĐÃ HỦY.
/// Hoàn Kiếm: Bàn 07 Đang phục vụ (đơn riêng). Quán B: Bàn 01 Đang phục vụ (đơn riêng).
/// </summary>
public class OrderServiceCloseTests
{
    private sealed record Ids(
        int CauGiay, int Product,
        long Order01, int Table01, long Order02, int Table02, long OrderEmpty, int Table03, long OrderAllCancelled, int Table04,
        long OrderPaid, int Table05, long OrderCancelled, long OrderHk, int TableHk, long OrderB, int TableB);

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

        var catA = new Category { TenantId = a, Name = "Cà phê" };
        var catB = new Category { TenantId = b, Name = "Trà" };
        var caPhe = new Product { TenantId = a, Category = catA, Name = "Cà phê sữa đá", Price = 29000 };
        var bacXiu = new Product { TenantId = a, Category = catA, Name = "Bạc xỉu", Price = 32000 };
        var traB = new Product { TenantId = b, Category = catB, Name = "Trà đào", Price = 45000 };

        var order01 = NewOrder(a, cg, ban01, "HD-CG-261101-001", staffA.Id);
        order01.Items.Add(Line(a, caPhe, 2));
        order01.Items.Add(Line(a, bacXiu, 3, cancelled: true));
        order01.SubTotal = 58000;
        order01.Total = 58000;

        var order02 = NewOrder(a, cg, ban02, "HD-CG-261101-002", staffA.Id);
        order02.Items.Add(Line(a, bacXiu, 1));
        order02.SubTotal = 32000;
        order02.Total = 32000;

        var orderEmpty = NewOrder(a, cg, ban03, "HD-CG-261101-003", staffA.Id);

        var orderAllCancelled = NewOrder(a, cg, ban04, "HD-CG-261101-004", staffA.Id);
        orderAllCancelled.Items.Add(Line(a, caPhe, 1, cancelled: true));

        var paid = NewOrder(a, cg, ban05, "HD-CG-261101-005", staffA.Id);
        paid.Status = OrderStatus.Paid;
        paid.Items.Add(Line(a, caPhe, 1));
        paid.SubTotal = 29000;
        paid.Total = 29000;

        var cancelled = NewOrder(a, cg, ban06, "HD-CG-261101-006", staffA.Id);
        cancelled.Status = OrderStatus.Cancelled;
        cancelled.CancelReason = "Khách rời đi";

        var orderHk = NewOrder(a, hk, ban07, "HD-HK-261101-001", staffA.Id);
        orderHk.Items.Add(Line(a, caPhe, 1));
        orderHk.SubTotal = 29000;
        orderHk.Total = 29000;

        var orderB = NewOrder(b, tx, banB1, "HD-TX-261101-001", staffB.Id);
        orderB.Items.Add(Line(b, traB, 1));
        orderB.SubTotal = 45000;
        orderB.Total = 45000;

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban02, ban03, ban04, ban05, ban06, ban07, banB1,
            catA, catB, caPhe, bacXiu, traB);
        db.Orders.AddRange(order01, order02, orderEmpty, orderAllCancelled, paid, cancelled, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, caPhe.Id,
            order01.Id, ban01.Id, order02.Id, ban02.Id, orderEmpty.Id, ban03.Id, orderAllCancelled.Id, ban04.Id,
            paid.Id, ban05.Id, cancelled.Id, orderHk.Id, ban07.Id, orderB.Id, banB1.Id);
    }

    private static CancelOrderModel Reason(string? reason) => new() { Reason = reason };

    // ------------------------------------------------------------ Tạm tính (A29)

    [Fact]
    public async Task Provisional_ServingOrder_SetsTableAwaitingPayment_AndKeepsOrderServing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).ProvisionalAsync(ids.CauGiay, ids.Order01);

        Assert.Equal(TableStatus.AwaitingPayment, detail.TableStatus);
        Assert.Equal(OrderStatus.Serving, detail.Status);
        Assert.Equal(58000m, detail.Total);                                    // tạm tính không đổi tiền

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.AwaitingPayment, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.SingleAsync(o => o.Id == ids.Order01)).Status);
    }

    [Fact]
    public async Task Provisional_WhenAlreadyAwaitingPayment_StaysAwaitingPayment_SoSlipCanBeReprinted()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).ProvisionalAsync(ids.CauGiay, ids.Order02);

        Assert.Equal(TableStatus.AwaitingPayment, detail.TableStatus);
    }

    [Fact]
    public async Task Provisional_OrderWithoutActiveItems_ThrowsBusinessRule_AndTableUnchanged()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Đơn 03 chưa có dòng nào; đơn 04 chỉ còn dòng đã hủy.
        foreach (var orderId in new[] { ids.OrderEmpty, ids.OrderAllCancelled })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => new OrderService(db).ProvisionalAsync(ids.CauGiay, orderId));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table04)).Status);
    }

    [Fact]
    public async Task Provisional_PaidOrCancelledOrder_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderPaid, ids.OrderCancelled })
        {
            await Assert.ThrowsAsync<ConflictException>(() => new OrderService(db).ProvisionalAsync(ids.CauGiay, orderId));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table05)).Status);
    }

    [Fact]
    public async Task Provisional_OrderOfOtherBranchOtherTenantOrUnknown_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).ProvisionalAsync(ids.CauGiay, orderId));
        }

        using var check = testDb.CreateContext(null);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableHk)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableB)).Status);
    }

    [Fact]
    public async Task AddItemAfterProvisional_PutsTableBackToServing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);

        using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new OrderService(db).ProvisionalAsync(ids.CauGiay, ids.Order01);
        }

        using var db2 = testDb.CreateContext(TestDb.TenantA);
        var detail = await new OrderService(db2).SaveItemAsync(ids.CauGiay, ids.Order01,
            new SaveOrderItemModel { ProductId = ids.Product, Quantity = 1 });

        Assert.Equal(TableStatus.Serving, detail.TableStatus);                 // P05 bước 5: gọi thêm thì quay lại Đang phục vụ
        Assert.Equal(87000m, detail.Total);
    }

    // ------------------------------------------------------------ Hủy đơn (A30)

    [Fact]
    public async Task CancelOrder_EmptyOrder_WithoutReason_CancelsAndFreesTable()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).CancelOrderAsync(ids.CauGiay, ids.OrderEmpty, Reason(null));

        Assert.Equal(OrderStatus.Cancelled, detail.Status);

        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == ids.OrderEmpty);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.CancelReason);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
    }

    [Fact]
    public async Task CancelOrder_OrderWithOnlyCancelledLines_WithoutReason_Cancels_AndKeepsLines()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await new OrderService(db).CancelOrderAsync(ids.CauGiay, ids.OrderAllCancelled, Reason("   "));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(OrderStatus.Cancelled, (await check.Orders.SingleAsync(o => o.Id == ids.OrderAllCancelled)).Status);
        Assert.Equal(1, await check.OrderItems.CountAsync(i => i.OrderId == ids.OrderAllCancelled));   // R42: không xóa dòng
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table04)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelOrder_OrderWithItems_WithoutReason_ThrowsFieldValidation_AndChangesNothing(string? reason)
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            new OrderService(db).CancelOrderAsync(ids.CauGiay, ids.Order01, Reason(reason)));
        Assert.Equal("Reason", ex.Field);

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.SingleAsync(o => o.Id == ids.Order01)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
    }

    [Fact]
    public async Task CancelOrder_OrderWithItems_WithReason_CancelsKeepsLinesAndFreesTable()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).CancelOrderAsync(ids.CauGiay, ids.Order01, Reason("  Khách rời đi  "));

        Assert.Equal(OrderStatus.Cancelled, detail.Status);
        Assert.Equal(2, detail.Lines.Count);                                   // R42: giữ nguyên các dòng

        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.SingleAsync(o => o.Id == ids.Order01);
        Assert.Equal("Khách rời đi", order.CancelReason);                      // đã cắt khoảng trắng
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
    }

    [Fact]
    public async Task CancelOrder_AwaitingPaymentOrder_FreesTable()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await new OrderService(db).CancelOrderAsync(ids.CauGiay, ids.Order02, Reason("Khách không thanh toán được"));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table02)).Status);
    }

    [Fact]
    public async Task CancelOrder_PaidOrAlreadyCancelledOrder_ThrowsConflict_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderPaid, ids.OrderCancelled })
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                new OrderService(db).CancelOrderAsync(ids.CauGiay, orderId, Reason("Thử hủy lại")));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(OrderStatus.Paid, (await check.Orders.SingleAsync(o => o.Id == ids.OrderPaid)).Status);
        Assert.Equal("Khách rời đi", (await check.Orders.SingleAsync(o => o.Id == ids.OrderCancelled)).CancelReason);
    }

    [Fact]
    public async Task CancelOrder_OrderOfOtherBranchOtherTenantOrUnknown_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new OrderService(db).CancelOrderAsync(ids.CauGiay, orderId, Reason("Thử hủy đơn quán khác")));
        }

        using var check = testDb.CreateContext(null);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == ids.OrderHk)).Status);
        Assert.Equal(OrderStatus.Serving, (await check.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == ids.OrderB)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableHk)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == ids.TableB)).Status);
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
