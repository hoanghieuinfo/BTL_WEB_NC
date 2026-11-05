// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.System.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.System.Controllers;

/// <summary>MH05 – Tổng quan hệ thống Super Admin (UC06, R09).</summary>
public class DashboardController : SystemControllerBase
{
    private readonly ISystemTenantService _tenantService;

    public DashboardController(ISystemTenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await _tenantService.GetDashboardOverviewAsync(ct);
        return View(vm);
    }
}
