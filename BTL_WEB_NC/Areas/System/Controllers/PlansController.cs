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
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(k => k.Key, v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(ApiResult.Fail("Dữ liệu không hợp lệ.", errors));
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
            return BadRequest(ApiResult.Fail(ex.Message, new Dictionary<string, string[]> { { ex.Field, new[] { ex.Message } } }));
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
