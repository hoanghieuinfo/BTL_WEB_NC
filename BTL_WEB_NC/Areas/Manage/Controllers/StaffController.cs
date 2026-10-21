// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>
/// MH11 – Nhân viên (P04, R17). Controller mỏng; lọc theo tenant và vai trò Staff nằm trong StaffService
/// vì AppUser không có Global Query Filter. {id} là AspNetUsers.Id.
/// </summary>
[Route("manage/staff")]
public class StaffController : ManageControllerBase
{
    private readonly IStaffService _staff;
    private readonly IPlanUsageService _usage;

    public StaffController(IStaffService staff, IPlanUsageService usage)
    {
        _staff = staff;
        _usage = usage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new StaffPageModel(await _usage.GetAsync(ct), await _staff.ActiveBranchesAsync(ct)));

    /// <summary>Tìm kiếm, lọc chi nhánh/trạng thái, phân trang (Partial View).</summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] StaffListQuery query, CancellationToken ct)
    {
        ViewData["Usage"] = await _usage.GetAsync(ct);
        return PartialView("_StaffList", await _staff.ListAsync(query, ct));
    }

    /// <summary>
    /// Thêm nhân viên. Phản hồi chứa mật khẩu khởi tạo (chỉ một lần) → cấm trình duyệt/proxy lưu đệm.
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] StaffFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        var created = await _staff.CreateAsync(model, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResult.Ok(created, "Đã tạo tài khoản nhân viên."));
    }

    /// <summary>Sửa họ tên, số điện thoại.</summary>
    [HttpPost("{id}/update")]
    public async Task<IActionResult> Update(string id, [FromBody] StaffEditModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _staff.UpdateAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã lưu thông tin nhân viên."));
    }

    /// <summary>Đặt lại mật khẩu: phản hồi chứa mật khẩu khởi tạo mới (một lần) → no-store như khi tạo tài khoản.</summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken ct)
    {
        var result = await _staff.ResetPasswordAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResult.Ok(result, "Đã đặt lại mật khẩu."));
    }

    [HttpPost("{id}/transfer")]
    public async Task<IActionResult> Transfer(string id, [FromBody] StaffTransferModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _staff.TransferAsync(id, model.BranchId!.Value, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã chuyển chi nhánh. Nhân viên sẽ được đăng xuất trong vài phút."));
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(string id, CancellationToken ct)
    {
        await _staff.LockAsync(id, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã khóa tài khoản."));
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(string id, CancellationToken ct)
    {
        await _staff.UnlockAsync(id, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã mở khóa tài khoản."));
    }
}
