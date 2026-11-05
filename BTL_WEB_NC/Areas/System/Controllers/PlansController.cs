// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Areas.System.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.System.Controllers;

/// <summary>MH07 – Quản lý gói dịch vụ (UC05, R07, R08).</summary>
public class PlansController : SystemControllerBase
{
    private readonly ISystemPlanService _planService;

    public PlansController(ISystemPlanService planService)
    {
        _planService = planService;
    }

    // GET /system/plans – Trang danh sách gói dịch vụ
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await _planService.GetAllPlansAsync(ct);
        return View(plans);
    }

    // POST /system/plans/save – AJAX: thêm hoặc cập nhật gói
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ServicePlanFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            // data.errors = { tenTruong: [...] } như mọi endpoint khác (mục 3.2.6) để ajax.js hiện lỗi tại từng ô.
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        try
        {
            var id = await _planService.SavePlanAsync(model, ct);
            return Ok(ApiResult.Ok(new { id }, model.Id.HasValue ? "Cập nhật gói dịch vụ thành công." : "Thêm gói dịch vụ thành công."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResult.Fail(ex.Message));
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(ApiResult.FieldError(ex.Field, ex.Message));
        }
    }

    // POST /system/plans/{id}/toggle – AJAX: bật/tắt trạng thái gói
    [HttpPost("system/plans/{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        try
        {
            await _planService.ToggleActiveAsync(id, ct);
            return Ok(ApiResult.Ok(null, "Đã cập nhật trạng thái gói dịch vụ."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResult.Fail(ex.Message));
        }
    }
}
