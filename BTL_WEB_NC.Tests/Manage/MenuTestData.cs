// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Files;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// Dữ liệu thực đơn tối thiểu cho hai tenant:
/// - Quán A: danh mục "Cà phê"; món "Bạc xỉu" (có định lượng, đã bán trong 1 hóa đơn), "Matcha latte" (chưa bán, chưa định lượng);
///   2 nguyên liệu.
/// - Quán B: danh mục "Trà"; món "Trà đào" có định lượng; 1 nguyên liệu.
/// </summary>
internal sealed record MenuIds(
    int CategoryA, int CategoryB, int SoldProductA, int UnsoldProductA, int ProductB,
    int IngredientA1, int IngredientA2, int IngredientB);

internal static class MenuTestData
{
    public static MenuIds Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null); // seed: gán TenantId tường minh

        var catA = new Category { TenantId = TestDb.TenantA, Name = "Cà phê", SortOrder = 1 };
        var catB = new Category { TenantId = TestDb.TenantB, Name = "Trà", SortOrder = 1 };
        var ingA1 = new Ingredient { TenantId = TestDb.TenantA, Name = "Cà phê bột", Unit = "g", MinStock = 100 };
        var ingA2 = new Ingredient { TenantId = TestDb.TenantA, Name = "Sữa đặc", Unit = "ml", MinStock = 100 };
        var ingB = new Ingredient { TenantId = TestDb.TenantB, Name = "Trà đen", Unit = "g", MinStock = 100 };

        var sold = new Product { TenantId = TestDb.TenantA, Category = catA, Name = "Bạc xỉu", Price = 32_000, IsAvailable = true };
        sold.Recipes.Add(new Recipe { TenantId = TestDb.TenantA, Ingredient = ingA1, Quantity = 15 });
        var unsold = new Product { TenantId = TestDb.TenantA, Category = catA, Name = "Matcha latte", Price = 49_000, IsAvailable = false };
        var productB = new Product { TenantId = TestDb.TenantB, Category = catB, Name = "Trà đào", Price = 39_000, IsAvailable = true };
        productB.Recipes.Add(new Recipe { TenantId = TestDb.TenantB, Ingredient = ingB, Quantity = 8 });

        // Một hóa đơn của quán A có "Bạc xỉu" → món này đã phát sinh giao dịch.
        var staff = new AppUser
        {
            Id = "staff-a", UserName = "nv@a.test", NormalizedUserName = "NV@A.TEST", Email = "nv@a.test",
            NormalizedEmail = "NV@A.TEST", FullName = "Nhân viên A", TenantId = TestDb.TenantA, IsActive = true
        };
        var branch = new Branch
        {
            TenantId = TestDb.TenantA, Name = "Cầu Giấy", Slug = "cau-giay", Address = "Hà Nội",
            OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0)
        };
        var zone = new Zone { TenantId = TestDb.TenantA, Branch = branch, Name = "Tầng 1", SortOrder = 1 };
        var table = new DiningTable { TenantId = TestDb.TenantA, Zone = zone, Name = "Bàn 01", IsActive = true };
        var order = new Order
        {
            TenantId = TestDb.TenantA, Branch = branch, Table = table, Code = "HD-CG-261021-001", Status = OrderStatus.Paid,
            CreatedById = staff.Id, CreatedAt = new DateTime(2026, 10, 21, 9, 0, 0), PaidAt = new DateTime(2026, 10, 21, 9, 30, 0),
            SubTotal = 32_000, Total = 32_000, PaymentMethod = PaymentMethod.Cash, CashReceived = 50_000, CashierId = staff.Id
        };
        order.Items.Add(new OrderItem { TenantId = TestDb.TenantA, Product = sold, ProductName = "Bạc xỉu", UnitPrice = 32_000, Quantity = 1 });

        db.Users.Add(staff);
        db.AddRange(catA, catB, ingA1, ingA2, ingB, sold, unsold, productB, order);
        db.SaveChanges();

        return new MenuIds(catA.Id, catB.Id, sold.Id, unsold.Id, productB.Id, ingA1.Id, ingA2.Id, ingB.Id);
    }

    public static ProductService ProductService(AppDbContext db, int tenantId, IImageUploadService? images = null) =>
        new(db, new TenantContext { TenantId = tenantId }, images ?? new FakeImageUploadService());
}

/// <summary>Thay ImageUploadService trong test service: không ghi đĩa, ghi lại các lần gọi.</summary>
internal sealed class FakeImageUploadService : IImageUploadService
{
    public string? Error { get; set; }
    public List<string> Saved { get; } = new();
    public List<string?> Deleted { get; } = new();

    public Task<ImageUploadResult> SaveAsync(IFormFile file, int tenantId, CancellationToken ct = default)
    {
        if (Error is not null)
        {
            return Task.FromResult(new ImageUploadResult(null, Error));
        }
        var path = $"/uploads/{tenantId}/{Guid.NewGuid():N}.jpg";
        Saved.Add(path);
        return Task.FromResult(new ImageUploadResult(path, null));
    }

    public void Delete(string? path) => Deleted.Add(path);
}
