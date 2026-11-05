// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Security.Claims;
using BTL_WEB_NC.Infrastructure.Auth;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;

namespace BTL_WEB_NC.Tests.Account;

/// <summary>R03: còn mật khẩu khởi tạo thì chỉ được vào trang đổi mật khẩu, đăng xuất, trang báo lỗi quyền.</summary>
public class MustChangePasswordMiddlewareTests
{
    [Fact]
    public async Task PhaiDoiMatKhau_VaoManage_ChuyenToiTrangDoiMatKhau()
    {
        var context = CreateContext("/manage", mustChange: true);

        var nextCalled = await Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/tai-khoan/doi-mat-khau", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task PhaiDoiMatKhau_GoiAjax_Tra403()
    {
        var context = CreateContext("/manage/products/list", mustChange: true);
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var nextCalled = await Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/tai-khoan/doi-mat-khau")]
    [InlineData("/dang-xuat")]
    [InlineData("/khong-co-quyen")]
    public async Task PhaiDoiMatKhau_TrangDuocPhep_ChoQua(string path)
    {
        var context = CreateContext(path, mustChange: true);

        Assert.True(await Invoke(context));
    }

    [Fact]
    public async Task KhongPhaiDoiMatKhau_ChoQua()
    {
        var context = CreateContext("/manage", mustChange: false);

        Assert.True(await Invoke(context));
    }

    private static async Task<bool> Invoke(HttpContext context)
    {
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context);
        return nextCalled;
    }

    private static DefaultHttpContext CreateContext(string path, bool mustChange)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        if (mustChange)
        {
            claims.Add(new Claim(TenantClaimTypes.MustChangePassword, "true"));
        }

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
