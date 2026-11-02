// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// A15 – chuyển bàn và gộp bàn (R25). Quán A – Cầu Giấy:
///   Bàn 01 Đang phục vụ, đơn có Cà phê × 2 (58.000) + 1 dòng ĐÃ HỦY (Bạc xỉu × 3);
///   Bàn 02 Chờ thanh toán, đơn có Bạc xỉu × 1 (32.000);
///   Bàn 03 Trống; Bàn 04 Trống nhưng ngừng hoạt động; Bàn 06 Trống, có đơn ĐÃ THANH TOÁN;
///   Bàn 08 trạng thái Trống nhưng vẫn còn đơn đang phục vụ (dữ liệu lệch do hai máy thao tác cùng lúc).
/// Hoàn Kiếm: Bàn 09 Trống, Bàn 10 Đang phục vụ (đơn riêng). Quán B: Bàn 01 Trống, Bàn 02 Đang phục vụ (đơn riêng).
/// </summary>
public class OrderServiceMoveTests
{
    private sealed record Ids(
        int CauGiay,
        long Order01, int Table01, long Order02, int Table02, int Table03, int Table04, long OrderPaid, int Table06,
        long Order08, int Table08, int TableHkEmpty, long OrderHk, int TableHkServing, int TableBEmpty, long OrderB, int TableBServing);

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
        var ban03 = NewTable(a, zoneCg, "Bàn 03", TableStatus.Empty);
        var ban04 = NewTable(a, zoneCg, "Bàn 04", TableStatus.Empty);
        ban04.IsActive = false;
        var ban06 = NewTable(a, zoneCg, "Bàn 06", TableStatus.Empty);
        var ban08 = NewTable(a, zoneCg, "Bàn 08", TableStatus.Empty);
        var ban09 = NewTable(a, zoneHk, "Bàn 09", TableStatus.Empty);
        var ban10 = NewTable(a, zoneHk, "Bàn 10", TableStatus.Serving);
        var banB1 = NewTable(b, zoneTx, "Bàn 01", TableStatus.Empty);
        var banB2 = NewTable(b, zoneTx, "Bàn 02", TableStatus.Serving);

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

        var paid = NewOrder(a, cg, ban06, "HD-CG-261101-003", staffA.Id);
        paid.Status = OrderStatus.Paid;
        paid.Items.Add(Line(a, caPhe, 1));
        paid.SubTotal = 29000;
        paid.Total = 29000;

        var order08 = NewOrder(a, cg, ban08, "HD-CG-261101-004", staffA.Id);

        var orderHk = NewOrder(a, hk, ban10, "HD-HK-261101-001", staffA.Id);
        orderHk.Items.Add(Line(a, caPhe, 1));
        orderHk.SubTotal = 29000;
        orderHk.Total = 29000;

        var orderB = NewOrder(b, tx, banB2, "HD-TX-261101-001", staffB.Id);
        orderB.Items.Add(Line(b, traB, 1));

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban02, ban03, ban04, ban06, ban08, ban09, ban10, banB1, banB2,
            catA, catB, caPhe, bacXiu, traB);
        db.Orders.AddRange(order01, order02, paid, order08, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, order01.Id, ban01.Id, order02.Id, ban02.Id, ban03.Id, ban04.Id, paid.Id, ban06.Id,
            order08.Id, ban08.Id, ban09.Id, orderHk.Id, ban10.Id, banB1.Id, orderB.Id, banB2.Id);
    }

    private static MoveOrderModel To(int tableId) => new() { TargetTableId = tableId };

    // ------------------------------------------------------------ Chuyển bàn

    [Fact]
    public async Task Transfer_MovesOrderToEmptyTable_AndSwapsStatuses()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).TransferAsync(ids.CauGiay, ids.Order01, To(ids.Table03));

        Assert.Equal(ids.Table03, detail.TableId);
        Assert.Equal("Bàn 03", detail.TableName);
        Assert.Equal(TableStatus.Serving, detail.TableStatus);
        Assert.Equal(58000m, detail.Total);                                    // món và tiền không đổi
        Assert.Equal(2, detail.Lines.Count);

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
        Assert.Equal(ids.Table03, (await check.Orders.SingleAsync(o => o.Id == ids.Order01)).TableId);
    }

    [Fact]
    public async Task Transfer_AwaitingPaymentOrder_KeepsAwaitingPaymentOnNewTable()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).TransferAsync(ids.CauGiay, ids.Order02, To(ids.Table03));

        Assert.Equal(TableStatus.AwaitingPayment, detail.TableStatus);

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table02)).Status);
        Assert.Equal(TableStatus.AwaitingPayment, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
    }

    [Fact]
    public async Task Transfer_ToTableWithGuests_ThrowsConflict_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Bàn 02 đang Chờ thanh toán; Bàn 08 trạng thái Trống nhưng còn đơn đang phục vụ.
        foreach (var tableId in new[] { ids.Table02, ids.Table08 })
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                new OrderService(db).TransferAsync(ids.CauGiay, ids.Order01, To(tableId)));
        }

        await AssertOrder01UntouchedAsync(testDb, ids);
    }

    [Fact]
    public async Task Transfer_ToInactiveOtherBranchOtherTenantOrUnknownTable_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var tableId in new[] { ids.Table04, ids.TableHkEmpty, ids.TableBEmpty, 99999 })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.TransferAsync(ids.CauGiay, ids.Order01, To(tableId)));
        }

        await AssertOrder01UntouchedAsync(testDb, ids);
    }

    [Fact]
    public async Task Transfer_ToSameTable_ThrowsBusinessRule()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new OrderService(db).TransferAsync(ids.CauGiay, ids.Order01, To(ids.Table01)));

        await AssertOrder01UntouchedAsync(testDb, ids);
    }

    [Fact]
    public async Task Transfer_ClosedOrder_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new OrderService(db).TransferAsync(ids.CauGiay, ids.OrderPaid, To(ids.Table03)));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(ids.Table06, (await check.Orders.SingleAsync(o => o.Id == ids.OrderPaid)).TableId);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
    }

    [Fact]
    public async Task Transfer_OrderOfOtherBranchOrOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.TransferAsync(ids.CauGiay, orderId, To(ids.Table03)));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
    }

    // ------------------------------------------------------------ Gộp bàn

    [Fact]
    public async Task Merge_MovesAllLinesToTarget_RecalculatesTotals_AndCancelsSource()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var merged = await new OrderService(db).MergeAsync(ids.CauGiay, ids.Order01, To(ids.Table02));

        Assert.Equal(ids.Order02, merged.TargetOrderId);
        Assert.Equal("HD-CG-261101-002", merged.TargetCode);
        Assert.Equal("Bàn 02", merged.TargetTableName);

        using var check = testDb.CreateContext(TestDb.TenantA);
        var target = await check.Orders.Include(o => o.Items).SingleAsync(o => o.Id == ids.Order02);
        Assert.Equal(3, target.Items.Count);                                   // 1 của đích + 2 của nguồn (kể cả dòng đã hủy – R42)
        Assert.Equal(1, target.Items.Count(i => i.IsCancelled));
        Assert.Equal(90000m, target.SubTotal);                                 // 32.000 + 58.000; dòng hủy không tính
        Assert.Equal(90000m, target.Total);
        Assert.Equal(OrderStatus.Serving, target.Status);

        var source = await check.Orders.Include(o => o.Items).SingleAsync(o => o.Id == ids.Order01);
        Assert.Empty(source.Items);
        Assert.Equal(OrderStatus.Cancelled, source.Status);
        Assert.Equal("Gộp vào đơn HD-CG-261101-002", source.CancelReason);
        Assert.Equal(0m, source.SubTotal);
        Assert.Equal(0m, source.Total);

        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table02)).Status);   // Chờ thanh toán → Đang phục vụ
    }

    [Fact]
    public async Task Merge_KeepsLineDataIntact()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await new OrderService(db).MergeAsync(ids.CauGiay, ids.Order01, To(ids.Table02));

        using var check = testDb.CreateContext(TestDb.TenantA);
        var lines = await check.OrderItems.Where(i => i.OrderId == ids.Order02).OrderBy(i => i.Id).ToListAsync();
        var cancelled = Assert.Single(lines, i => i.IsCancelled);
        Assert.Equal("Bạc xỉu", cancelled.ProductName);
        Assert.Equal(32000m, cancelled.UnitPrice);                             // giá lúc bán được giữ nguyên
        Assert.Equal(3, cancelled.Quantity);
        Assert.Equal(TestDb.TenantA, cancelled.TenantId);
    }

    [Fact]
    public async Task Merge_IntoTableWithoutOpenOrder_ThrowsConflict_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Bàn 03 trống, Bàn 06 chỉ có đơn đã thanh toán.
        foreach (var tableId in new[] { ids.Table03, ids.Table06 })
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                new OrderService(db).MergeAsync(ids.CauGiay, ids.Order01, To(tableId)));
        }

        await AssertOrder01UntouchedAsync(testDb, ids);
    }

    [Fact]
    public async Task Merge_IntoInactiveOtherBranchOtherTenantOrUnknownTable_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var tableId in new[] { ids.Table04, ids.TableHkServing, ids.TableBServing, 99999 })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.MergeAsync(ids.CauGiay, ids.Order01, To(tableId)));
        }

        await AssertOrder01UntouchedAsync(testDb, ids);

        // Đơn của chi nhánh Hoàn Kiếm và quán B không bị chạm vào.
        using var check = testDb.CreateContext(tenantId: null);
        var others = await check.Orders.IgnoreQueryFilters().Where(o => o.Id == ids.OrderHk || o.Id == ids.OrderB).Include(o => o.Items).ToListAsync();
        Assert.Equal(2, others.Count);
        Assert.All(others, o => Assert.Equal(1, o.Items.Count));
    }

    [Fact]
    public async Task Merge_IntoSameTable_ThrowsBusinessRule()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new OrderService(db).MergeAsync(ids.CauGiay, ids.Order01, To(ids.Table01)));

        await AssertOrder01UntouchedAsync(testDb, ids);
    }

    [Fact]
    public async Task Merge_ClosedSourceOrder_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new OrderService(db).MergeAsync(ids.CauGiay, ids.OrderPaid, To(ids.Table02)));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(OrderStatus.Paid, (await check.Orders.SingleAsync(o => o.Id == ids.OrderPaid)).Status);
        Assert.Equal(1, await check.OrderItems.CountAsync(i => i.OrderId == ids.Order02));
    }

    [Fact]
    public async Task Merge_SourceOrderOfOtherBranchOrOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.MergeAsync(ids.CauGiay, orderId, To(ids.Table02)));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(1, await check.OrderItems.CountAsync(i => i.OrderId == ids.Order02));
    }

    // ------------------------------------------------------------ Kiểm tra dữ liệu vào (model)

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(7, true)]
    public void Model_TargetTableIdIsRequiredAndPositive(int? tableId, bool valid)
    {
        var model = new MoveOrderModel { TargetTableId = tableId };

        Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    // ------------------------------------------------------------ Dùng chung

    // Sau một lần chuyển/gộp bị từ chối, đơn 01 và Bàn 01 phải y như ban đầu và đơn 02 không nhận thêm món.
    private static async Task AssertOrder01UntouchedAsync(TestDb testDb, Ids ids)
    {
        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.Include(o => o.Items).SingleAsync(o => o.Id == ids.Order01);
        Assert.Equal(ids.Table01, order.TableId);
        Assert.Equal(OrderStatus.Serving, order.Status);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(58000m, order.SubTotal);
        Assert.Equal(TableStatus.Serving, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table01)).Status);
        Assert.Equal(1, await check.OrderItems.CountAsync(i => i.OrderId == ids.Order02));
        Assert.Equal(TableStatus.Empty, (await check.DiningTables.SingleAsync(t => t.Id == ids.Table03)).Status);
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
