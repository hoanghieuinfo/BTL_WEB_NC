// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Areas.System.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
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
/// MH06 – SystemTenantService (Module 1, D2): đếm nhân viên theo vai trò Staff, chủ quán là TenantAdmin,
/// quy tắc chuyển trạng thái (P01 bước 5, P02), hết hạn và gia hạn gói (P02 bước 3).
/// </summary>
public class SystemTenantServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly SystemTenantService _service;

    public SystemTenantServiceTests()
    {
        _provider = IdentityTestHost.Create(_db);
        _scope = _provider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _service = new SystemTenantService(_context, _userManager);

        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }
    }

    // ---------------------------------------------------------------- Đếm nhân viên, chủ quán

    [Fact]
    public async Task HaGoi_ChuQuanVaNhanVienDaNghiKhongTinh_DuocPhep()
    {
        await AddUserAsync(TestDb.TenantA, AppRoles.TenantAdmin);
        await AddUserAsync(TestDb.TenantA, AppRoles.Staff);
        await AddUserAsync(TestDb.TenantA, AppRoles.Staff);
        await AddUserAsync(TestDb.TenantA, AppRoles.Staff, isActive: false);
        var small = await AddPlanAsync(maxStaff: 2);

        await _service.ChangePlanAsync(TestDb.TenantA, small.Id);

        Assert.Equal(small.Id, (await _context.Tenants.SingleAsync(t => t.Id == TestDb.TenantA)).ServicePlanId);
    }

    [Fact]
    public async Task HaGoi_NhanVienDangLamVuotGioiHan_BiChan()
    {
        await AddUserAsync(TestDb.TenantA, AppRoles.TenantAdmin);
        for (var i = 0; i < 3; i++) await AddUserAsync(TestDb.TenantA, AppRoles.Staff);
        var small = await AddPlanAsync(maxStaff: 2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ChangePlanAsync(TestDb.TenantA, small.Id));
        Assert.Contains("3 nhân viên", ex.Message);
    }

    [Fact]
    public async Task DanhSach_QuyMoChiDemStaff_ChuQuanLaTenantAdmin()
    {
        // Tạo nhân viên trước để chắc chắn không lấy "tài khoản đầu tiên" làm chủ quán.
        await AddUserAsync(TestDb.TenantA, AppRoles.Staff, email: "nv1@a.test");
        await AddUserAsync(TestDb.TenantA, AppRoles.Staff, email: "nv2@a.test");
        await AddUserAsync(TestDb.TenantA, AppRoles.TenantAdmin, email: "chu@a.test");

        var result = await _service.GetPagedTenantsAsync(new TenantFilterModel());
        var a = result.Items.Single(t => t.Id == TestDb.TenantA);

        Assert.Equal(2, a.CurrentStaff);
        Assert.Equal("chu@a.test", a.OwnerEmail);
    }

    // ---------------------------------------------------------------- Chuyển trạng thái

    [Fact]
    public async Task TuChoi_KhongCoLyDo_BiChan_CoLyDoThiLuu()
    {
        await SetTenantAsync(TestDb.TenantA, t => t.Status = TenantStatus.PendingActivation);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Rejected, "  "));

        await _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Rejected, " Thông tin không hợp lệ ");

        var tenant = await ReloadTenantAsync(TestDb.TenantA);
        Assert.Equal(TenantStatus.Rejected, tenant.Status);
        Assert.Equal("Thông tin không hợp lệ", tenant.LockReason);
    }

    [Theory]
    [InlineData(TenantStatus.Active, TenantStatus.Rejected)]          // đã duyệt thì không từ chối
    [InlineData(TenantStatus.Active, TenantStatus.Active)]            // trùng trạng thái
    [InlineData(TenantStatus.Active, TenantStatus.PendingActivation)] // không quay về chờ
    [InlineData(TenantStatus.Rejected, TenantStatus.Active)]          // từ chối là kết thúc
    [InlineData(TenantStatus.Active, (TenantStatus)99)]               // giá trị ngoài enum
    public async Task ChuyenTrangThaiSai_BiChan(TenantStatus from, TenantStatus to)
    {
        await SetTenantAsync(TestDb.TenantA, t => t.Status = from);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateStatusAsync(TestDb.TenantA, to, "lý do"));
        Assert.Equal(from, (await ReloadTenantAsync(TestDb.TenantA)).Status);
    }

    [Fact]
    public async Task MoKhoa_XoaLyDoKhoa()
    {
        await _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Locked, "Chưa thanh toán");
        await _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Active, null);

        var tenant = await ReloadTenantAsync(TestDb.TenantA);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.LockReason);
    }

    [Fact]
    public async Task KhoaQuan_DoiSecurityStampMoiTaiKhoan()
    {
        var owner = await AddUserAsync(TestDb.TenantA, AppRoles.TenantAdmin);
        var staff = await AddUserAsync(TestDb.TenantA, AppRoles.Staff);
        var other = await AddUserAsync(TestDb.TenantB, AppRoles.Staff);
        var before = new[] { owner.SecurityStamp, staff.SecurityStamp, other.SecurityStamp };

        await _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Locked, "Vi phạm điều khoản");

        Assert.NotEqual(before[0], (await _userManager.FindByIdAsync(owner.Id))!.SecurityStamp);
        Assert.NotEqual(before[1], (await _userManager.FindByIdAsync(staff.Id))!.SecurityStamp);
        Assert.Equal(before[2], (await _userManager.FindByIdAsync(other.Id))!.SecurityStamp);
    }

    [Fact]
    public void LyDoQua255KyTu_KhongHopLe()
    {
        var request = new UpdateTenantStatusRequest { Status = TenantStatus.Locked, LockReason = new string('x', 256) };

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true);

        Assert.False(valid);
    }

    // ---------------------------------------------------------------- Hết hạn, gia hạn

    [Fact]
    public async Task LocHetHan_ChiTraQuanCoNgayHetHanTruocHomNay()
    {
        var today = VietnamTime.Now.Date;
        await SetTenantAsync(TestDb.TenantA, t => t.PlanExpiresAt = today.AddDays(-1));
        await SetTenantAsync(TestDb.TenantB, t => t.PlanExpiresAt = today); // hết hạn trong hôm nay: chưa tính

        var result = await _service.GetPagedTenantsAsync(new TenantFilterModel { Expired = true });

        var only = Assert.Single(result.Items);
        Assert.Equal(TestDb.TenantA, only.Id);
        Assert.True(only.IsExpired);
    }

    [Fact]
    public async Task GiaHan_NgayHomNayHoacQuaKhu_BiTuChoi()
    {
        var today = VietnamTime.Now.Date;

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => _service.ExtendExpiryAsync(TestDb.TenantA, today));
        Assert.Equal("planExpiresAt", ex.Field);
        await Assert.ThrowsAsync<FieldValidationException>(() => _service.ExtendExpiryAsync(TestDb.TenantA, today.AddDays(-3)));
    }

    [Fact]
    public async Task GiaHan_NgayHopLe_LuuDungNgay()
    {
        var newDate = VietnamTime.Now.Date.AddDays(30);

        await _service.ExtendExpiryAsync(TestDb.TenantA, newDate.AddHours(15));

        var tenant = await ReloadTenantAsync(TestDb.TenantA);
        Assert.Equal(newDate, tenant.PlanExpiresAt);
        Assert.False((await _service.GetTenantDetailAsync(TestDb.TenantA))!.IsExpired);
    }

    [Fact]
    public async Task GiaHan_QuanChoKichHoat_BiChan()
    {
        await SetTenantAsync(TestDb.TenantA, t => t.Status = TenantStatus.PendingActivation);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ExtendExpiryAsync(TestDb.TenantA, VietnamTime.Now.Date.AddDays(30)));
    }

    [Fact]
    public async Task GiaHan_TenantKhongTonTai_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ExtendExpiryAsync(999, VietnamTime.Now.Date.AddDays(30)));
    }

    // ---------------------------------------------------------------- Đăng ký

    [Fact]
    public async Task DangKy_ChuQuanKhongGanChiNhanh()
    {
        var registration = new TenantRegistrationService(_context, _userManager);
        var planId = (await _context.ServicePlans.FirstAsync()).Id;

        var tenantId = await registration.RegisterAsync(new RegisterTenantViewModel
        {
            ShopName = "Quán Mới", Slug = "quan-moi", FullName = "Chủ Mới", PhoneNumber = "0900000000",
            Email = "chumoi@test.vn", Password = SeedData.DemoPassword, ConfirmPassword = SeedData.DemoPassword,
            ServicePlanId = planId, AgreeTerms = true
        });

        var owner = await _userManager.FindByEmailAsync("chumoi@test.vn");
        Assert.NotNull(owner);
        Assert.Equal(tenantId, owner!.TenantId);
        Assert.Null(owner.BranchId);
        Assert.True(await _userManager.IsInRoleAsync(owner, AppRoles.TenantAdmin));
    }

    // ---------------------------------------------------------------- Hỗ trợ

    private async Task<AppUser> AddUserAsync(int tenantId, string role, bool isActive = true, string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@test.vn";
        var user = new AppUser { UserName = email, Email = email, FullName = email, TenantId = tenantId, IsActive = isActive };
        Assert.True((await _userManager.CreateAsync(user, SeedData.DemoPassword)).Succeeded);
        await _userManager.AddToRoleAsync(user, role);
        return user;
    }

    private async Task<ServicePlan> AddPlanAsync(int maxStaff)
    {
        var plan = new ServicePlan { Name = $"Nhỏ {Guid.NewGuid():N}", MaxBranches = 5, MaxStaff = maxStaff, PricePerMonth = 0 };
        _context.ServicePlans.Add(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    private async Task SetTenantAsync(int tenantId, Action<Tenant> change)
    {
        var tenant = await _context.Tenants.SingleAsync(t => t.Id == tenantId);
        change(tenant);
        await _context.SaveChangesAsync();
    }

    /// <summary>Đọc bằng context mới để chắc chắn giá trị đã xuống CSDL, không phải bản đang theo dõi trong bộ nhớ.</summary>
    private async Task<Tenant> ReloadTenantAsync(int tenantId)
    {
        using var fresh = _db.CreateContext(tenantId: null);
        return await fresh.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _db.Dispose();
    }
}
