// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Security.Claims;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Tenancy;

/// <summary>TenantResolutionMiddleware: gán TenantContext từ claim; tenant bị khóa thì đăng xuất (Bảng 3.22).</summary>
public class TenantResolutionMiddlewareTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task DocClaim_GanTenantContext_VaChoQua()
    {
        var (context, auth) = CreateHttpContext(TestDb.TenantA, branchId: 7, AppRoles.Staff);
        var tenant = new TenantContext();
        var nextCalled = false;

        await Invoke(context, tenant, () => nextCalled = true);

        Assert.True(nextCalled);
        Assert.Equal(TestDb.TenantA, tenant.TenantId);
        Assert.Equal(7, tenant.BranchId);
        Assert.Equal(AppRoles.Staff, tenant.Role);
        Assert.False(auth.SignedOut);
    }

    [Fact]
    public async Task ChuaDangNhap_TenantContextRong()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var tenant = new TenantContext();
        var nextCalled = false;

        await Invoke(context, tenant, () => nextCalled = true);

        Assert.True(nextCalled);
        Assert.Null(tenant.TenantId);
    }

    [Fact]
    public async Task TenantBiKhoa_DangXuat_VaChuyenVeDangNhap()
    {
        LockTenant(TestDb.TenantA);
        var (context, auth) = CreateHttpContext(TestDb.TenantA, branchId: null, AppRoles.TenantAdmin);
        var tenant = new TenantContext();
        var nextCalled = false;

        await Invoke(context, tenant, () => nextCalled = true);

        Assert.False(nextCalled);
        Assert.True(auth.SignedOut);
        Assert.Null(tenant.TenantId);
        Assert.Equal("/dang-nhap?locked=1", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task TenantBiKhoa_RequestAjax_Tra401()
    {
        LockTenant(TestDb.TenantA);
        var (context, auth) = CreateHttpContext(TestDb.TenantA, branchId: null, AppRoles.TenantAdmin);
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        await Invoke(context, new TenantContext(), () => { });

        Assert.True(auth.SignedOut);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private async Task Invoke(HttpContext context, TenantContext tenant, Action onNext)
    {
        var middleware = new TenantResolutionMiddleware(_ => { onNext(); return Task.CompletedTask; });
        using var db = _db.CreateContext(tenantId: null);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await middleware.InvokeAsync(context, tenant, db, cache);
    }

    private void LockTenant(int tenantId)
    {
        using var db = _db.CreateContext(tenantId: null);
        db.Tenants.Single(t => t.Id == tenantId).Status = TenantStatus.Locked;
        db.SaveChanges();
    }

    private static (DefaultHttpContext Context, FakeAuthenticationService Auth) CreateHttpContext(
        int tenantId, int? branchId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Role, role),
            new(TenantClaimTypes.TenantId, tenantId.ToString()),
        };
        if (branchId.HasValue)
        {
            claims.Add(new Claim(TenantClaimTypes.BranchId, branchId.Value.ToString()));
        }

        var auth = new FakeAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RequestServices = services
        };
        context.Response.Body = new MemoryStream();
        return (context, auth);
    }

    public void Dispose() => _db.Dispose();

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public bool SignedOut { get; private set; }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
