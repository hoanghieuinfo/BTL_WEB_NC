// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Infrastructure;

/// <summary>
/// DI tối thiểu cho UserManager/RoleManager trên CSDL SQLite của TestDb.
/// tenantId = null: như lúc seed/đăng nhập (chưa có tenant). Có tenantId: như request của chủ quán đó –
/// UserManager và các service Manage dùng CHUNG một AppDbContext trong scope (giống ứng dụng thật).
/// </summary>
public static class IdentityTestHost
{
    public static ServiceProvider Create(TestDb testDb, int? tenantId = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new TenantContext { TenantId = tenantId });
        services.AddScoped<AppDbContext>(_ => testDb.CreateContext(tenantId));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();
        services.AddScoped<IPlanUsageService, PlanUsageService>();
        services.AddScoped<IStaffService, StaffService>();
        return services.BuildServiceProvider();
    }
}
