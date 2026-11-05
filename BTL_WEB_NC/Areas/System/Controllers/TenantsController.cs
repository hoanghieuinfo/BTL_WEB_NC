// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Areas.System.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.System.Controllers;

/// <summary>MH06 – Quản lý Tenant (UC04, R05, R06).</summary>
public class TenantsController : SystemControllerBase
{
    private readonly ISystemTenantService _tenantService;
    private readonly ISystemPlanService _planService;

    public TenantsController(ISystemTenantService tenantService, ISystemPlanService planService)
    {
        _tenantService = tenantService;
        _planService = planService;
    }

    // GET /system/tenants – Trang danh sách tenant
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Plans = await _planService.GetAllPlansAsync(ct);
        return View();
    }

    // GET /system/tenants/list – AJAX A03: lọc, tìm kiếm, phân trang
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TenantFilterModel filter, CancellationToken ct)
    {
        var result = await _tenantService.GetPagedTenantsAsync(filter, ct);
        return PartialView("_TenantList", result);
    }

    // POST /system/tenants/{id}/status – AJAX A04: kích hoạt, khóa, mở khóa, từ chối
    [HttpPost("system/tenants/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTenantStatusRequest model, CancellationToken ct)
    {
        try
        {
            await _tenantService.UpdateStatusAsync(id, model.Status, model.LockReason, ct);
            var statusText = model.Status switch
            {
                TenantStatus.Active => "kích hoạt",
                TenantStatus.Locked => "khóa",
                TenantStatus.Rejected => "từ chối",
                TenantStatus.PendingActivation => "chuyển về chờ kích hoạt",
                _ => "cập nhật"
            };
            return Ok(ApiResult.Ok(null, $"Đã {statusText} quán thành công."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResult.Fail(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ApiResult.Fail(ex.Message));
        }
    }

    // POST /system/tenants/{id}/plan – AJAX A05: đổi gói dịch vụ
    [HttpPost("system/tenants/{id:int}/plan")]
    public async Task<IActionResult> ChangePlan(int id, [FromBody] ChangeTenantPlanRequest model, CancellationToken ct)
    {
        try
        {
            await _tenantService.ChangePlanAsync(id, model.PlanId, ct);
            return Ok(ApiResult.Ok(null, "Đã đổi gói dịch vụ thành công."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResult.Fail(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ApiResult.Fail(ex.Message));
        }
    }
}
