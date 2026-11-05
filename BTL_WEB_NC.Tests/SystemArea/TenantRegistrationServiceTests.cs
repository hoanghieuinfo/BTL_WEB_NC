// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Services;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.SystemArea;

/// <summary>
/// UC01 – Đăng ký mở quán (P01 bước 2–4, R01): một transaction tạo tenant Chờ kích hoạt + chi nhánh mặc định + TenantAdmin.
/// Dữ liệu không hợp lệ (slug dành riêng/trùng, email trùng, gói ngừng cung cấp, lỗi tạo tài khoản) thì không tạo bản ghi nào.
/// </summary>
public class TenantRegistrationServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly TenantRegistrationService _service;
    private readonly int _planId;

    public TenantRegistrationServiceTests()
    {
        _provider = IdentityTestHost.Create(_db);
        _scope = _provider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _service = new TenantRegistrationService(_context, _userManager);

        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        roleManager.CreateAsync(new IdentityRole(AppRoles.TenantAdmin)).GetAwaiter().GetResult();
        _planId = _context.ServicePlans.First().Id;
    }

    [Fact]
    public async Task DangKyHopLe_TaoDungMotTenantMotChiNhanhMotChuQuan()
    {
        var tenantId = await _service.RegisterAsync(Model("quan-moi", "chu@moi.vn"));

        using var fresh = _db.CreateContext(tenantId: null);
        var tenant = await fresh.Tenants.SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatus.PendingActivation, tenant.Status);
        Assert.Equal("quan-moi", tenant.Slug);
        Assert.Equal(_planId, tenant.ServicePlanId);

        var branch = Assert.Single(await fresh.Branches.IgnoreQueryFilters().Where(b => b.TenantId == tenantId).ToListAsync());
        Assert.Equal("chi-nhanh-chinh", branch.Slug);

        var users = await fresh.Users.Where(u => u.TenantId == tenantId).ToListAsync();
        var owner = Assert.Single(users);
        Assert.Equal("chu@moi.vn", owner.Email);
        Assert.True(await _userManager.IsInRoleAsync(owner, AppRoles.TenantAdmin));
    }

    [Fact]
    public async Task SlugVaEmail_ChuanHoa_ChuThuongVaBoKhoangTrang()
    {
        var tenantId = await _service.RegisterAsync(Model("  Quan-Hoa  ", "  hoa@moi.vn "));

        using var fresh = _db.CreateContext(tenantId: null);
        Assert.Equal("quan-hoa", (await fresh.Tenants.SingleAsync(t => t.Id == tenantId)).Slug);
        Assert.NotNull(await _userManager.FindByEmailAsync("hoa@moi.vn"));
    }

    [Theory]
    [InlineData("system")]
    [InlineData("manage")]
    [InlineData("dang-nhap")]
    [InlineData("SITEMAP.XML")]
    public async Task SlugDanhRieng_BiTuChoi_KhongTaoGi(string slug)
    {
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.RegisterAsync(Model(slug, "a@moi.vn")));

        Assert.Equal("slug", ex.Field);
        await AssertNothingCreatedAsync();
    }

    [Fact]
    public async Task SlugTrung_BiTuChoi_KhongTaoGi()
    {
        // "quan-a" là slug của TestDb.TenantA.
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.RegisterAsync(Model("quan-a", "a@moi.vn")));

        Assert.Equal("slug", ex.Field);
        await AssertNothingCreatedAsync();
    }

    [Fact]
    public async Task EmailTrung_KhongPhanBietHoaThuong_BiTuChoi_KhongTaoGi()
    {
        await _service.RegisterAsync(Model("quan-dau", "trung@moi.vn"));
        var before = await CountsAsync();

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.RegisterAsync(Model("quan-sau", "TRUNG@moi.vn")));

        Assert.Equal("email", ex.Field);
        Assert.Equal(before, await CountsAsync());
    }

    [Fact]
    public async Task GoiNgungCungCap_BiTuChoi()
    {
        var plan = await _context.ServicePlans.SingleAsync(p => p.Id == _planId);
        plan.IsActive = false;
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.RegisterAsync(Model("quan-moi", "a@moi.vn")));

        Assert.Equal("servicePlanId", ex.Field);
        await AssertNothingCreatedAsync();
    }

    [Fact]
    public async Task TaoTaiKhoanLoi_RollbackCaTenantVaChiNhanh()
    {
        // Tenant và chi nhánh đã SaveChanges trước khi tạo tài khoản; mật khẩu yếu làm CreateAsync thất bại
        // → transaction rollback, không còn tenant "mồ côi" không có chủ.
        var model = Model("quan-loi", "loi@moi.vn");
        model.Password = model.ConfirmPassword = "abc";

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.RegisterAsync(model));

        Assert.Equal("password", ex.Field);
        using var fresh = _db.CreateContext(tenantId: null);
        Assert.False(await fresh.Tenants.AnyAsync(t => t.Slug == "quan-loi"));
        Assert.Equal(0, await fresh.Branches.IgnoreQueryFilters().CountAsync());
        Assert.Null(await _userManager.FindByEmailAsync("loi@moi.vn"));
    }

    [Fact]
    public async Task KiemTraEmailVaSlug_A01_A02()
    {
        await _service.RegisterAsync(Model("quan-co-roi", "co@moi.vn"));

        Assert.True(await _service.IsEmailRegisteredAsync(" CO@moi.vn "));
        Assert.False(await _service.IsEmailRegisteredAsync("chua@moi.vn"));
        Assert.True(await _service.IsSlugTakenAsync("Quan-Co-Roi"));
        Assert.True(await _service.IsSlugTakenAsync("pos"));      // dành riêng cũng coi là đã dùng
        Assert.False(await _service.IsSlugTakenAsync("quan-trong"));
    }

    private RegisterTenantViewModel Model(string slug, string email) => new()
    {
        ShopName = "Quán Thử", Slug = slug, FullName = "Chủ Thử", PhoneNumber = "0900000000",
        Email = email, Password = SeedData.DemoPassword, ConfirmPassword = SeedData.DemoPassword,
        ServicePlanId = _planId, AgreeTerms = true
    };

    private async Task<(int Tenants, int Branches, int Users)> CountsAsync()
    {
        using var fresh = _db.CreateContext(tenantId: null);
        return (await fresh.Tenants.CountAsync(), await fresh.Branches.IgnoreQueryFilters().CountAsync(), await fresh.Users.CountAsync());
    }

    /// <summary>TestDb có sẵn 2 tenant, chưa có chi nhánh và tài khoản nào.</summary>
    private async Task AssertNothingCreatedAsync() => Assert.Equal((2, 0, 0), await CountsAsync());

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _db.Dispose();
    }
}
