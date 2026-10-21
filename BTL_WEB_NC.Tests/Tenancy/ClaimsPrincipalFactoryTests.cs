// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Tenancy;

/// <summary>Claim tenant_id, branch_id sinh từ CSDL – cũng là đường SecurityStampValidator dựng lại cookie.</summary>
public class ClaimsPrincipalFactoryTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task SinhClaimTenantVaChiNhanh()
    {
        int branchId;
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var branch = new Branch
            {
                Name = "Cầu Giấy", Slug = "cau-giay", Address = "Hà Nội",
                OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0)
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            branchId = branch.Id;
        }

        using var provider = IdentityTestHost.Create(_db);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole(AppRoles.Staff));

        var user = new AppUser
        {
            UserName = "nv@a.test", Email = "nv@a.test", FullName = "Nhân Viên A",
            TenantId = TestDb.TenantA, BranchId = branchId
        };
        Assert.True((await userManager.CreateAsync(user, SeedData.DemoPassword)).Succeeded);
        await userManager.AddToRoleAsync(user, AppRoles.Staff);

        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
        var principal = await factory.CreateAsync(user);

        Assert.Equal(TestDb.TenantA.ToString(), principal.FindFirst(TenantClaimTypes.TenantId)?.Value);
        Assert.Equal(branchId.ToString(), principal.FindFirst(TenantClaimTypes.BranchId)?.Value);
        Assert.Equal("Nhân Viên A", principal.FindFirst(TenantClaimTypes.FullName)?.Value);
        Assert.True(principal.IsInRole(AppRoles.Staff));
    }

    [Fact]
    public async Task SuperAdmin_KhongCoClaimTenant()
    {
        using var provider = IdentityTestHost.Create(_db);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser { UserName = "sa@test", Email = "sa@test", FullName = "Quản trị" };
        Assert.True((await userManager.CreateAsync(user, SeedData.DemoPassword)).Succeeded);

        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
        var principal = await factory.CreateAsync(user);

        Assert.Null(principal.FindFirst(TenantClaimTypes.TenantId));
        Assert.Null(principal.FindFirst(TenantClaimTypes.BranchId));
    }

    public void Dispose() => _db.Dispose();
}
