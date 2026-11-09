// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// MH17 – hóa đơn trong ngày (R28). Ngày thử 01/11/2026. Quán A – Cầu Giấy có: P1 10:00 (tiền mặt, giảm 5.000), P2 15:30 (chuyển khoản),
/// P3 23:59:59 cùng ngày, P4 00:00:00 ngày 02/11 và P5 23:59:59 ngày 31/10 (ngoài ngày), đơn Đang phục vụ, đơn Đã hủy.
/// Hoàn Kiếm (cùng quán) và quán B mỗi nơi có một hóa đơn trong ngày.
/// </summary>
public class OrderServiceInvoicesTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);
    private static readonly DateTime D = new(2026, 11, 1);

    private static int Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var staff = NewUser("nv-a", "Nguyễn Văn An", a);
        var staffB = NewUser("nv-b", "Nhân viên B", b);
        db.Users.AddRange(staff, staffB);

        var cg = NewBranch(a, "Cầu Giấy", "cau-giay", "CG");
        var hk = NewBranch(a, "Hoàn Kiếm", "hoan-kiem", "HK");
        var tx = NewBranch(b, "Thanh Xuân", "thanh-xuan", "TX");
        var zCg = new Zone { TenantId = a, Branch = cg, Name = "Tầng 1", SortOrder = 1 };
        var zHk = new Zone { TenantId = a, Branch = hk, Name = "Tầng 1", SortOrder = 1 };
        var zTx = new Zone { TenantId = b, Branch = tx, Name = "Trong nhà", SortOrder = 1 };
        var tables = Enumerable.Range(1, 8).Select(i => NewTable(a, zCg, $"Bàn {i:00}")).ToList();
        var tHk = NewTable(a, zHk, "Bàn HK");
        var tTx = NewTable(b, zTx, "Bàn TX");

        var p1 = Paid(a, cg, tables[0], "HD-CG-261101-001", staff, D.AddHours(10), 50000, 5000);
        p1.PaymentMethod = PaymentMethod.Cash;
        var p2 = Paid(a, cg, tables[1], "HD-CG-261101-002", staff, D.AddHours(15.5), 35000, 0);
        p2.PaymentMethod = PaymentMethod.BankTransfer;
        var p3 = Paid(a, cg, tables[2], "HD-CG-261101-003", staff, D.AddDays(1).AddSeconds(-1), 20000, 0);
        var p4 = Paid(a, cg, tables[3], "HD-CG-261102-001", staff, D.AddDays(1), 99000, 0);
        var p5 = Paid(a, cg, tables[4], "HD-CG-261031-001", staff, D.AddSeconds(-1), 88000, 0);
        var serving = Open(a, cg, tables[5], "HD-CG-261101-004", staff, OrderStatus.Serving);
        var cancelled = Open(a, cg, tables[6], "HD-CG-261101-005", staff, OrderStatus.Cancelled);
        var other = Paid(a, hk, tHk, "HD-HK-261101-001", staff, D.AddHours(11), 70000, 0);
        var otherTenant = Paid(b, tx, tTx, "HD-TX-261101-001", staffB, D.AddHours(12), 45000, 0);

        db.AddRange(cg, hk, tx, zCg, zHk, zTx, tHk, tTx);
        db.AddRange(tables);
        db.Orders.AddRange(p1, p2, p3, p4, p5, serving, cancelled, other, otherTenant);
        db.SaveChanges();
        return cg.Id;
    }

    [Fact]
    public async Task ListPaid_ReturnsOnlyPaidOrdersOfTheDay_NewestFirst_WithRevenue()
    {
        using var testDb = new TestDb();
        var branchId = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new OrderService(db).ListPaidAsync(branchId, Day);

        // Gồm cả 23:59:59 cùng ngày; loại 00:00:00 ngày sau, 23:59:59 hôm trước, đơn đang phục vụ và đơn đã hủy.
        Assert.Equal(new[] { "HD-CG-261101-003", "HD-CG-261101-002", "HD-CG-261101-001" }, result.Items.Select(i => i.Code));
        Assert.Equal(3, result.Count);
        Assert.Equal(105000m, result.TotalRevenue);   // 20.000 + 35.000 + 50.000
        Assert.Equal(Day, result.Day);
    }

    [Fact]
    public async Task ListPaid_RowCarriesTableMethodDiscountAndCashier()
    {
        using var testDb = new TestDb();
        var branchId = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var row = (await new OrderService(db).ListPaidAsync(branchId, Day)).Items.Single(i => i.Code == "HD-CG-261101-001");

        Assert.Equal("Bàn 01", row.TableName);
        Assert.Equal("Tầng 1", row.ZoneName);
        Assert.Equal(PaymentMethod.Cash, row.Method);
        Assert.Equal(5000m, row.Discount);
        Assert.Equal(50000m, row.Total);
        Assert.Equal("Nguyễn Văn An", row.CashierName);
        Assert.Equal(D.AddHours(10), row.PaidAt);
    }

    [Fact]
    public async Task ListPaid_DoesNotLeakOtherBranchOrOtherTenant()
    {
        using var testDb = new TestDb();
        var branchId = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new OrderService(db).ListPaidAsync(branchId, Day);

        Assert.DoesNotContain(result.Items, i => i.Code.StartsWith("HD-HK") || i.Code.StartsWith("HD-TX"));
    }

    [Fact]
    public async Task ListPaid_UnknownBranchOrBranchOfOtherTenant_IsEmpty()
    {
        using var testDb = new TestDb();
        Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        // Quán A truyền chi nhánh của quán B: Global Query Filter loại hết đơn → danh sách rỗng, không lộ gì.
        var txBranchId = testDb.CreateContext(tenantId: null).Branches.IgnoreQueryFilters().Single(b => b.Code == "TX").Id;
        var result = await new OrderService(db).ListPaidAsync(txBranchId, Day);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Empty((await new OrderService(db).ListPaidAsync(99999, Day)).Items);
    }

    [Fact]
    public async Task ListPaid_DayWithoutInvoices_IsEmpty()
    {
        using var testDb = new TestDb();
        var branchId = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var result = await new OrderService(db).ListPaidAsync(branchId, new DateOnly(2026, 12, 25));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Count);
    }

    // ------------------------------------------------------------ Dữ liệu mẫu

    private static Order Paid(int tenantId, Branch branch, DiningTable table, string code, AppUser user, DateTime paidAt, decimal total, decimal discount) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = OrderStatus.Paid,
        CreatedBy = user, CreatedAt = paidAt.AddMinutes(-30), PaidAt = paidAt, Cashier = user,
        SubTotal = total + discount, Discount = discount, DiscountReason = discount > 0 ? "Khách quen" : null,
        Total = total, PaymentMethod = PaymentMethod.Cash
    };

    private static Order Open(int tenantId, Branch branch, DiningTable table, string code, AppUser user, OrderStatus status) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = status,
        CreatedBy = user, CreatedAt = D.AddHours(9)
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

    private static DiningTable NewTable(int tenantId, Zone zone, string name) =>
        new() { TenantId = tenantId, Zone = zone, Name = name, Seats = 4, Status = TableStatus.Empty, IsActive = true };
}
