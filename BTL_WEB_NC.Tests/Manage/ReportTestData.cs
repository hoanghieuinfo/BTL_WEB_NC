// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// Hóa đơn quanh một ngày D cho báo cáo, tính tay được:
/// Quán A – Cầu Giấy (CG), Hoàn Kiếm (HK):
///   1. CG, D 09:00, Đã TT: Cà phê sữa đá 2 × 29.000 + Bạc xỉu 1 × 32.000 = 90.000, giảm 10.000 → 80.000
///   2. CG, D 15:00, Đã TT: Cà phê sữa đá 1 × 29.000 + Sinh tố bơ 1 × 49.000 (ĐÃ HỦY) → 29.000
///   3. HK, D 23:59:59, Đã TT: Trà đào cam sả 2 × 45.000 → 90.000
///   4. CG, D, Đang phục vụ: Bạc xỉu 5 (không tính)          5. CG, D, Đã hủy (không tính)
///   6. CG, D−1 23:59, Đã TT: Cà phê sữa đá 1 → 29.000 (kỳ trước)
///   7. CG, D+1 00:00, Đã TT: Bạc xỉu 1 → 32.000 (ngoài kỳ)
/// Quán B – Thanh Xuân: D 10:00, Đã TT: Trà sữa 10 × 35.000 = 350.000 (không được lẫn vào quán A).
/// Tồn kho quán A: Sữa đặc CG −120/2000 (âm), Cà phê bột CG 640/1000, HK 5000/1000 (đủ), Trà đen HK 180/300,
/// "Đào ngâm" đã xóa mềm CG 0/40 (không được hiện). Quán B: Trân châu TX 10/1000.
/// Ngày D: kỳ này D, kỳ trước D−1 → doanh thu 199.000 / 3 đơn, kỳ trước 29.000 / 1 đơn.
/// </summary>
internal sealed record ReportIds(int BranchCG, int BranchHK, int BranchB, long OrderDiscounted, long OrderB);

internal static class ReportTestData
{
    public static ReportIds Seed(TestDb testDb, DateOnly day)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;
        var d = day.ToDateTime(TimeOnly.MinValue);

        var cashierA = User("thu-ngan-a", "Trần Văn Nam", a);
        var cashierB = User("thu-ngan-b", "Đỗ Quang Huy", b);
        db.Users.AddRange(cashierA, cashierB);

        var cg = Branch(a, "Cầu Giấy", "cau-giay");
        var hk = Branch(a, "Hoàn Kiếm", "hoan-kiem");
        var tx = Branch(b, "Thanh Xuân", "thanh-xuan");
        var tableCg = Table(a, cg, "Bàn 03", "Tầng 1");
        var tableHk = Table(a, hk, "Bàn 07", "Tầng 1");
        var tableTx = Table(b, tx, "Bàn 01", "Trong nhà");

        var cat = new Category { TenantId = a, Name = "Đồ uống", SortOrder = 1 };
        var catB = new Category { TenantId = b, Name = "Trà sữa", SortOrder = 1 };
        var cfs = Product(a, cat, "Cà phê sữa đá", 29_000);
        var bx = Product(a, cat, "Bạc xỉu", 32_000);
        var stb = Product(a, cat, "Sinh tố bơ", 49_000);
        var td = Product(a, cat, "Trà đào cam sả", 45_000);
        var ts = Product(b, catB, "Trà sữa", 35_000);
        db.AddRange(cg, hk, tx, tableCg, tableHk, tableTx, cat, catB, cfs, bx, stb, td, ts);

        var discounted = Paid(a, cg, tableCg, "HD-CG-1", cashierA, d.AddHours(9), discount: 10_000, Item(a, cfs, 2), Item(a, bx, 1));
        var orderB = Paid(b, tx, tableTx, "HD-TX-1", cashierB, d.AddHours(10), 0, Item(b, ts, 10));
        db.Orders.AddRange(
            discounted,
            Paid(a, cg, tableCg, "HD-CG-2", cashierA, d.AddHours(15), 0, Item(a, cfs, 1), Item(a, stb, 1, cancelled: true)),
            Paid(a, hk, tableHk, "HD-HK-1", cashierA, d.AddDays(1).AddSeconds(-1), 0, Item(a, td, 2)),
            Open(a, cg, tableCg, "HD-CG-3", cashierA, d.AddHours(16), OrderStatus.Serving, Item(a, bx, 5)),
            Open(a, cg, tableCg, "HD-CG-4", cashierA, d.AddHours(17), OrderStatus.Cancelled, Item(a, bx, 1)),
            Paid(a, cg, tableCg, "HD-CG-0", cashierA, d.AddMinutes(-1), 0, Item(a, cfs, 1)),
            Paid(a, cg, tableCg, "HD-CG-5", cashierA, d.AddDays(1), 0, Item(a, bx, 1)),
            orderB);

        var milk = new Ingredient { TenantId = a, Name = "Sữa đặc", Unit = "ml", MinStock = 2000 };
        var coffee = new Ingredient { TenantId = a, Name = "Cà phê bột", Unit = "g", MinStock = 1000 };
        var tea = new Ingredient { TenantId = a, Name = "Trà đen", Unit = "g", MinStock = 300 };
        var peach = new Ingredient { TenantId = a, Name = "Đào ngâm", Unit = "miếng", MinStock = 40, IsDeleted = true };
        var pearl = new Ingredient { TenantId = b, Name = "Trân châu", Unit = "g", MinStock = 1000 };
        db.AddRange(milk, coffee, tea, peach, pearl);
        db.BranchStocks.AddRange(
            Stock(a, cg, milk, -120, d), Stock(a, cg, coffee, 640, d), Stock(a, hk, coffee, 5000, d),
            Stock(a, hk, tea, 180, d), Stock(a, cg, peach, 0, d), Stock(b, tx, pearl, 10, d));

        db.SaveChanges();
        return new ReportIds(cg.Id, hk.Id, tx.Id, discounted.Id, orderB.Id);
    }

    private static AppUser User(string id, string name, int tenantId) => new()
    {
        Id = id, UserName = id + "@test", NormalizedUserName = (id + "@TEST").ToUpperInvariant(), Email = id + "@test",
        NormalizedEmail = (id + "@TEST").ToUpperInvariant(), FullName = name, TenantId = tenantId, IsActive = true
    };

    private static Branch Branch(int tenantId, string name, string slug) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = "Hà Nội", Phone = "0241234567",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static DiningTable Table(int tenantId, Branch branch, string name, string zone) => new()
    {
        TenantId = tenantId, Name = name, IsActive = true,
        Zone = new Zone { TenantId = tenantId, Branch = branch, Name = zone, SortOrder = 1 }
    };

    private static Product Product(int tenantId, Category category, string name, decimal price) =>
        new() { TenantId = tenantId, Category = category, Name = name, Price = price, IsAvailable = true };

    private static OrderItem Item(int tenantId, Product product, int quantity, bool cancelled = false) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity,
        IsCancelled = cancelled, CancelReason = cancelled ? "Khách đổi món" : null
    };

    private static Order Paid(int tenantId, Branch branch, DiningTable table, string code, AppUser cashier, DateTime paidAt,
        decimal discount, params OrderItem[] items)
    {
        var order = Open(tenantId, branch, table, code, cashier, paidAt.AddMinutes(-30), OrderStatus.Paid, items);
        order.SubTotal = items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
        order.Discount = discount;
        order.DiscountReason = discount > 0 ? "Khách quen" : null;
        order.Total = order.SubTotal - discount;
        order.PaymentMethod = PaymentMethod.Cash;
        order.CashReceived = order.Total + 20_000;
        order.PaidAt = paidAt;
        order.CashierId = cashier.Id;
        return order;
    }

    private static Order Open(int tenantId, Branch branch, DiningTable table, string code, AppUser staff, DateTime createdAt,
        OrderStatus status, params OrderItem[] items)
    {
        var order = new Order
        {
            TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = status,
            CreatedById = staff.Id, CreatedAt = createdAt, CancelReason = status == OrderStatus.Cancelled ? "Khách rời đi" : null
        };
        foreach (var item in items)
        {
            order.Items.Add(item);
        }
        if (status == OrderStatus.Serving)
        {
            order.SubTotal = order.Total = items.Sum(i => i.UnitPrice * i.Quantity);
        }
        return order;
    }

    private static BranchStock Stock(int tenantId, Branch branch, Ingredient ingredient, decimal quantity, DateTime at) =>
        new() { TenantId = tenantId, Branch = branch, Ingredient = ingredient, Quantity = quantity, UpdatedAt = at };
}
