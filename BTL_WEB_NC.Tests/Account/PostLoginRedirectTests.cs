// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Auth;
using BTL_WEB_NC.Infrastructure.Tenancy;

namespace BTL_WEB_NC.Tests.Account;

/// <summary>Trang đích sau đăng nhập (R02) – đổi mật khẩu khởi tạo phải được xét trước returnUrl (R03).</summary>
public class PostLoginRedirectTests
{
    [Fact]
    public void PhaiDoiMatKhau_CoReturnUrl_VanVeTrangDoiMatKhau()
    {
        var target = PostLoginRedirect.Resolve(true, "/manage/products", new[] { AppRoles.TenantAdmin });

        Assert.Equal("/tai-khoan/doi-mat-khau", target);
    }

    [Fact]
    public void KhongPhaiDoiMatKhau_CoReturnUrl_DiTheoReturnUrl()
    {
        var target = PostLoginRedirect.Resolve(false, "/manage/products", new[] { AppRoles.TenantAdmin });

        Assert.Equal("/manage/products", target);
    }

    [Theory]
    [InlineData(AppRoles.SuperAdmin, "/system")]
    [InlineData(AppRoles.TenantAdmin, "/manage")]
    [InlineData(AppRoles.Staff, "/pos")]
    public void KhongCoReturnUrl_VeTrangTheoVaiTro(string role, string expected)
    {
        Assert.Equal(expected, PostLoginRedirect.Resolve(false, null, new[] { role }));
    }

    [Fact]
    public void KhongCoVaiTro_VeTrangChu()
    {
        Assert.Equal("/", PostLoginRedirect.Resolve(false, null, Array.Empty<string>()));
    }
}
