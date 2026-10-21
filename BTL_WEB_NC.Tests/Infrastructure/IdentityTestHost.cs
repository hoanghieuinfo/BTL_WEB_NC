// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.Infrastructure;

/// <summary>DI tối thiểu cho UserManager/RoleManager trên CSDL SQLite của TestDb (không có tenant hiện tại).</summary>
public static class IdentityTestHost
{
    public static ServiceProvider Create(TestDb testDb)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<AppDbContext>(_ => testDb.CreateContext(tenantId: null));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();
        return services.BuildServiceProvider();
    }
}
