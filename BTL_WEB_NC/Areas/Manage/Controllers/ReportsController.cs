// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>MH13 – Báo cáo doanh thu (P08, R20, BM09). A09 trả JSON cho Chart.js; hóa đơn trong kỳ là Partial View.</summary>
[Route("manage/reports")]
public class ReportsController : ManageControllerBase
{
    private readonly IReportService _reports;
    private readonly IInventoryService _inventory;

    public ReportsController(IReportService reports, IInventoryService inventory)
    {
        _reports = reports;
        _inventory = inventory;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new ReportPageModel(await _inventory.BranchesAsync(ct), VietnamTime.Today));

    /// <summary>A09: tổng quan + so với kỳ trước, theo ngày, theo chi nhánh, top 10 món.</summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] ReportQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        return Ok(ApiResult.Ok(await _reports.RevenueAsync(query, ct)));
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] ReportQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        return PartialView("_OrderList", await _reports.OrdersAsync(query, ct));
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> OrderDetail(long id, CancellationToken ct) =>
        PartialView("_OrderDetail", await _reports.OrderDetailAsync(id, ct));
}
