// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// Dữ liệu kho tối thiểu cho hai tenant:
/// - Quán A: chi nhánh Cầu Giấy (có tồn), Hoàn Kiếm (tạm ngưng, chưa có dòng tồn nào);
///   nguyên liệu Cà phê bột (g, min 100, tồn 1000), Sữa đặc (ml, min 100, tồn 500), Đá viên (g, min 1000, tồn 50 → dưới mức),
///   Bơ (g, min 50, chưa có dòng tồn); món "Cà phê sữa" (20 g cà phê + 30 ml sữa), "Bạc xỉu" (10 g + 40 ml), "Matcha" (chưa định lượng).
///   Đơn đang phục vụ ở Cầu Giấy: Cà phê sữa ×2 + Cà phê sữa ×1 (gọi thêm), Bạc xỉu ×1, Bạc xỉu ×3 ĐÃ HỦY, Matcha ×1.
///   Đơn ở Hoàn Kiếm: Cà phê sữa ×1.
/// - Quán B: chi nhánh Thanh Xuân, nguyên liệu Trà đen (tồn 300), món "Trà đen đá" (8 g), đơn Trà đen đá ×2.
/// </summary>
internal sealed record InventoryIds(
    int BranchA, int BranchA2, int BranchB,
    int CoffeeA, int MilkA, int IceA, int AvocadoA, int TeaB,
    int ProductCoffeeMilk, int ProductBacXiu, int ProductMatcha,
    long OrderA, long OrderA2, long OrderB, string OwnerA, string OwnerB);

internal static class InventoryTestData
{
    public static InventoryIds Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null); // seed: gán TenantId tường minh
        var now = new DateTime(2026, 10, 21, 9, 0, 0);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var ownerA = User("owner-a", "Nguyễn Thị Lan", a);
        var ownerB = User("owner-b", "Hoàng Văn Bình", b);
        db.Users.AddRange(ownerA, ownerB);

        var cauGiay = Branch(a, "Cầu Giấy", "cau-giay", isActive: true);
        var hoanKiem = Branch(a, "Hoàn Kiếm", "hoan-kiem", isActive: false);
        var thanhXuan = Branch(b, "Thanh Xuân", "thanh-xuan", isActive: true);

        var coffee = new Ingredient { TenantId = a, Name = "Cà phê bột", Unit = "g", MinStock = 100 };
        var milk = new Ingredient { TenantId = a, Name = "Sữa đặc", Unit = "ml", MinStock = 100 };
        var ice = new Ingredient { TenantId = a, Name = "Đá viên", Unit = "g", MinStock = 1000 };
        var avocado = new Ingredient { TenantId = a, Name = "Bơ", Unit = "g", MinStock = 50 };
        var tea = new Ingredient { TenantId = b, Name = "Trà đen", Unit = "g", MinStock = 100 };

        var catA = new Category { TenantId = a, Name = "Cà phê", SortOrder = 1 };
        var catB = new Category { TenantId = b, Name = "Trà", SortOrder = 1 };
        var coffeeMilk = Product(a, catA, "Cà phê sữa", (coffee, 20), (milk, 30));
        var bacXiu = Product(a, catA, "Bạc xỉu", (coffee, 10), (milk, 40));
        var matcha = Product(a, catA, "Matcha");
        var teaIce = Product(b, catB, "Trà đen đá", (tea, 8));

        db.AddRange(cauGiay, hoanKiem, thanhXuan, coffee, milk, ice, avocado, tea, catA, catB, coffeeMilk, bacXiu, matcha, teaIce);
        db.BranchStocks.AddRange(
            new BranchStock { TenantId = a, Branch = cauGiay, Ingredient = coffee, Quantity = 1000, UpdatedAt = now },
            new BranchStock { TenantId = a, Branch = cauGiay, Ingredient = milk, Quantity = 500, UpdatedAt = now },
            new BranchStock { TenantId = a, Branch = cauGiay, Ingredient = ice, Quantity = 50, UpdatedAt = now },
            new BranchStock { TenantId = b, Branch = thanhXuan, Ingredient = tea, Quantity = 300, UpdatedAt = now });

        var orderA = Order(a, cauGiay, "HD-CG-001", ownerA.Id, now,
            Item(a, coffeeMilk, 2), Item(a, coffeeMilk, 1), Item(a, bacXiu, 1), Item(a, bacXiu, 3, cancelled: true), Item(a, matcha, 1));
        var orderA2 = Order(a, hoanKiem, "HD-HK-001", ownerA.Id, now, Item(a, coffeeMilk, 1));
        var orderB = Order(b, thanhXuan, "HD-TX-001", ownerB.Id, now, Item(b, teaIce, 2));
        db.Orders.AddRange(orderA, orderA2, orderB);
        db.SaveChanges();

        return new InventoryIds(cauGiay.Id, hoanKiem.Id, thanhXuan.Id, coffee.Id, milk.Id, ice.Id, avocado.Id, tea.Id,
            coffeeMilk.Id, bacXiu.Id, matcha.Id, orderA.Id, orderA2.Id, orderB.Id, ownerA.Id, ownerB.Id);
    }

    private static AppUser User(string id, string name, int tenantId) => new()
    {
        Id = id, UserName = id + "@test", NormalizedUserName = (id + "@TEST").ToUpperInvariant(), Email = id + "@test",
        NormalizedEmail = (id + "@TEST").ToUpperInvariant(), FullName = name, TenantId = tenantId, IsActive = true
    };

    private static Branch Branch(int tenantId, string name, string slug, bool isActive) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = "Hà Nội",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = isActive
    };

    private static Product Product(int tenantId, Category category, string name, params (Ingredient Ingredient, decimal Quantity)[] recipe)
    {
        var product = new Product { TenantId = tenantId, Category = category, Name = name, Price = 30_000, IsAvailable = true };
        foreach (var (ingredient, quantity) in recipe)
        {
            product.Recipes.Add(new Recipe { TenantId = tenantId, Ingredient = ingredient, Quantity = quantity });
        }
        return product;
    }

    private static OrderItem Item(int tenantId, Product product, int quantity, bool cancelled = false) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity,
        IsCancelled = cancelled, CancelReason = cancelled ? "Khách đổi món" : null
    };

    private static Order Order(int tenantId, Branch branch, string code, string staffId, DateTime now, params OrderItem[] items)
    {
        var zone = new Zone { TenantId = tenantId, Branch = branch, Name = "Tầng 1", SortOrder = 1 };
        var table = new DiningTable { TenantId = tenantId, Zone = zone, Name = "Bàn " + code, IsActive = true };
        var order = new Order
        {
            TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = OrderStatus.Serving,
            CreatedById = staffId, CreatedAt = now
        };
        foreach (var item in items)
        {
            order.Items.Add(item);
        }
        return order;
    }
}
