// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// MH15 – GetAsync. Quán A – Cầu Giấy: Bàn 01 (Tầng 1) có đơn Đang phục vụ với 3 dòng (1 dòng đã hủy);
/// Quán A – Hoàn Kiếm: Bàn 09 có đơn riêng; Quán B – Thanh Xuân: Bàn 01 có đơn riêng.
/// </summary>
public class OrderServiceGetTests
{
    private sealed record Ids(int CauGiay, long OrderCg, long OrderHk, long OrderTx);

    private static Ids Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var staffA = NewUser("nv-a", "Lê Thị Hoa", a);
        var staffB = NewUser("nv-b", "Đỗ Quang Huy", b);
        db.Users.AddRange(staffA, staffB);

        var cg = NewBranch(a, "Cầu Giấy", "cau-giay", "CG");
        var hk = NewBranch(a, "Hoàn Kiếm", "hoan-kiem", "HK");
        var tx = NewBranch(b, "Thanh Xuân", "thanh-xuan", "TX");
        var zoneCg = new Zone { TenantId = a, Branch = cg, Name = "Tầng 1", SortOrder = 1 };
        var zoneHk = new Zone { TenantId = a, Branch = hk, Name = "Tầng 1", SortOrder = 1 };
        var zoneTx = new Zone { TenantId = b, Branch = tx, Name = "Trong nhà", SortOrder = 1 };
        var ban01 = new DiningTable { TenantId = a, Zone = zoneCg, Name = "Bàn 01", Seats = 4, Status = TableStatus.Serving };
        var ban09 = new DiningTable { TenantId = a, Zone = zoneHk, Name = "Bàn 09", Seats = 4, Status = TableStatus.Serving };
        var banTx = new DiningTable { TenantId = b, Zone = zoneTx, Name = "Bàn 01", Seats = 4, Status = TableStatus.Serving };

        var catA = new Category { TenantId = a, Name = "Cà phê" };
        var catB = new Category { TenantId = b, Name = "Trà" };
        var caPhe = new Product { TenantId = a, Category = catA, Name = "Cà phê sữa đá", Price = 29000 };
        var bacXiu = new Product { TenantId = a, Category = catA, Name = "Bạc xỉu", Price = 32000 };
        var traDao = new Product { TenantId = b, Category = catB, Name = "Trà đào", Price = 45000 };

        var orderCg = NewOrder(a, cg, ban01, "HD-CG-261101-001", staffA.Id, guests: 3);
        orderCg.Items.Add(new OrderItem { TenantId = a, Product = bacXiu, ProductName = "Bạc xỉu", UnitPrice = 32000, Quantity = 1 });
        orderCg.Items.Add(new OrderItem { TenantId = a, Product = caPhe, ProductName = "Cà phê sữa đá", UnitPrice = 29000, Quantity = 2, Note = "Ít đá" });
        orderCg.Items.Add(new OrderItem { TenantId = a, Product = bacXiu, ProductName = "Bạc xỉu", UnitPrice = 32000, Quantity = 1, IsCancelled = true, CancelReason = "Khách đổi ý" });
        orderCg.SubTotal = 90000;
        orderCg.Total = 90000;
        var orderHk = NewOrder(a, hk, ban09, "HD-HK-261101-001", staffA.Id);
        var orderTx = NewOrder(b, tx, banTx, "HD-TX-261101-001", staffB.Id);

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, ban01, ban09, banTx, catA, catB, caPhe, bacXiu, traDao);
        db.Orders.AddRange(orderCg, orderHk, orderTx);
        db.SaveChanges();
        return new Ids(cg.Id, orderCg.Id, orderHk.Id, orderTx.Id);
    }

    [Fact]
    public async Task Get_ReturnsHeaderLinesAndTotals()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var detail = await new OrderService(db).GetAsync(ids.CauGiay, ids.OrderCg);

        Assert.Equal("HD-CG-261101-001", detail.Code);
        Assert.Equal("Bàn 01", detail.TableName);
        Assert.Equal("Tầng 1", detail.ZoneName);
        Assert.Equal("Cầu Giấy", detail.BranchName);
        Assert.Equal("Lê Thị Hoa", detail.StaffName);
        Assert.Equal(3, detail.GuestCount);
        Assert.Equal(OrderStatus.Serving, detail.Status);
        Assert.True(detail.IsOpen);
        Assert.Equal(90000m, detail.SubTotal);
        Assert.Equal(90000m, detail.Total);
        Assert.False(string.IsNullOrEmpty(detail.RowVersion));
        Assert.Equal(3, detail.Lines.Count);                          // món hủy vẫn có trong danh sách (R42)
        Assert.Equal(new[] { "Bạc xỉu", "Cà phê sữa đá", "Bạc xỉu" }, detail.Lines.Select(l => l.ProductName));
        Assert.Equal("Ít đá", detail.Lines[1].Note);
        Assert.Equal(58000m, detail.Lines[1].Amount);
        var cancelled = detail.Lines[2];
        Assert.True(cancelled.IsCancelled);
        Assert.Equal("Khách đổi ý", cancelled.CancelReason);
        Assert.Equal(0m, cancelled.Amount);                           // món hủy không tính tiền
    }

    [Fact]
    public async Task Get_OrderOfOtherBranchSameTenant_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).GetAsync(ids.CauGiay, ids.OrderHk));
    }

    [Fact]
    public async Task Get_OrderOfOtherTenant_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).GetAsync(ids.CauGiay, ids.OrderTx));
    }

    [Fact]
    public async Task Get_UnknownOrder_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).GetAsync(ids.CauGiay, 999999));
    }

    private static Order NewOrder(int tenantId, Branch branch, DiningTable table, string code, string userId, int? guests = null) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = OrderStatus.Serving,
        CreatedById = userId, CreatedAt = new DateTime(2026, 11, 1, 9, 30, 0), GuestCount = guests
    };

    private static AppUser NewUser(string id, string fullName, int tenantId) => new()
    {
        Id = id, UserName = id + "@test", NormalizedUserName = (id + "@TEST").ToUpperInvariant(), Email = id + "@test",
        NormalizedEmail = (id + "@TEST").ToUpperInvariant(), FullName = fullName, TenantId = tenantId, IsActive = true
    };

    private static Branch NewBranch(int tenantId, string name, string slug, string code) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Code = code, Address = "Hà Nội", Phone = "0241234567",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };
}
