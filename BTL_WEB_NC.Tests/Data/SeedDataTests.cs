// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Data;

/// <summary>
/// Dữ liệu mẫu đúng yêu cầu Bước 1b và không vi phạm quy tắc tenant/giới hạn gói.
/// Seed một lần cho cả lớp (IClassFixture) vì băm mật khẩu tốn thời gian.
/// Lưu ý: SQLite không so sánh/cộng decimal chuẩn → các phép tính số tiền làm trong bộ nhớ.
/// </summary>
public class SeedDataTests : IClassFixture<SeedDataTests.SeededDb>
{
    private static readonly DateTime Now = new(2026, 10, 21, 15, 30, 0);

    private readonly SeededDb _fixture;

    public SeedDataTests(SeededDb fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void SoChiNhanhVaNhanVien_KhongVuotGoi()
    {
        using var db = _fixture.Db.CreateContext(tenantId: null);
        var tenants = db.Tenants.Include(t => t.ServicePlan).ToList();
        var branches = db.Branches.IgnoreQueryFilters().ToList();
        var staffRoleId = db.Roles.Single(r => r.Name == "Staff").Id;
        var staffUserIds = db.UserRoles.Where(ur => ur.RoleId == staffRoleId).Select(ur => ur.UserId).ToList();
        var staff = db.Users.Where(u => staffUserIds.Contains(u.Id)).ToList();

        Assert.Equal(2, tenants.Count);
        foreach (var tenant in tenants)
        {
            Assert.True(branches.Count(b => b.TenantId == tenant.Id) <= tenant.ServicePlan.MaxBranches);
            Assert.True(staff.Count(u => u.TenantId == tenant.Id) <= tenant.ServicePlan.MaxStaff);
        }

        var chain = tenants.Single(t => t.ServicePlan.Name == "Chuỗi");
        var basic = tenants.Single(t => t.ServicePlan.Name == "Cơ bản");
        Assert.Equal(2, branches.Count(b => b.TenantId == chain.Id));
        Assert.Equal(1, branches.Count(b => b.TenantId == basic.Id)); // đủ giới hạn 1/1
    }

    [Fact]
    public void MoiTenantCoMotChuQuan_NhanVienCoChiNhanhCuaTenantMinh()
    {
        using var db = _fixture.Db.CreateContext(tenantId: null);
        var branches = db.Branches.IgnoreQueryFilters().ToList();
        var adminRoleId = db.Roles.Single(r => r.Name == "TenantAdmin").Id;
        var adminIds = db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToList();
        var users = db.Users.ToList();

        foreach (var tenant in db.Tenants.ToList())
        {
            Assert.Single(users, u => u.TenantId == tenant.Id && adminIds.Contains(u.Id));
        }
        foreach (var user in users.Where(u => u.BranchId.HasValue))
        {
            Assert.Equal(user.TenantId, branches.Single(b => b.Id == user.BranchId).TenantId);
        }
    }

    [Fact]
    public void CoMonChuaDinhLuong_VaTonDuoiMuc_VaTonAm()
    {
        using var db = _fixture.Db.CreateContext(tenantId: null);
        var products = db.Products.IgnoreQueryFilters().Include(p => p.Recipes).ToList();
        var stocks = db.BranchStocks.IgnoreQueryFilters().Include(s => s.Ingredient).ToList();

        Assert.Contains(products, p => p.Name == "Matcha latte" && p.Recipes.Count == 0);
        Assert.Contains(stocks, s => s.Quantity >= 0 && s.Quantity < s.Ingredient.MinStock);
        Assert.Contains(stocks, s => s.Quantity < 0);
    }

    [Fact]
    public void HoaDon_CoHuyMon_CoGiamGia_CoMotDonDangPhucVu()
    {
        using var db = _fixture.Db.CreateContext(tenantId: null);
        var orders = db.Orders.IgnoreQueryFilters().Include(o => o.Items).ToList();

        var serving = Assert.Single(orders, o => o.Status == OrderStatus.Serving);
        Assert.Null(serving.PaidAt);
        Assert.Equal(TableStatus.Serving, db.DiningTables.IgnoreQueryFilters().Single(t => t.Id == serving.TableId).Status);

        var paid = orders.Where(o => o.Status == OrderStatus.Paid).ToList();
        Assert.True(paid.Count > 300);
        Assert.Contains(paid, o => o.Items.Any(i => i.IsCancelled));
        Assert.Contains(paid, o => o.Discount > 0 && !string.IsNullOrEmpty(o.DiscountReason));

        // Rải đủ 30 ngày và trên mọi chi nhánh; không có hóa đơn ở tương lai.
        Assert.Equal(30, paid.Select(o => o.PaidAt!.Value.Date).Distinct().Count());
        Assert.Equal(3, paid.Select(o => o.BranchId).Distinct().Count());
        Assert.All(paid, o => Assert.True(o.PaidAt <= Now));

        // Tổng tiền đúng: SubTotal = tổng món không hủy, Total = SubTotal − Discount.
        Assert.All(paid, o =>
        {
            Assert.Equal(o.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity), o.SubTotal);
            Assert.Equal(o.SubTotal - o.Discount, o.Total);
        });
    }

    [Fact]
    public void MoiBanGhiCon_CungTenantVoiBanGhiCha()
    {
        using var db = _fixture.Db.CreateContext(tenantId: null);

        var itemMismatch = db.OrderItems.IgnoreQueryFilters()
            .Count(i => i.TenantId != i.Order.TenantId || i.TenantId != i.Product.TenantId);
        var orderMismatch = db.Orders.IgnoreQueryFilters()
            .Count(o => o.TenantId != o.Branch.TenantId || o.TenantId != o.Table.TenantId);
        var recipeMismatch = db.Recipes.IgnoreQueryFilters()
            .Count(r => r.TenantId != r.Product.TenantId || r.TenantId != r.Ingredient.TenantId);
        var productMismatch = db.Products.IgnoreQueryFilters()
            .Count(p => p.TenantId != p.Category.TenantId);
        var tableMismatch = db.DiningTables.IgnoreQueryFilters()
            .Count(t => t.TenantId != t.Zone.TenantId || t.Zone.TenantId != t.Zone.Branch.TenantId);
        var stockMismatch = db.BranchStocks.IgnoreQueryFilters()
            .Count(s => s.TenantId != s.Branch.TenantId || s.TenantId != s.Ingredient.TenantId);

        Assert.Equal(0, itemMismatch + orderMismatch + recipeMismatch + productMismatch + tableMismatch + stockMismatch);
    }

    [Fact]
    public async Task TenantB_KhongThayDuLieuCuaA()
    {
        int tenantB;
        using (var lookup = _fixture.Db.CreateContext(tenantId: null))
        {
            tenantB = lookup.Tenants.Single(t => t.Slug == "tra-gio").Id;
        }

        using var db = _fixture.Db.CreateContext(tenantB);
        var branchNames = await db.Branches.Select(b => b.Name).ToListAsync();
        var trungTen = await db.Products.CountAsync(p => p.Name == "Trà đào cam sả");

        Assert.Equal(new[] { "Thanh Xuân" }, branchNames);
        Assert.Equal(1, trungTen); // chỉ món của B dù A cũng có món cùng tên
        Assert.All(await db.Orders.ToListAsync(), o => Assert.Equal(tenantB, o.TenantId));
    }

    /// <summary>CSDL được seed một lần cho mọi test trong lớp.</summary>
    public sealed class SeededDb : IAsyncLifetime
    {
        public TestDb Db { get; } = new(seedTenants: false);

        public async Task InitializeAsync()
        {
            using var provider = IdentityTestHost.Create(Db);
            using var scope = provider.CreateScope();
            await SeedData.SeedAsync(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                Now);
        }

        public Task DisposeAsync()
        {
            Db.Dispose();
            return Task.CompletedTask;
        }
    }
}
