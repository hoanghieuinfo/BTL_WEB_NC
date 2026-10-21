// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
using System.Globalization;
using System.Security.Claims;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>
/// Sinh claim tenant_id, branch_id, full_name cho cookie. Đặt ở factory (không phải trong action đăng nhập)
/// vì SecurityStampValidator dựng lại cookie bằng chính factory này mỗi 5 phút: claim luôn lấy mới từ CSDL,
/// nên chuyển chi nhánh nhân viên có hiệu lực ở lần kiểm tra kế tiếp.
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(TenantClaimTypes.FullName, user.FullName));
        if (user.TenantId.HasValue)
        {
            identity.AddClaim(new Claim(TenantClaimTypes.TenantId,
                user.TenantId.Value.ToString(CultureInfo.InvariantCulture)));
        }
        if (user.BranchId.HasValue)
        {
            identity.AddClaim(new Claim(TenantClaimTypes.BranchId,
                user.BranchId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return identity;
    }
}
