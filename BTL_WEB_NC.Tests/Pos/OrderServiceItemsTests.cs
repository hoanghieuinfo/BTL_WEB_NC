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
/// A13 – thêm món, đổi số lượng, ghi chú. Quán A – Cầu Giấy: Bàn 01 Đang phục vụ (đơn rỗng), Bàn 02 Chờ thanh toán
/// (đơn có 1 dòng Bạc xỉu × 1 và 1 dòng ĐÃ HỦY), Bàn 03 có đơn ĐÃ THANH TOÁN; Hoàn Kiếm: Bàn 09 có đơn riêng.
/// Quán B: Bàn 01 có đơn riêng. Món quán A: Cà phê sữa đá 29.000, Bạc xỉu 32.000, Bánh flan (ngừng bán), Món cũ (xóa mềm).
/// </summary>
public class OrderServiceItemsTests
{
    private sealed record Ids(
        int CauGiay, long OrderEmpty, long OrderAwaiting, long OrderPaid, long OrderHk, long OrderB,
        int CaPhe, int BacXiu, int Flan, int Deleted, int ProductB, long LineBacXiu, long LineCancelled);

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
        var flan = new Product { TenantId = a, Category = catA, Name = "Bánh flan", Price = 25000, IsAvailable = false };
        var cu = new Product { TenantId = a, Category = catA, Name = "Món cũ", Price = 10000, IsDeleted = true };
        var traB = new Product { TenantId = b, Category = catB, Name = "Trà đào", Price = 45000 };

        var empty = NewOrder(a, cg, ban01, "HD-CG-261101-001", staffA.Id);
        var awaiting = NewOrder(a, cg, ban02, "HD-CG-261101-002", staffA.Id);
        var lineBacXiu = new OrderItem { TenantId = a, Product = bacXiu, ProductName = "Bạc xỉu", UnitPrice = 32000, Quantity = 1 };
        var lineCancelled = new OrderItem { TenantId = a, Product = caPhe, ProductName = "Cà phê sữa đá", UnitPrice = 29000, Quantity = 3, IsCancelled = true, CancelReason = "Nhầm món" };
        awaiting.Items.Add(lineBacXiu);
        awaiting.Items.Add(lineCancelled);
        awaiting.SubTotal = 32000;
        awaiting.Total = 32000;
        var paid = NewOrder(a, cg, ban03, "HD-CG-261101-003", staffA.Id);
        paid.Status = OrderStatus.Paid;
        var orderHk = NewOrder(a, hk, ban09, "HD-HK-261101-001", staffA.Id);
        var orderB = NewOrder(b, tx, banTx, "HD-TX-261101-001", staffB.Id);

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban02, ban03, ban09, banTx, catA, catB, caPhe, bacXiu, flan, cu, traB);
        db.Orders.AddRange(empty, awaiting, paid, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, empty.Id, awaiting.Id, paid.Id, orderHk.Id, orderB.Id,
            caPhe.Id, bacXiu.Id, flan.Id, cu.Id, traB.Id, lineBacXiu.Id, lineCancelled.Id);
    }

    private static SaveOrderItemModel Add(int productId, int quantity, string? note = null) =>
        new() { ProductId = productId, Quantity = quantity, Note = note };

    private static SaveOrderItemModel Edit(long itemId, int quantity, string? note = null) =>
        new() { ItemId = itemId, Quantity = quantity, Note = note };

    // ------------------------------------------------------------ Thêm món

    [Fact]
    public async Task Add_CreatesLine_WithSnapshotPrice_AndRecalculatesTotals()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(ids.CaPhe, 2, "  Ít đá  "));

        var line = Assert.Single(detail.Lines);
        Assert.Equal("Cà phê sữa đá", line.ProductName);
        Assert.Equal(29000m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("Ít đá", line.Note);                 // ghi chú được cắt khoảng trắng
        Assert.Equal(58000m, detail.SubTotal);
        Assert.Equal(58000m, detail.Total);

        using var check = testDb.CreateContext(TestDb.TenantA);
        var saved = await check.OrderItems.SingleAsync(i => i.OrderId == ids.OrderEmpty);
        Assert.Equal(TestDb.TenantA, saved.TenantId);
        Assert.Equal(58000m, (await check.Orders.SingleAsync(o => o.Id == ids.OrderEmpty)).SubTotal);
    }

    [Fact]
    public async Task Add_SameProductTwice_CreatesTwoSeparateLines()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        await service.SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(ids.CaPhe, 1));
        var detail = await service.SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(ids.CaPhe, 1, "Ít đường"));

        Assert.Equal(2, detail.Lines.Count);              // mỗi lần gọi là một dòng riêng
        Assert.Equal(58000m, detail.SubTotal);
    }

    [Fact]
    public async Task Add_BlankNote_IsStoredAsNull()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(ids.CaPhe, 1, "   "));

        Assert.Null(Assert.Single(detail.Lines).Note);
    }

    [Fact]
    public async Task Add_WhenTableAwaitingPayment_ReturnsTableToServing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderAwaiting, Add(ids.CaPhe, 1));

        Assert.Equal(TableStatus.Serving, detail.TableStatus);
        Assert.Equal(61000m, detail.SubTotal);            // 32.000 (Bạc xỉu) + 29.000; dòng đã hủy không tính
    }

    [Fact]
    public async Task Add_UnavailableProduct_ThrowsBusinessRule_AndAddsNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(ids.Flan, 1)));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Empty(await check.OrderItems.Where(i => i.OrderId == ids.OrderEmpty).ToListAsync());
    }

    [Fact]
    public async Task Add_ProductOfOtherTenantDeletedOrUnknown_ThrowsNotFound_AndAddsNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var productId in new[] { ids.ProductB, ids.Deleted, 99999 })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Add(productId, 1)));
        }

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Empty(await check.OrderItems.Where(i => i.OrderId == ids.OrderEmpty).ToListAsync());
    }

    // ------------------------------------------------------------ Sửa dòng

    [Fact]
    public async Task Edit_ChangesQuantityAndNote_AndRecalculates()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderAwaiting, Edit(ids.LineBacXiu, 3, "Ít ngọt"));

        var line = detail.Lines.Single(l => l.Id == ids.LineBacXiu);
        Assert.Equal(3, line.Quantity);
        Assert.Equal("Ít ngọt", line.Note);
        Assert.Equal(96000m, detail.SubTotal);
        Assert.Equal(96000m, detail.Total);
        Assert.Equal(TableStatus.Serving, detail.TableStatus);
    }

    [Fact]
    public async Task Edit_CancelledLine_ThrowsConflict()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderAwaiting, Edit(ids.LineCancelled, 1)));
    }

    [Fact]
    public async Task Edit_LineOfAnotherOrder_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Dòng thuộc đơn Bàn 02 nhưng gửi kèm đơn Bàn 01.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderEmpty, Edit(ids.LineBacXiu, 2)));
    }

    // ------------------------------------------------------------ Phạm vi đơn

    [Fact]
    public async Task Save_OrderOfOtherBranchOrOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new OrderService(db);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                service.SaveItemAsync(ids.CauGiay, orderId, Add(ids.CaPhe, 1)));
        }
    }

    [Fact]
    public async Task Save_PaidOrder_ThrowsConflict_AndAddsNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new OrderService(db).SaveItemAsync(ids.CauGiay, ids.OrderPaid, Add(ids.CaPhe, 1)));

        using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Empty(await check.OrderItems.Where(i => i.OrderId == ids.OrderPaid).ToListAsync());
    }

    // ------------------------------------------------------------ Kiểm tra dữ liệu vào (model)

    [Theory]
    [InlineData(null, null, 1, false)]      // thiếu cả ItemId lẫn ProductId
    [InlineData(5L, 7, 1, false)]           // có cả hai
    [InlineData(null, 7, 1, true)]          // thêm món
    [InlineData(5L, null, 1, true)]         // sửa dòng
    [InlineData(null, 7, 0, false)]         // số lượng 0
    [InlineData(null, 7, 100, false)]       // số lượng > 99
    [InlineData(null, 0, 1, false)]         // ProductId không hợp lệ
    [InlineData(0L, null, 1, false)]        // ItemId không hợp lệ
    public void Model_RequiresExactlyOneTarget_AndQuantityBetween1And99(long? itemId, int? productId, int quantity, bool valid)
    {
        var model = new SaveOrderItemModel { ItemId = itemId, ProductId = productId, Quantity = quantity };

        Assert.Equal(valid, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void Model_NoteLongerThan100_IsInvalid()
    {
        var model = new SaveOrderItemModel { ProductId = 7, Quantity = 1, Note = new string('x', 101) };

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    // ------------------------------------------------------------ Dữ liệu mẫu

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
