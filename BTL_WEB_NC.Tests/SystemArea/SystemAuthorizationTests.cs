// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Reflection;
using System.Security.Claims;
using BTL_WEB_NC.Areas.System.Controllers;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.SystemArea;

/// <summary>
/// R04, Bảng 3.19: Area System chỉ cho Super Admin. Dựng đúng policy từ [Authorize] trên controller rồi chạy
/// IAuthorizationService như middleware Authorization: tài khoản đã đăng nhập nhưng sai vai trò → không đạt → 403.
/// </summary>
public class SystemAuthorizationTests
{
    private static readonly Type[] SystemControllers = typeof(SystemControllerBase).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SystemControllerBase)) && !t.IsAbstract)
        .ToArray();

    [Fact]
    public void MoiControllerCuaAreaSystem_KeThuaControllerGoc()
    {
        // Dashboard, Tenants, Plans – nếu thêm controller mới mà quên kế thừa, các test dưới không bảo vệ nó.
        Assert.Equal(3, SystemControllers.Length);
        Assert.All(SystemControllers, t => Assert.Equal("BTL_WEB_NC.Areas.System.Controllers", t.Namespace));
    }

    [Fact]
    public void KhongCoActionNaoAllowAnonymous()
    {
        foreach (var controller in SystemControllers)
        {
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }
    }

    [Theory]
    [InlineData(AppRoles.TenantAdmin, false)]
    [InlineData(AppRoles.Staff, false)]
    [InlineData(AppRoles.SuperAdmin, true)]
    public async Task ChiSuperAdminDuocVao(string role, bool allowed)
    {
        foreach (var controller in SystemControllers)
        {
            var result = await AuthorizeAsync(controller, User(role));
            Assert.True(allowed == result, $"{controller.Name} với vai trò {role}: mong đợi {(allowed ? "được" : "bị chặn")}");
        }
    }

    [Fact]
    public async Task ChuaDangNhap_KhongDat()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await AuthorizeAsync(typeof(TenantsController), anonymous));
    }

    [Fact]
    public void ControllerCoArea_System()
    {
        Assert.All(SystemControllers, t => Assert.Equal("System", t.GetCustomAttribute<AreaAttribute>(inherit: true)?.RouteValue));
    }

    private static async Task<bool> AuthorizeAsync(Type controller, ClaimsPrincipal user)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorizeData = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
        Assert.NotNull(policy);

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy!);
        return result.Succeeded;
    }

    private static ClaimsPrincipal User(string role) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "u1"),
        new Claim(ClaimTypes.Role, role)
    }, "Test"));
}
