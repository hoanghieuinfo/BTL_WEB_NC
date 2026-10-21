// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>MH08 /manage – Tổng quan quán. Render ở server; đổi chi nhánh là tải lại trang với ?branchId=.</summary>
public class DashboardController : ManageControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? branchId, CancellationToken ct) =>
        View(await _dashboard.GetAsync(branchId, ct));
}
