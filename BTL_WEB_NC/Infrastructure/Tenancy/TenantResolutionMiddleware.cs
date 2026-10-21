// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
using System.Globalization;
using System.Security.Claims;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>
/// Chạy sau UseAuthentication, trước UseAuthorization (mục 3.2.1).
/// Gán TenantContext từ claim phía server; tenant không còn Hoạt động thì đăng xuất (Bảng 3.22, R06).
/// </summary>
public class TenantResolutionMiddleware
{
    private static readonly TimeSpan StatusCacheDuration = TimeSpan.FromSeconds(60);

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenant, AppDbContext db, IMemoryCache cache)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            tenant.TenantId = ReadInt(user, TenantClaimTypes.TenantId);
            tenant.BranchId = ReadInt(user, TenantClaimTypes.BranchId);
            tenant.Role = user.FindFirstValue(ClaimTypes.Role);

            if (tenant.TenantId is int tenantId && !await IsTenantActiveAsync(tenantId, db, cache))
            {
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                tenant.TenantId = null;
                tenant.BranchId = null;
                tenant.Role = null;

                if (context.Request.IsAjaxRequest())
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(
                        ApiResult.Fail("Quán đã bị tạm ngưng. Vui lòng đăng nhập lại."));
                }
                else
                {
                    context.Response.Redirect("/dang-nhap?locked=1");
                }
                return;
            }
        }

        await _next(context);
    }

    private static int? ReadInt(ClaimsPrincipal user, string claimType) =>
        int.TryParse(user.FindFirstValue(claimType), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Trạng thái tenant cache 60 giây để không truy vấn CSDL ở mọi request.</summary>
    private static async Task<bool> IsTenantActiveAsync(int tenantId, AppDbContext db, IMemoryCache cache)
    {
        return await cache.GetOrCreateAsync($"tenant-status:{tenantId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = StatusCacheDuration;
            // Tenants không phải ITenantEntity nên không bị Global Query Filter.
            var status = await db.Tenants
                .Where(t => t.Id == tenantId)
                .Select(t => (TenantStatus?)t.Status)
                .FirstOrDefaultAsync();
            return status == TenantStatus.Active;
        });
    }
}
