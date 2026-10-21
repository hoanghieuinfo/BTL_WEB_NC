// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Mức dùng gói (R11, R17) theo quy tắc đếm ở CLAUDE.md mục 6.</summary>
public class PlanUsageServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task DemChiNhanhKeCaTamNgung_ChiDemNhanVienDangLam_VaKhongLanTenantKhac()
    {
        using (var dbA = _db.CreateContext(TestDb.TenantA))
        {
            dbA.Branches.AddRange(NewBranch("Cầu Giấy", "cau-giay", isActive: true), NewBranch("Hoàn Kiếm", "hoan-kiem", isActive: false));
            await dbA.SaveChangesAsync();
        }
        using (var dbB = _db.CreateContext(TestDb.TenantB))
        {
            dbB.Branches.Add(NewBranch("Thanh Xuân", "thanh-xuan", isActive: true));
            await dbB.SaveChangesAsync();
        }

        using (var provider = IdentityTestHost.Create(_db))
        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole(AppRoles.Staff));
            await CreateStaffAsync(userManager, "dang.lam@a.test", TestDb.TenantA, isActive: true);
            await CreateStaffAsync(userManager, "da.nghi@a.test", TestDb.TenantA, isActive: false);
            await CreateStaffAsync(userManager, "nv@b.test", TestDb.TenantB, isActive: true);
        }

        using var db = _db.CreateContext(TestDb.TenantA);
        var usage = await new PlanUsageService(db, new TenantContext { TenantId = TestDb.TenantA }).GetAsync();

        Assert.Equal(2, usage.BranchCount); // chi nhánh tạm ngưng vẫn chiếm suất
        Assert.Equal(1, usage.StaffCount);  // nhân viên đã nghỉ không tính; nhân viên tenant B không lẫn vào
        Assert.Equal("Quán A", usage.TenantName);
    }

    private static Branch NewBranch(string name, string slug, bool isActive) => new()
    {
        Name = name, Slug = slug, Address = "Hà Nội", IsActive = isActive,
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0)
    };

    private static async Task CreateStaffAsync(UserManager<AppUser> userManager, string email, int tenantId, bool isActive)
    {
        var user = new AppUser { UserName = email, Email = email, FullName = email, TenantId = tenantId, IsActive = isActive };
        Assert.True((await userManager.CreateAsync(user, "Cafe@2026")).Succeeded);
        await userManager.AddToRoleAsync(user, AppRoles.Staff);
    }

    public void Dispose() => _db.Dispose();
}
