// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Data;

/// <summary>
/// Dữ liệu mẫu cho môi trường phát triển. Chạy khi chưa có tenant nào (TenantContext rỗng),
/// nên mọi bản ghi nghiệp vụ được gán TenantId tường minh – trường hợp mục 3.2.2c cho phép.
/// Random(42) cố định để số liệu lặp lại được khi tính tay đối chiếu báo cáo.
/// </summary>
public static class SeedData
{
    /// <summary>Mật khẩu chung của tài khoản thử – chỉ dùng cho dữ liệu demo, không phải mật khẩu thật.</summary>
    public const string DemoPassword = "Cafe@2026";

    public record SeedAccount(string Email, string Role, string FullName, string? TenantName);

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        if (await db.ServicePlans.AnyAsync())
        {
            return;
        }

        var accounts = await SeedAsync(db,
            sp.GetRequiredService<UserManager<AppUser>>(),
            sp.GetRequiredService<RoleManager<IdentityRole>>(),
            VietnamTime.Now);

        Console.WriteLine();
        Console.WriteLine($"=== Đã seed dữ liệu mẫu. Mật khẩu mọi tài khoản: {DemoPassword} ===");
        foreach (var a in accounts)
        {
            Console.WriteLine($"  {a.Role,-12} {a.Email,-28} {a.FullName}{(a.TenantName is null ? "" : $" – {a.TenantName}")}");
        }
        Console.WriteLine();
    }

    public static async Task<IReadOnlyList<SeedAccount>> SeedAsync(
        AppDbContext db, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, DateTime now)
    {
        var accounts = new List<SeedAccount>();
        var random = new Random(42);

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // BM02 – Bảng gói dịch vụ.
        var trial = new ServicePlan { Name = "Dùng thử", MaxBranches = 1, MaxStaff = 3, PricePerMonth = 0, TrialDays = 30 };
        var basic = new ServicePlan { Name = "Cơ bản", MaxBranches = 1, MaxStaff = 10, PricePerMonth = 199_000 };
        var chain = new ServicePlan { Name = "Chuỗi", MaxBranches = 5, MaxStaff = 50, PricePerMonth = 499_000 };
        db.ServicePlans.AddRange(trial, basic, chain);

        var tenantA = new Tenant
        {
            Name = "Cà Phê Mây", Slug = "ca-phe-may", Phone = "0912345678", ServicePlan = chain,
            Status = TenantStatus.Active, PlanExpiresAt = now.Date.AddDays(20), CreatedAt = now.AddDays(-60)
        };
        var tenantB = new Tenant
        {
            Name = "Trà Gió", Slug = "tra-gio", Phone = "0987654321", ServicePlan = basic,
            Status = TenantStatus.Active, PlanExpiresAt = now.Date.AddDays(5), CreatedAt = now.AddDays(-45)
        };
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();

        accounts.Add(await CreateUserAsync(userManager, "admin@cafesaas.test", "Quản trị hệ thống", AppRoles.SuperAdmin, null, null));

        await SeedTenantAAsync(db, userManager, tenantA, now, random, accounts);
        await SeedTenantBAsync(db, userManager, tenantB, now, random, accounts);

        return accounts;
    }

    // ------------------------------------------------------------------ Quán A: gói Chuỗi, 2 chi nhánh

    private static async Task SeedTenantAAsync(AppDbContext db, UserManager<AppUser> userManager,
        Tenant tenant, DateTime now, Random random, List<SeedAccount> accounts)
    {
        var t = tenant.Id;

        var cauGiay = NewBranch(t, "Cầu Giấy", "cau-giay", "125 Trần Duy Hưng, Cầu Giấy, Hà Nội", "0241234567");
        var hoanKiem = NewBranch(t, "Hoàn Kiếm", "hoan-kiem", "18 Hàng Bài, Hoàn Kiếm, Hà Nội", "0247654321");
        db.Branches.AddRange(cauGiay, hoanKiem);

        var tablesCg = AddZones(db, t, cauGiay, ("Tầng 1", 4), ("Tầng 2", 3), ("Sân vườn", 2));
        var tablesHk = AddZones(db, t, hoanKiem, ("Tầng 1", 4), ("Ban công", 3));

        // Nguyên liệu: (tên, đơn vị cơ sở, mức tồn tối thiểu).
        var ing = AddIngredients(db, t,
            ("Cà phê bột", "g", 2000), ("Sữa đặc", "ml", 3000), ("Sữa tươi", "ml", 5000),
            ("Đá viên", "g", 10000), ("Đường", "g", 2000), ("Trà đen", "g", 500),
            ("Đào ngâm", "g", 1000), ("Bột matcha", "g", 300), ("Bơ quả", "g", 2000),
            ("Cam tươi", "quả", 20), ("Bánh croissant", "cái", 20), ("Bánh tiramisu", "cái", 10),
            ("Hạt hướng dương", "g", 1000), ("Khoai tây chiên", "g", 3000));

        var cats = AddCategories(db, t, "Cà phê", "Trà", "Sinh tố & nước ép", "Bánh", "Đồ ăn vặt");

        var products = new List<Product>
        {
            AddProduct(db, t, cats[0], "Cà phê đen đá", 25_000, ing, ("Cà phê bột", 25), ("Đường", 10), ("Đá viên", 150)),
            AddProduct(db, t, cats[0], "Cà phê sữa đá", 29_000, ing, ("Cà phê bột", 25), ("Sữa đặc", 30), ("Đá viên", 150)),
            AddProduct(db, t, cats[0], "Bạc xỉu", 32_000, ing, ("Cà phê bột", 15), ("Sữa đặc", 30), ("Sữa tươi", 60), ("Đá viên", 120)),
            AddProduct(db, t, cats[0], "Cà phê sữa tươi", 35_000, ing, ("Cà phê bột", 20), ("Sữa tươi", 100), ("Đá viên", 120)),
            AddProduct(db, t, cats[1], "Trà đào cam sả", 45_000, ing, ("Trà đen", 8), ("Đào ngâm", 60), ("Cam tươi", 0.5m), ("Đường", 15), ("Đá viên", 150)),
            AddProduct(db, t, cats[1], "Trà đen đá", 25_000, ing, ("Trà đen", 8), ("Đường", 15), ("Đá viên", 150)),
            // Món chưa có định lượng → bán không trừ kho (R15, khối "Cảnh báo trừ kho" ở MH10).
            AddProduct(db, t, cats[1], "Matcha latte", 49_000, ing),
            AddProduct(db, t, cats[2], "Sinh tố bơ", 49_000, ing, ("Bơ quả", 150), ("Sữa đặc", 30), ("Sữa tươi", 50), ("Đá viên", 100)),
            AddProduct(db, t, cats[2], "Nước cam ép", 40_000, ing, ("Cam tươi", 3), ("Đường", 10), ("Đá viên", 100)),
            AddProduct(db, t, cats[2], "Nước ép cam đào", 45_000, ing, ("Cam tươi", 2), ("Đào ngâm", 40), ("Đá viên", 100)),
            AddProduct(db, t, cats[3], "Bánh croissant bơ", 35_000, ing, ("Bánh croissant", 1)),
            AddProduct(db, t, cats[3], "Bánh tiramisu", 45_000, ing, ("Bánh tiramisu", 1)),
            AddProduct(db, t, cats[4], "Hướng dương", 15_000, ing, ("Hạt hướng dương", 100)),
            AddProduct(db, t, cats[4], "Khoai tây chiên", 30_000, ing, ("Khoai tây chiên", 150)),
            AddProduct(db, t, cats[4], "Combo khoai tây & trà đen", 50_000, ing, ("Khoai tây chiên", 150), ("Trà đen", 8), ("Đường", 15), ("Đá viên", 150)),
        };
        products.Single(p => p.Name == "Sinh tố bơ").IsAvailable = false; // Ngừng bán

        // Tồn kho: đủ ở hầu hết; Cầu Giấy có Đào ngâm dưới mức và Bánh croissant âm (R19).
        AddStocks(db, t, cauGiay, ing, now, factor: 3,
            ("Đào ngâm", 800m), ("Bánh croissant", -3m));
        AddStocks(db, t, hoanKiem, ing, now, factor: 2);

        // Lưu trước để chi nhánh có Id thật, gán được BranchId cho nhân viên.
        await db.SaveChangesAsync();

        accounts.Add(await CreateUserAsync(userManager, "chuquan@caphemay.test", "Nguyễn Thị Lan", AppRoles.TenantAdmin, tenant, null));
        var staffCg = new[]
        {
            await CreateUserAsync(userManager, "nam.cg@caphemay.test", "Trần Văn Nam", AppRoles.Staff, tenant, cauGiay.Id),
            await CreateUserAsync(userManager, "hoa.cg@caphemay.test", "Lê Thị Hoa", AppRoles.Staff, tenant, cauGiay.Id),
        };
        var staffHk = new[]
        {
            await CreateUserAsync(userManager, "duc.hk@caphemay.test", "Phạm Minh Đức", AppRoles.Staff, tenant, hoanKiem.Id),
            // Nhân viên mới: hiện nhãn "Chưa đổi mật khẩu" ở MH11.
            await CreateUserAsync(userManager, "trang.hk@caphemay.test", "Vũ Thu Trang", AppRoles.Staff, tenant, hoanKiem.Id, mustChangePassword: true),
        };
        accounts.AddRange(staffCg);
        accounts.AddRange(staffHk);

        var sellable = products.Where(p => p.IsAvailable).ToList();
        AddOrders(db, t, cauGiay, "CG", tablesCg, await UserIdsAsync(userManager, staffCg), sellable, now, random, ordersPerDay: (14, 22));
        AddOrders(db, t, hoanKiem, "HK", tablesHk, await UserIdsAsync(userManager, staffHk), sellable, now, random, ordersPerDay: (10, 18));

        // Một đơn đang phục vụ tại bàn đầu tiên của Cầu Giấy.
        var servingTable = tablesCg[0];
        servingTable.Status = TableStatus.Serving;
        var waiterId = (await UserIdsAsync(userManager, staffCg))[0];
        var serving = new Order
        {
            TenantId = t, Branch = cauGiay, Table = servingTable, Code = $"HD-CG-{now:yyMMdd}-999",
            Status = OrderStatus.Serving, GuestCount = 2, CreatedById = waiterId, CreatedAt = now.AddMinutes(-25)
        };
        serving.Items.Add(NewItem(t, products[1], 2));
        serving.Items.Add(NewItem(t, products[10], 1));
        serving.SubTotal = serving.Items.Sum(i => i.UnitPrice * i.Quantity);
        serving.Total = serving.SubTotal;
        db.Orders.Add(serving);

        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Quán B: gói Cơ bản, 1 chi nhánh (đủ giới hạn)

    private static async Task SeedTenantBAsync(AppDbContext db, UserManager<AppUser> userManager,
        Tenant tenant, DateTime now, Random random, List<SeedAccount> accounts)
    {
        var t = tenant.Id;

        var thanhXuan = NewBranch(t, "Thanh Xuân", "thanh-xuan", "52 Nguyễn Trãi, Thanh Xuân, Hà Nội", "0249998887");
        db.Branches.Add(thanhXuan);
        var tables = AddZones(db, t, thanhXuan, ("Trong nhà", 4), ("Vỉa hè", 2));

        var ing = AddIngredients(db, t,
            ("Trà đen", "g", 300), ("Sữa tươi", "ml", 3000), ("Đường", "g", 1000), ("Đá viên", "g", 5000),
            ("Trân châu", "g", 1000), ("Đào ngâm", "g", 500), ("Vải ngâm", "g", 500), ("Chanh", "quả", 10));

        var cats = AddCategories(db, t, "Trà sữa", "Trà trái cây", "Topping");

        // Trùng tên "Trà đào cam sả" với quán A để thử cô lập tenant.
        var products = new List<Product>
        {
            AddProduct(db, t, cats[0], "Trà sữa trân châu", 35_000, ing, ("Trà đen", 6), ("Sữa tươi", 80), ("Trân châu", 50), ("Đường", 20), ("Đá viên", 120)),
            AddProduct(db, t, cats[0], "Trà sữa ít đường", 33_000, ing, ("Trà đen", 6), ("Sữa tươi", 80), ("Đường", 8), ("Đá viên", 120)),
            AddProduct(db, t, cats[1], "Trà đào cam sả", 39_000, ing, ("Trà đen", 6), ("Đào ngâm", 50), ("Đường", 15), ("Đá viên", 150)),
            AddProduct(db, t, cats[1], "Trà vải", 39_000, ing, ("Trà đen", 6), ("Vải ngâm", 50), ("Đường", 15), ("Đá viên", 150)),
            AddProduct(db, t, cats[1], "Trà chanh", 20_000, ing, ("Trà đen", 5), ("Chanh", 0.5m), ("Đường", 20), ("Đá viên", 150)),
            AddProduct(db, t, cats[2], "Trân châu thêm", 8_000, ing, ("Trân châu", 50)),
        };

        AddStocks(db, t, thanhXuan, ing, now, factor: 2);
        await db.SaveChangesAsync();

        accounts.Add(await CreateUserAsync(userManager, "chuquan@tragio.test", "Hoàng Văn Bình", AppRoles.TenantAdmin, tenant, null));
        var staff = new[] { await CreateUserAsync(userManager, "huy@tragio.test", "Đỗ Quang Huy", AppRoles.Staff, tenant, thanhXuan.Id) };
        accounts.AddRange(staff);

        AddOrders(db, t, thanhXuan, "TX", tables, await UserIdsAsync(userManager, staff), products, now, random, ordersPerDay: (6, 12));

        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Hàm dựng dữ liệu

    private static Branch NewBranch(int tenantId, string name, string slug, string address, string phone) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = address, Phone = phone,
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static List<DiningTable> AddZones(AppDbContext db, int tenantId, Branch branch, params (string Name, int Tables)[] zones)
    {
        var tables = new List<DiningTable>();
        var number = 1;
        for (var i = 0; i < zones.Length; i++)
        {
            var zone = new Zone { TenantId = tenantId, Branch = branch, Name = zones[i].Name, SortOrder = i + 1 };
            db.Zones.Add(zone);
            for (var j = 0; j < zones[i].Tables; j++)
            {
                var table = new DiningTable
                {
                    TenantId = tenantId, Zone = zone, Name = $"Bàn {number++:00}",
                    Seats = j % 2 == 0 ? 4 : 2, Status = TableStatus.Empty, IsActive = true
                };
                zone.Tables.Add(table);
                tables.Add(table);
            }
        }
        return tables;
    }

    private static Dictionary<string, Ingredient> AddIngredients(AppDbContext db, int tenantId,
        params (string Name, string Unit, decimal MinStock)[] items)
    {
        var result = new Dictionary<string, Ingredient>();
        foreach (var (name, unit, minStock) in items)
        {
            var ingredient = new Ingredient { TenantId = tenantId, Name = name, Unit = unit, MinStock = minStock };
            db.Ingredients.Add(ingredient);
            result[name] = ingredient;
        }
        return result;
    }

    private static List<Category> AddCategories(AppDbContext db, int tenantId, params string[] names)
    {
        var categories = names
            .Select((name, i) => new Category { TenantId = tenantId, Name = name, SortOrder = i + 1 })
            .ToList();
        db.Categories.AddRange(categories);
        return categories;
    }

    private static Product AddProduct(AppDbContext db, int tenantId, Category category, string name, decimal price,
        Dictionary<string, Ingredient> ingredients, params (string Ingredient, decimal Quantity)[] recipe)
    {
        var product = new Product
        {
            TenantId = tenantId, Category = category, Name = name, Price = price, IsAvailable = true
        };
        foreach (var (ingredientName, quantity) in recipe)
        {
            product.Recipes.Add(new Recipe
            {
                TenantId = tenantId, Ingredient = ingredients[ingredientName], Quantity = quantity
            });
        }
        db.Products.Add(product);
        return product;
    }

    /// <summary>Tồn mặc định = MinStock × factor; overrides đặt tồn cụ thể (dưới mức hoặc âm).</summary>
    private static void AddStocks(AppDbContext db, int tenantId, Branch branch, Dictionary<string, Ingredient> ingredients,
        DateTime now, decimal factor, params (string Ingredient, decimal Quantity)[] overrides)
    {
        foreach (var ingredient in ingredients.Values)
        {
            var quantity = ingredient.MinStock * factor;
            foreach (var (name, value) in overrides)
            {
                if (name == ingredient.Name)
                {
                    quantity = value;
                }
            }
            db.BranchStocks.Add(new BranchStock
            {
                TenantId = tenantId, Branch = branch, Ingredient = ingredient, Quantity = quantity, UpdatedAt = now
            });
        }
    }

    /// <summary>
    /// Hóa đơn 30 ngày gần nhất (kể cả hôm nay tới giờ hiện tại). Khoảng 5% món bị hủy, 8% đơn có giảm giá,
    /// 2% đơn bị hủy cả đơn. Mã hóa đơn dạng HD-CG-261019-087 (chi nhánh – ngày – số thứ tự).
    /// </summary>
    private static void AddOrders(AppDbContext db, int tenantId, Branch branch, string branchCode,
        List<DiningTable> tables, List<string> staffIds, List<Product> products, DateTime now, Random random,
        (int Min, int Max) ordersPerDay)
    {
        for (var daysAgo = 29; daysAgo >= 0; daysAgo--)
        {
            var day = now.Date.AddDays(-daysAgo);
            var count = random.Next(ordersPerDay.Min, ordersPerDay.Max + 1);
            var times = Enumerable.Range(0, count)
                .Select(_ => day.AddHours(7).AddMinutes(random.Next(0, 14 * 60)))
                .OrderBy(x => x)
                .ToList();

            var seq = 0;
            foreach (var createdAt in times)
            {
                var paidAt = createdAt.AddMinutes(random.Next(15, 90));
                if (paidAt > now)
                {
                    continue; // chưa tới giờ này trong hôm nay
                }

                var staffId = staffIds[random.Next(staffIds.Count)];
                var order = new Order
                {
                    TenantId = tenantId, Branch = branch, Table = tables[random.Next(tables.Count)],
                    Code = $"HD-{branchCode}-{day:yyMMdd}-{++seq:000}",
                    GuestCount = random.Next(1, 5), CreatedById = staffId, CreatedAt = createdAt
                };

                foreach (var product in products.OrderBy(_ => random.Next()).Take(random.Next(1, 5)))
                {
                    var item = NewItem(tenantId, product, random.Next(1, 4));
                    if (random.NextDouble() < 0.05)
                    {
                        item.IsCancelled = true;
                        item.CancelReason = "Khách đổi món";
                    }
                    order.Items.Add(item);
                }
                if (order.Items.All(i => i.IsCancelled))
                {
                    order.Items.First().IsCancelled = false;
                    order.Items.First().CancelReason = null;
                }

                order.SubTotal = order.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);

                if (random.NextDouble() < 0.02)
                {
                    order.Status = OrderStatus.Cancelled;
                    order.CancelReason = "Khách rời đi";
                    order.Total = order.SubTotal;
                    db.Orders.Add(order);
                    continue;
                }

                if (random.NextDouble() < 0.08)
                {
                    order.Discount = Math.Round(order.SubTotal * 0.1m / 1000m, MidpointRounding.AwayFromZero) * 1000m;
                    order.DiscountReason = random.Next(2) == 0 ? "Khách quen" : "Khuyến mãi khai trương";
                }
                order.Total = order.SubTotal - order.Discount;
                order.Status = OrderStatus.Paid;
                order.PaidAt = paidAt;
                order.CashierId = staffId;

                if (random.Next(3) == 0)
                {
                    order.PaymentMethod = PaymentMethod.BankTransfer;
                }
                else
                {
                    order.PaymentMethod = PaymentMethod.Cash;
                    order.CashReceived = Math.Ceiling(order.Total / 50_000m) * 50_000m;
                }

                db.Orders.Add(order);
            }
        }
    }

    private static OrderItem NewItem(int tenantId, Product product, int quantity) => new()
    {
        TenantId = tenantId, Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = quantity
    };

    private static async Task<SeedAccount> CreateUserAsync(UserManager<AppUser> userManager, string email, string fullName,
        string role, Tenant? tenant, int? branchId, bool mustChangePassword = false)
    {
        var user = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = fullName,
            TenantId = tenant?.Id, BranchId = branchId, IsActive = true, MustChangePassword = mustChangePassword,
            StartDate = role == AppRoles.Staff ? new DateOnly(2026, 1, 5) : null
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Không tạo được tài khoản {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
        await userManager.AddToRoleAsync(user, role);

        return new SeedAccount(email, role, fullName, tenant?.Name);
    }

    private static async Task<List<string>> UserIdsAsync(UserManager<AppUser> userManager, IEnumerable<SeedAccount> accounts)
    {
        var ids = new List<string>();
        foreach (var account in accounts)
        {
            ids.Add((await userManager.FindByEmailAsync(account.Email))!.Id);
        }
        return ids;
    }
}
