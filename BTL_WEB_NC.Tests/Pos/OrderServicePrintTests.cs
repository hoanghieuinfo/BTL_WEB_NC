// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// MH16 – dữ liệu phiếu in (R27, BM06). Quán A – Cầu Giấy (địa chỉ, SĐT có sẵn):
///   Đơn 1 Đang phục vụ: Cà phê × 2 (30.000) + 1 dòng ĐÃ HỦY, cột SubTotal cố ý lệch (0) → phiếu tạm tính;
///   Đơn 2 ĐÃ THANH TOÁN: Cà phê × 2, giảm 10.000 (Khách quen), tiền mặt, khách đưa 100.000 → hóa đơn; Đơn 3 ĐÃ HỦY;
///   Đơn 4 Đã thanh toán bằng chuyển khoản, không giảm giá. Hoàn Kiếm: đơn riêng. Quán B: đơn riêng.
/// </summary>
public class OrderServicePrintTests
{
    private sealed record Ids(int CauGiay, long Serving, long PaidCash, long Cancelled, long PaidTransfer, long OrderHk, long OrderB);

    private static Ids Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var staffA = NewUser("nv-a", "Nguyễn Văn An", a);
        var cashierA = NewUser("tn-a", "Trần Thu Ngân", a);
        var staffB = NewUser("nv-b", "Nhân viên B", b);
        db.Users.AddRange(staffA, cashierA, staffB);

        var cg = NewBranch(a, "Cầu Giấy", "cau-giay", "CG");
        var hk = NewBranch(a, "Hoàn Kiếm", "hoan-kiem", "HK");
        var tx = NewBranch(b, "Thanh Xuân", "thanh-xuan", "TX");
        var zoneCg = new Zone { TenantId = a, Branch = cg, Name = "Tầng 1", SortOrder = 1 };
        var zoneHk = new Zone { TenantId = a, Branch = hk, Name = "Tầng 1", SortOrder = 1 };
        var zoneTx = new Zone { TenantId = b, Branch = tx, Name = "Trong nhà", SortOrder = 1 };
        var t1 = NewTable(a, zoneCg, "Bàn 01", TableStatus.Serving);
        var t2 = NewTable(a, zoneCg, "Bàn 02", TableStatus.Empty);
        var t3 = NewTable(a, zoneCg, "Bàn 03", TableStatus.Empty);
        var t4 = NewTable(a, zoneCg, "Bàn 04", TableStatus.Empty);
        var t5 = NewTable(a, zoneHk, "Bàn 05", TableStatus.Serving);
        var t6 = NewTable(b, zoneTx, "Bàn 01", TableStatus.Serving);

        var catA = new Category { TenantId = a, Name = "Cà phê" };
        var catB = new Category { TenantId = b, Name = "Trà" };
        var caPhe = new Product { TenantId = a, Category = catA, Name = "Cà phê sữa", Price = 30000 };
        var bacXiu = new Product { TenantId = a, Category = catA, Name = "Bạc xỉu", Price = 35000 };
        var traB = new Product { TenantId = b, Category = catB, Name = "Trà đào", Price = 45000 };

        var serving = NewOrder(a, cg, t1, "HD-CG-261101-001", staffA.Id);
        serving.Items.Add(Line(a, caPhe, 2, note: "Ít đá"));
        serving.Items.Add(Line(a, bacXiu, 3, cancelled: true));

        var paidCash = NewOrder(a, cg, t2, "HD-CG-261101-002", staffA.Id);
        paidCash.Status = OrderStatus.Paid;
        paidCash.GuestCount = 2;
        paidCash.Items.Add(Line(a, caPhe, 2));
        paidCash.SubTotal = 60000;
        paidCash.Discount = 10000;
        paidCash.DiscountReason = "Khách quen";
        paidCash.Total = 50000;
        paidCash.PaymentMethod = PaymentMethod.Cash;
        paidCash.CashReceived = 100000;
        paidCash.PaidAt = new DateTime(2026, 11, 1, 10, 15, 0);
        paidCash.Cashier = cashierA;

        var cancelled = NewOrder(a, cg, t3, "HD-CG-261101-003", staffA.Id);
        cancelled.Status = OrderStatus.Cancelled;
        cancelled.CancelReason = "Khách rời đi";
        cancelled.Items.Add(Line(a, caPhe, 1));

        var paidTransfer = NewOrder(a, cg, t4, "HD-CG-261101-004", staffA.Id);
        paidTransfer.Status = OrderStatus.Paid;
        paidTransfer.Items.Add(Line(a, bacXiu, 1));
        paidTransfer.SubTotal = 35000;
        paidTransfer.Total = 35000;
        paidTransfer.PaymentMethod = PaymentMethod.BankTransfer;
        paidTransfer.PaidAt = new DateTime(2026, 11, 1, 11, 0, 0);
        paidTransfer.Cashier = staffA;

        var orderHk = NewOrder(a, hk, t5, "HD-HK-261101-001", staffA.Id);
        orderHk.Items.Add(Line(a, caPhe, 1));
        var orderB = NewOrder(b, tx, t6, "HD-TX-261101-001", staffB.Id);
        orderB.Items.Add(Line(b, traB, 1));

        db.AddRange(cg, hk, tx, zoneCg, zoneHk, zoneTx, t1, t2, t3, t4, t5, t6, catA, catB, caPhe, bacXiu, traB);
        db.Orders.AddRange(serving, paidCash, cancelled, paidTransfer, orderHk, orderB);
        db.SaveChanges();
        return new Ids(cg.Id, serving.Id, paidCash.Id, cancelled.Id, paidTransfer.Id, orderHk.Id, orderB.Id);
    }

    [Fact]
    public async Task Print_ServingOrder_IsProvisionalSlip_WithHeaderAndOnlyActiveLines()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var before = VietnamTime.Now;
        var slip = await new OrderService(db).GetPrintAsync(ids.CauGiay, ids.Serving);

        Assert.False(slip.IsInvoice);
        Assert.Equal("Quán A", slip.TenantName);                               // đầu phiếu: quán, chi nhánh, địa chỉ, SĐT
        Assert.Equal("Cầu Giấy", slip.BranchName);
        Assert.Equal("Hà Nội", slip.BranchAddress);
        Assert.Equal("0241234567", slip.BranchPhone);
        Assert.Equal("Bàn 01", slip.TableName);
        Assert.Equal("Tầng 1", slip.ZoneName);
        Assert.Equal("Nguyễn Văn An", slip.StaffName);                         // phiếu tạm tính: nhân viên mở đơn

        var line = Assert.Single(slip.Lines);                                  // dòng đã hủy không in
        Assert.Equal("Cà phê sữa", line.Name);
        Assert.Equal("Ít đá", line.Note);
        Assert.Equal(60000m, line.Amount);

        Assert.Equal(60000m, slip.SubTotal);                                   // tính từ dòng in ra, không từ cột SubTotal (đang lệch = 0)
        Assert.Equal(60000m, slip.Total);
        Assert.Equal(0m, slip.Discount);
        Assert.Null(slip.PaymentMethod);
        Assert.Null(slip.Change);
        Assert.InRange(slip.Time, before.AddSeconds(-1), VietnamTime.Now.AddSeconds(1));   // giờ in, giờ Việt Nam
    }

    [Fact]
    public async Task Print_PaidCashOrder_IsInvoiceBm06_WithStoredAmountsAndChange()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var slip = await new OrderService(db).GetPrintAsync(ids.CauGiay, ids.PaidCash);

        Assert.True(slip.IsInvoice);
        Assert.Equal("Trần Thu Ngân", slip.StaffName);                         // hóa đơn: thu ngân
        Assert.Equal(new DateTime(2026, 11, 1, 10, 15, 0), slip.Time);         // giờ thanh toán
        Assert.Equal(2, slip.GuestCount);
        Assert.Equal(60000m, slip.SubTotal);
        Assert.Equal(10000m, slip.Discount);
        Assert.Equal("Khách quen", slip.DiscountReason);
        Assert.Equal(50000m, slip.Total);
        Assert.Equal(PaymentMethod.Cash, slip.PaymentMethod);
        Assert.Equal(100000m, slip.CashReceived);
        Assert.Equal(50000m, slip.Change);                                     // 100.000 − 50.000
    }

    [Fact]
    public async Task Print_PaidTransferOrder_HasNoChange()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var slip = await new OrderService(db).GetPrintAsync(ids.CauGiay, ids.PaidTransfer);

        Assert.True(slip.IsInvoice);
        Assert.Equal(PaymentMethod.BankTransfer, slip.PaymentMethod);
        Assert.Null(slip.CashReceived);
        Assert.Null(slip.Change);
        Assert.Equal(35000m, slip.Total);
    }

    [Fact]
    public async Task Print_CancelledOrder_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).GetPrintAsync(ids.CauGiay, ids.Cancelled));
    }

    [Fact]
    public async Task Print_OrderOfOtherBranchOtherTenantOrUnknown_ThrowsNotFound()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        foreach (var orderId in new[] { ids.OrderHk, ids.OrderB, 99999L })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new OrderService(db).GetPrintAsync(ids.CauGiay, orderId));
        }
    }

    [Fact]
    public async Task Print_DoesNotChangeAnything()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new OrderService(db).GetPrintAsync(ids.CauGiay, ids.Serving);
        }

        // GET chỉ đọc: in phiếu không được đổi trạng thái bàn hay đơn (tạm tính đã đổi ở A29).
        using var check = testDb.CreateContext(TestDb.TenantA);
        var order = await check.Orders.Include(o => o.Table).SingleAsync(o => o.Id == ids.Serving);
        Assert.Equal(OrderStatus.Serving, order.Status);
        Assert.Equal(TableStatus.Serving, order.Table.Status);
        Assert.Equal(0m, order.SubTotal);
    }

    // ------------------------------------------------------------ Dữ liệu mẫu

    private static OrderItem Line(int tenantId, Product product, int quantity, bool cancelled = false, string? note = null) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity,
        Note = note, IsCancelled = cancelled, CancelReason = cancelled ? "Nhầm món" : null
    };

    private static Order NewOrder(int tenantId, Branch branch, DiningTable table, string code, string userId) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = OrderStatus.Serving,
        CreatedById = userId, CreatedAt = new DateTime(2026, 11, 1, 9, 30, 0)
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

    private static DiningTable NewTable(int tenantId, Zone zone, string name, TableStatus status) =>
        new() { TenantId = tenantId, Zone = zone, Name = name, Seats = 4, Status = status, IsActive = true };
}
