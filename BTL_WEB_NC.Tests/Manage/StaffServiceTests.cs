// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Text.Json;
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// MH11 – nhân viên. AppUser không có Global Query Filter nên đây là chỗ dễ lộ dữ liệu nhất:
/// kiểm tra kỹ không thấy/sửa được nhân viên quán khác, không tạo được vai trò khác Staff, chặn vượt MaxStaff (R17).
/// Quán A: gói 3 nhân viên; chi nhánh Cầu Giấy, Hoàn Kiếm (đang hoạt động), Tây Hồ (tạm ngưng).
/// </summary>
public class StaffServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly int _cauGiay;
    private readonly int _hoanKiem;
    private readonly int _tayHo;
    private readonly int _branchB;
    private readonly string _adminA;
    private readonly string _staffA;
    private readonly string _staffB;

    public StaffServiceTests()
    {
        using (var db = _db.CreateContext(tenantId: null))
        {
            var plan = new ServicePlan { Name = "Thử 3", MaxBranches = 5, MaxStaff = 3, PricePerMonth = 0 };
            db.ServicePlans.Add(plan);
            db.Tenants.Single(t => t.Id == TestDb.TenantA).ServicePlan = plan;
            var cg = NewBranch(TestDb.TenantA, "Cầu Giấy", "cau-giay", true);
            var hk = NewBranch(TestDb.TenantA, "Hoàn Kiếm", "hoan-kiem", true);
            var th = NewBranch(TestDb.TenantA, "Tây Hồ", "tay-ho", false);
            var b = NewBranch(TestDb.TenantB, "Thanh Xuân", "thanh-xuan", true);
            db.Branches.AddRange(cg, hk, th, b);
            db.SaveChanges();
            (_cauGiay, _hoanKiem, _tayHo, _branchB) = (cg.Id, hk.Id, th.Id, b.Id);
        }

        using var provider = IdentityTestHost.Create(_db);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }
        _adminA = CreateUser(users, "chuquan@a.test", "Chủ Quán A", TestDb.TenantA, null, AppRoles.TenantAdmin);
        _staffA = CreateUser(users, "nam@a.test", "Trần Văn Nam", TestDb.TenantA, _cauGiay, AppRoles.Staff, phone: "0912345678");
        _staffB = CreateUser(users, "huy@b.test", "Đỗ Quang Huy", TestDb.TenantB, _branchB, AppRoles.Staff);
    }

    // ------------------------------------------------------------------ Cô lập tenant

    [Fact]
    public async Task DanhSach_ChiCoNhanVienCuaQuan_KhongCoChuQuan()
    {
        var result = await WithStaff(TestDb.TenantA, s => s.ListAsync(new StaffListQuery()));

        var row = Assert.Single(result.Items);
        Assert.Equal(_staffA, row.Id);
        Assert.Equal("Cầu Giấy", row.BranchName);
    }

    [Fact]
    public async Task ChuyenKhoaMoKhoa_NhanVienQuanKhac_Tra404_DuLieuBKhongDoi()
    {
        var stampBefore = await StampAsync(_staffB);

        await WithStaff(TestDb.TenantA, async s =>
        {
            await Assert.ThrowsAsync<NotFoundException>(() => s.TransferAsync(_staffB, _hoanKiem));
            await Assert.ThrowsAsync<NotFoundException>(() => s.LockAsync(_staffB));
            await Assert.ThrowsAsync<NotFoundException>(() => s.UnlockAsync(_staffB));
            return 0;
        });

        var b = await UserAsync(_staffB);
        Assert.True(b.IsActive);
        Assert.Equal(_branchB, b.BranchId);
        Assert.Equal(stampBefore, b.SecurityStamp);
    }

    [Fact]
    public async Task KhoaHoacChuyenTaiKhoanChuQuan_Tra404()
    {
        await WithStaff(TestDb.TenantA, async s =>
        {
            await Assert.ThrowsAsync<NotFoundException>(() => s.LockAsync(_adminA));
            await Assert.ThrowsAsync<NotFoundException>(() => s.TransferAsync(_adminA, _hoanKiem));
            return 0;
        });

        Assert.True((await UserAsync(_adminA)).IsActive);
    }

    [Fact]
    public async Task Them_VoiBranchIdCuaQuanKhac_Tra404_KhongTaoTaiKhoan()
    {
        await WithStaff(TestDb.TenantA, s =>
            Assert.ThrowsAsync<NotFoundException>(() => s.CreateAsync(Form("moi@a.test", _branchB))));

        Assert.Null(await FindByEmailAsync("moi@a.test"));
    }

    [Fact]
    public async Task Chuyen_SangBranchIdCuaQuanKhac_Tra404()
    {
        await WithStaff(TestDb.TenantA, s => Assert.ThrowsAsync<NotFoundException>(() => s.TransferAsync(_staffA, _branchB)));

        Assert.Equal(_cauGiay, (await UserAsync(_staffA)).BranchId);
    }

    // ------------------------------------------------------------------ Vai trò và TenantId không lấy từ request

    [Fact]
    public async Task Them_RequestCoTruongRoleVaTenantId_VanChiLaStaffCuaQuanHienTai()
    {
        var json = $$"""
            { "fullName": "Phạm Đức Anh", "email": "anh@a.test", "phone": "0904555777",
              "branchId": {{_cauGiay}}, "startDate": "2026-10-19",
              "role": "TenantAdmin", "roles": ["SuperAdmin"], "tenantId": {{TestDb.TenantB}} }
            """;
        var model = JsonSerializer.Deserialize<StaffFormModel>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var created = await WithStaff(TestDb.TenantA, s => s.CreateAsync(model));

        using var provider = IdentityTestHost.Create(_db);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = (await users.FindByIdAsync(created.Id))!;
        Assert.Equal(new[] { AppRoles.Staff }, await users.GetRolesAsync(user));
        Assert.Equal(TestDb.TenantA, user.TenantId);
        Assert.True(user.MustChangePassword);
        Assert.True(await users.CheckPasswordAsync(user, created.InitialPassword)); // mật khẩu khởi tạo dùng được
    }

    [Fact]
    public void MatKhauKhoiTao_DatChinhSach_VaNgauNhien()
    {
        var passwords = Enumerable.Range(0, 50).Select(_ => StaffService.GenerateInitialPassword()).ToList();

        Assert.Equal(50, passwords.Distinct().Count());
        Assert.All(passwords, p =>
        {
            Assert.Matches(@"^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$", p);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
            Assert.DoesNotContain(p, c => "0O1lI".Contains(c));
        });
    }

    // ------------------------------------------------------------------ Giới hạn gói (R17)

    [Fact]
    public async Task Them_VuotMaxStaff_BiChan_NhanVienDaNghiKhongChiemSuat()
    {
        await WithStaff(TestDb.TenantA, async s =>
        {
            await s.CreateAsync(Form("hai@a.test", _cauGiay));
            await s.CreateAsync(Form("ba@a.test", _hoanKiem)); // 3/3

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => s.CreateAsync(Form("bon@a.test", _cauGiay)));
            Assert.Contains("3/3", ex.Message);

            await s.LockAsync(_staffA); // đã nghỉ → trả lại một suất
            await s.CreateAsync(Form("bon@a.test", _cauGiay));

            // Mở khóa lại khi đã đủ 3/3 → chặn như thêm mới
            await Assert.ThrowsAsync<BusinessRuleException>(() => s.UnlockAsync(_staffA));
            return 0;
        });

        Assert.False((await UserAsync(_staffA)).IsActive);
        Assert.NotNull(await FindByEmailAsync("bon@a.test"));
    }

    // ------------------------------------------------------------------ Quy tắc khác

    [Fact]
    public async Task Them_EmailTrungVoiTaiKhoanQuanKhac_LoiTaiOEmail()
    {
        var ex = await WithStaff(TestDb.TenantA, s =>
            Assert.ThrowsAsync<FieldValidationException>(() => s.CreateAsync(Form("HUY@b.test", _cauGiay))));

        Assert.Equal("email", ex.Field);
        Assert.DoesNotContain("Thanh Xuân", ex.Message); // không lộ thông tin quán khác
    }

    [Fact]
    public async Task ThemHoacChuyen_VaoChiNhanhTamNgung_LoiTaiOChiNhanh()
    {
        await WithStaff(TestDb.TenantA, async s =>
        {
            var ex1 = await Assert.ThrowsAsync<FieldValidationException>(() => s.CreateAsync(Form("moi@a.test", _tayHo)));
            var ex2 = await Assert.ThrowsAsync<FieldValidationException>(() => s.TransferAsync(_staffA, _tayHo));
            Assert.Equal("branchId", ex1.Field);
            Assert.Equal("branchId", ex2.Field);
            return 0;
        });
    }

    [Fact]
    public async Task ChuyenChiNhanh_DoiBranchId_VaSecurityStamp()
    {
        var before = await StampAsync(_staffA);

        await WithStaff(TestDb.TenantA, async s => { await s.TransferAsync(_staffA, _hoanKiem); return 0; });

        var user = await UserAsync(_staffA);
        Assert.Equal(_hoanKiem, user.BranchId);
        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public async Task Chuyen_CungChiNhanhHienTai_LoiTaiO()
    {
        var ex = await WithStaff(TestDb.TenantA, s =>
            Assert.ThrowsAsync<FieldValidationException>(() => s.TransferAsync(_staffA, _cauGiay)));
        Assert.Equal("branchId", ex.Field);
    }

    [Fact]
    public async Task KhoaVaMoKhoa_DoiSecurityStamp_KhongXoaBanGhi()
    {
        var s0 = await StampAsync(_staffA);
        await WithStaff(TestDb.TenantA, async s => { await s.LockAsync(_staffA); return 0; });
        var locked = await UserAsync(_staffA);
        await WithStaff(TestDb.TenantA, async s => { await s.UnlockAsync(_staffA); return 0; });
        var unlocked = await UserAsync(_staffA);

        Assert.False(locked.IsActive);
        Assert.NotEqual(s0, locked.SecurityStamp);
        Assert.True(unlocked.IsActive);
        Assert.NotEqual(locked.SecurityStamp, unlocked.SecurityStamp);
    }

    [Fact]
    public async Task LocTimKiemTrangThai()
    {
        await WithStaff(TestDb.TenantA, async s =>
        {
            await s.CreateAsync(Form("ha@a.test", _hoanKiem, "Lê Thu Hà"));

            Assert.Equal("Lê Thu Hà", Assert.Single((await s.ListAsync(new StaffListQuery { Q = "Hà" })).Items).FullName);
            Assert.Equal(_staffA, Assert.Single((await s.ListAsync(new StaffListQuery { Q = "0912" })).Items).Id);
            Assert.Equal(_staffA, Assert.Single((await s.ListAsync(new StaffListQuery { Q = "nam@" })).Items).Id);
            Assert.Equal("Lê Thu Hà", Assert.Single((await s.ListAsync(new StaffListQuery { BranchId = _hoanKiem })).Items).FullName);
            // Nhân viên vừa tạo chưa đổi mật khẩu → "pending"; Nam đã đổi → "active".
            Assert.Equal("Lê Thu Hà", Assert.Single((await s.ListAsync(new StaffListQuery { Status = "pending" })).Items).FullName);
            Assert.Equal(_staffA, Assert.Single((await s.ListAsync(new StaffListQuery { Status = "active" })).Items).Id);
            Assert.Empty((await s.ListAsync(new StaffListQuery { Status = "locked" })).Items);
            return 0;
        });
    }

    // ------------------------------------------------------------------ Đặt lại mật khẩu, sửa thông tin

    [Fact]
    public async Task DatLaiMatKhau_NhanVienQuanKhac_Tra404_MatKhauBKhongDoi()
    {
        var stampBefore = await StampAsync(_staffB);

        await WithStaff(TestDb.TenantA, s => Assert.ThrowsAsync<NotFoundException>(() => s.ResetPasswordAsync(_staffB)));

        Assert.Equal(stampBefore, await StampAsync(_staffB));
        Assert.True(await CheckPasswordAsync(_staffB, "Cafe@2026"));
    }

    [Fact]
    public async Task DatLaiMatKhau_MatKhauCuHetHieuLuc_MatKhauMoiDung_PhaiDoiLanToi()
    {
        var stampBefore = await StampAsync(_staffA);

        var result = await WithStaff(TestDb.TenantA, s => s.ResetPasswordAsync(_staffA));

        Assert.False(await CheckPasswordAsync(_staffA, "Cafe@2026"));
        Assert.True(await CheckPasswordAsync(_staffA, result.InitialPassword));
        var user = await UserAsync(_staffA);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual(stampBefore, user.SecurityStamp); // phiên đang mở bị thu hồi
        Assert.Equal("Cầu Giấy", result.BranchName);
    }

    [Fact]
    public async Task DatLaiMatKhau_TaiKhoanDangKhoa_BiChan()
    {
        await WithStaff(TestDb.TenantA, async s =>
        {
            await s.LockAsync(_staffA);
            await Assert.ThrowsAsync<BusinessRuleException>(() => s.ResetPasswordAsync(_staffA));
            return 0;
        });

        Assert.True(await CheckPasswordAsync(_staffA, "Cafe@2026"));
    }

    [Fact]
    public async Task DatLaiMatKhau_TaiKhoanChuQuan_Tra404()
    {
        await WithStaff(TestDb.TenantA, s => Assert.ThrowsAsync<NotFoundException>(() => s.ResetPasswordAsync(_adminA)));

        Assert.True(await CheckPasswordAsync(_adminA, "Cafe@2026"));
    }

    [Fact]
    public async Task Sua_ChiDoiHoTenVaSoDienThoai()
    {
        var before = await UserAsync(_staffA);

        await WithStaff(TestDb.TenantA, async s =>
        {
            await s.UpdateAsync(_staffA, new StaffEditModel { FullName = "  Trần Văn Nam B ", Phone = "0987654321" });
            return 0;
        });

        var after = await UserAsync(_staffA);
        Assert.Equal("Trần Văn Nam B", after.FullName);
        Assert.Equal("0987654321", after.PhoneNumber);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.BranchId, after.BranchId);
    }

    [Fact]
    public async Task Sua_NhanVienQuanKhac_Tra404()
    {
        await WithStaff(TestDb.TenantA, s =>
            Assert.ThrowsAsync<NotFoundException>(() => s.UpdateAsync(_staffB, new StaffEditModel { FullName = "Đổi", Phone = "0987654321" })));

        Assert.Equal("Đỗ Quang Huy", (await UserAsync(_staffB)).FullName);
    }

    [Theory]
    [InlineData("A", "0987654321", "FullName")]
    [InlineData("Nguyễn Văn A", "098765432", "Phone")]
    [InlineData("Nguyễn Văn A", "09876543ab", "Phone")]
    public void Sua_ValidateTheoBM08(string name, string phone, string field)
    {
        var model = new StaffEditModel { FullName = name, Phone = phone };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(field));
    }

    // ------------------------------------------------------------------ Hàm phụ

    private async Task<bool> CheckPasswordAsync(string id, string password)
    {
        using var provider = IdentityTestHost.Create(_db);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await users.CheckPasswordAsync((await users.FindByIdAsync(id))!, password);
    }

    /// <summary>Chạy service trong một scope của chủ quán tenantId (giống một request).</summary>
    private async Task<T> WithStaff<T>(int tenantId, Func<IStaffService, Task<T>> action)
    {
        using var provider = IdentityTestHost.Create(_db, tenantId);
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IStaffService>());
    }

    private async Task<AppUser> UserAsync(string id)
    {
        using var db = _db.CreateContext(tenantId: null);
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    private async Task<string?> StampAsync(string id) => (await UserAsync(id)).SecurityStamp;

    private async Task<AppUser?> FindByEmailAsync(string email)
    {
        using var db = _db.CreateContext(tenantId: null);
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
    }

    private static StaffFormModel Form(string email, int branchId, string name = "Nhân Viên Mới") => new()
    {
        FullName = name, Email = email, Phone = "0900000000", BranchId = branchId, StartDate = new DateOnly(2026, 10, 19)
    };

    private static string CreateUser(UserManager<AppUser> users, string email, string name, int tenantId, int? branchId,
        string role, string phone = "0900000001")
    {
        var user = new AppUser
        {
            UserName = email, Email = email, FullName = name, TenantId = tenantId, BranchId = branchId,
            PhoneNumber = phone, IsActive = true, MustChangePassword = false
        };
        Assert.True(users.CreateAsync(user, "Cafe@2026").GetAwaiter().GetResult().Succeeded);
        users.AddToRoleAsync(user, role).GetAwaiter().GetResult();
        return user.Id;
    }

    private static Branch NewBranch(int tenantId, string name, string slug, bool active) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = $"{name}, Hà Nội", IsActive = active,
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0)
    };

    public void Dispose() => _db.Dispose();
}
