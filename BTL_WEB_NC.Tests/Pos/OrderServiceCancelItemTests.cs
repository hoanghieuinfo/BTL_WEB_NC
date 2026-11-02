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
/// A14 – hủy món (R25, R42). Quán A – Cầu Giấy: Bàn 01 Đang phục vụ (đơn có Cà phê × 2 = 58.000 và Bạc xỉu × 1 = 32.000),
/// Bàn 02 Chờ thanh toán (đơn có 1 dòng Cà phê × 1), Bàn 03 có đơn ĐÃ THANH TOÁN; Hoàn Kiếm: Bàn 09 có đơn riêng.
/// Quán B: Bàn 01 có đơn riêng.
/// </summary>
public class OrderServiceCancelItemTests
{
    private sealed record Ids(
        int CauGiay, long OrderServing, long OrderAwaiting, long OrderPaid, long OrderHk, long OrderB,
        long LineCaPhe, long LineBacXiu, long LineAwaiting, long LinePaid, long LineHk, long LineB);

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
        var ban09 = NewTable(a, zoneHk, "Bàn 09", TableStatus.Serving);
        var banTx = NewTable(b, zoneTx, "Bàn 01", TableStatus.Serving);

        var catA = new Category { TenantId = a, Name = "Cà phê" };
        var catB = new Category { TenantId = b, Name = "Trà" };
        var caPhe = new Product { TenantId = a, Category = catA, Name = "Cà phê sữa đá", Price = 29000 };
        var bacXiu = new Product { TenantId = a, Category = catA, Name = "Bạc xỉu", Price = 32000 };
        var traB = new Product { TenantId = b, Category = catB, Name = "Trà đào", Price = 45000 };

        var serving = NewOrder(a, cg, ban01, "HD-CG-261101-001", staffA.Id);
        var lineCaPhe = Line(a, caPhe, 2);
        var lineBacXiu = Line(a, bacXiu, 1);
        serving.Items.Add(lineCaPhe);
        serving.Items.Add(lineBacXiu);
        serving.SubTotal = 90000;
        serving.Total = 90000;

        var awaiting = NewOrder(a, cg, ban02, "HD-CG-261101-002", staffA.Id);
        var lineAwaiting = Line(a, caPhe, 1);
        awaiting.Items.Add(lineAwaiting);
        awaiting.SubTotal = 29000;
        awaiting.Total = 29000;

        var paid = NewOrder(a, cg, ban03, "HD-CG-261101-003", staffA.Id);
        paid.Status = OrderStatus.Paid;
        var linePaid = Line(a, caPhe, 1);
        paid.Items.Add(linePaid);
        paid.SubTotal = 29000;
        paid.Total = 29000;

        var orderHk = NewOrder(a, hk, ban09, "HD-HK-261101-001", staffA.Id);
        var lineHk = Line(a, bacXiu, 1);
        orderHk.Items.Add(lineHk);

        var orderB = NewOrder(b, tx, banTx, "HD-TX-261101-001", staffB.Id);
        var lineB = Line(b, traB, 1);
        orderB.Items.Add(lineB);

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban02, ban03, ban09, banTx, catA, catB, caPhe, bacXiu, traB);
        db.Orders.AddRange(serving, awaiting, paid, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, serving.Id, awaiting.Id, paid.Id, orderHk.Id, orderB.Id,
            lineCaPhe.Id, lineBacXiu.Id, lineAwaiting.Id, linePaid.Id, lineHk.Id, lineB.Id);
    }

    private static CancelOrderItemModel Reason(string? reason) => new() { Reason = reason };

    // ------------------------------------------------------------ Hủy thành công

    [Fact]
    public async Task Cancel_MarksLineCancelled_KeepsRow_AndRecalculatesTotals()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineCaPhe, Reason("  Khách đổi ý  "));

        Assert.Equal(2, detail.Lines.Count);                                  // dòng vẫn còn (R42)
        var cancelled = detail.Lines.Single(l => l.Id == ids.LineCaPhe);
        Assert.True(cancelled.IsCancelled);
        Assert.Equal("Khách đổi ý", cancelled.CancelReason);                  // lý do được cắt khoảng trắng
        Assert.Equal(0m, cancelled.Amount);                                   // món hủy không tính tiền
        Assert.Equal(32000m, detail.SubTotal);                                // chỉ còn Bạc xỉu
        Assert.Equal(32000m, detail.Total);

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(2, await check.OrderItems.CountAsync(i => i.OrderId == ids.OrderServing));   // không xóa dòng
        var order = await check.Orders.SingleAsync(o => o.Id == ids.OrderServing);
        Assert.Equal(32000m, order.SubTotal);
        Assert.Equal(32000m, order.Total);
    }

    [Fact]
    public async Task Cancel_LastActiveLine_LeavesOrderWithZeroTotal_AndStaysOpen()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        await service.CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineCaPhe, Reason("Nhầm món"));
        var detail = await service.CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineBacXiu, Reason("Hết nguyên liệu"));

        Assert.All(detail.Lines, l => Assert.True(l.IsCancelled));
        Assert.Equal(0m, detail.SubTotal);
        Assert.Equal(0m, detail.Total);
        Assert.Equal(OrderStatus.Serving, detail.Status);                     // hủy hết món không tự hủy đơn (hủy đơn là bước 7)
    }

    [Fact]
    public async Task Cancel_WhenTableAwaitingPayment_ReturnsTableToServing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).CancelItemAsync(ids.CauGiay, ids.OrderAwaiting, ids.LineAwaiting, Reason("Khách đổi ý"));

        Assert.Equal(TableStatus.Serving, detail.TableStatus);
    }

    // ------------------------------------------------------------ Lý do bắt buộc

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancel_BlankReason_Throws_AndChangesNothing(string? reason)
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<FieldValidationException>(() =>
            new OrderService(db).CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineCaPhe, Reason(reason)));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.False((await check.OrderItems.SingleAsync(i => i.Id == ids.LineCaPhe)).IsCancelled);
        Assert.Equal(90000m, (await check.Orders.SingleAsync(o => o.Id == ids.OrderServing)).SubTotal);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Khách đổi ý", true)]
    public void Model_ReasonIsRequired(string? reason, bool valid)
    {
        var model = Reason(reason);

        Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void Model_ReasonLongerThan255_IsInvalid()
    {
        var tooLong = Reason(new string('x', 256));
        var maxLen = Reason(new string('x', 255));

        Assert.False(Validator.TryValidateObject(tooLong, new ValidationContext(tooLong), new List<ValidationResult>(), validateAllProperties: true));
        Assert.True(Validator.TryValidateObject(maxLen, new ValidationContext(maxLen), new List<ValidationResult>(), validateAllProperties: true));
    }

    // ------------------------------------------------------------ Trạng thái

    [Fact]
    public async Task Cancel_AlreadyCancelledLine_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        await service.CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineCaPhe, Reason("Nhầm món"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelItemAsync(ids.CauGiay, ids.OrderServing, ids.LineCaPhe, Reason("Lần hai")));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal("Nhầm món", (await check.OrderItems.SingleAsync(i => i.Id == ids.LineCaPhe)).CancelReason);   // lý do đầu tiên không bị ghi đè
    }

    [Fact]
    public async Task Cancel_PaidOrder_ThrowsConflict_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new OrderService(db).CancelItemAsync(ids.CauGiay, ids.OrderPaid, ids.LinePaid, Reason("Muộn rồi")));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.False((await check.OrderItems.SingleAsync(i => i.Id == ids.LinePaid)).IsCancelled);
        Assert.Equal(29000m, (await check.Orders.SingleAsync(o => o.Id == ids.OrderPaid)).Total);
    }

    // ------------------------------------------------------------ Phạm vi: tenant, chi nhánh, khóa ngoại sai

    [Fact]
    public async Task Cancel_OrderOfOtherBranchOrOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var (orderId, itemId) in new[] { (ids.OrderHk, ids.LineHk), (ids.OrderB, ids.LineB), (99999L, ids.LineCaPhe) })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.CancelItemAsync(ids.CauGiay, orderId, itemId, Reason("Thử")));
        }

        await AssertNoLineCancelledAsync(testDb);                              // không dòng nào (kể cả quán B) bị đổi
    }

    [Fact]
    public async Task Cancel_LineOfAnotherOrderOrUnknown_ThrowsNotFound_AndChangesNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        // Dòng của đơn khác trong cùng chi nhánh, dòng của chi nhánh khác, dòng của quán khác, dòng không tồn tại – gửi kèm đơn Bàn 01.
        foreach (var itemId in new[] { ids.LineAwaiting, ids.LineHk, ids.LineB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.CancelItemAsync(ids.CauGiay, ids.OrderServing, itemId, Reason("Thử")));
        }

        await AssertNoLineCancelledAsync(testDb);
        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(90000m, (await check.Orders.SingleAsync(o => o.Id == ids.OrderServing)).SubTotal);
    }

    // Context không có tenant bị query filter loại hết bản ghi, nên đọc xuyên tenant phải IgnoreQueryFilters; đếm trước để không pass khi rỗng.
    private static async Task AssertNoLineCancelledAsync(TestDb testDb)
    {
        using var check = testDb.CreateContext(tenantId: null);
        var lines = await check.OrderItems.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(6, lines.Count);
        Assert.All(lines, i => Assert.False(i.IsCancelled));
    }

    // ------------------------------------------------------------ Dữ liệu mẫu

    private static OrderItem Line(int tenantId, Product product, int quantity) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity
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
