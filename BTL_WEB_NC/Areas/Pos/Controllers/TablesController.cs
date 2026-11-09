// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Pos.Controllers;

/// <summary>MH14 /pos – Sơ đồ bàn (UC14) và A10 GET /pos/tables/status – trạng thái các bàn.</summary>
[Route("pos")]
public class TablesController : PosControllerBase
{
    private readonly ITableMapService _tables;
    private readonly TenantContext _tenant;

    public TablesController(ITableMapService tables, TenantContext tenant)
    {
        _tables = tables;
        _tenant = tenant;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (_tenant.TenantId is int tenantId && _tenant.BranchId is int branchId)
        {
            var header = await _tables.GetHeaderAsync(tenantId, branchId, ct);
            ViewData["TenantName"] = header.TenantName;
            ViewData["BranchName"] = header.BranchName;
        }
        return View();
    }

    /// <summary>A10: trình duyệt gọi lại mỗi 10 giây để đổi màu bàn.</summary>
    [HttpGet("tables/status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }
        return Ok(ApiResult.Ok(await _tables.GetTablesAsync(branchId, ct)));
    }
}
