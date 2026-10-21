// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>
/// MH09 – Chi nhánh, khu vực, bàn, mã QR. Controller mỏng: kiểm tra ModelState, gọi service.
/// Đọc trả Partial View, ghi trả JSON; lỗi nghiệp vụ (404 tenant khác, hết suất gói…) do ManageExceptionFilter xử lý.
/// Route khu vực/bàn bắt đầu bằng "~/" để không ghép với tiền tố manage/branches.
/// </summary>
[Route("manage/branches")]
public class BranchesController : ManageControllerBase
{
    private readonly IBranchService _branches;
    private readonly IBranchQrService _qr;
    private readonly IPlanUsageService _usage;

    public BranchesController(IBranchService branches, IBranchQrService qr, IPlanUsageService usage)
    {
        _branches = branches;
        _qr = qr;
        _usage = usage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? branchId, CancellationToken ct)
    {
        var list = await BuildListAsync(branchId, ct);
        var board = list.SelectedId is int id ? await _branches.GetBoardAsync(id, ct) : null;
        var tenantSlug = await _branches.GetTenantSlugAsync(ct);
        return View(new BranchPageModel(list.Usage, tenantSlug, list, board));
    }

    /// <summary>Bảng chi nhánh (Partial View).</summary>
    [HttpGet("list")]
    public async Task<IActionResult> List(int? selectedId, CancellationToken ct) =>
        PartialView("_BranchList", await BuildListAsync(selectedId, ct));

    /// <summary>Khu vực và bàn của chi nhánh đang chọn (Partial View). Chi nhánh của tenant khác → 404.</summary>
    [HttpGet("{id:int}/zones")]
    public async Task<IActionResult> Zones(int id, CancellationToken ct) =>
        PartialView("_ZoneBoard", await _branches.GetBoardAsync(id, ct));

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] BranchFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        var id = await _branches.CreateBranchAsync(model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã thêm chi nhánh."));
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, [FromBody] BranchFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        await _branches.UpdateBranchAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã lưu chi nhánh."));
    }

    // ------------------------------------------------------------------ Khu vực

    [HttpPost("{id:int}/zones/create")]
    public async Task<IActionResult> CreateZone(int id, [FromBody] ZoneFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        var zoneId = await _branches.CreateZoneAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id = zoneId }, "Đã thêm khu vực."));
    }

    [HttpPost("~/manage/zones/{id:int}/rename")]
    public async Task<IActionResult> RenameZone(int id, [FromBody] ZoneFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        await _branches.RenameZoneAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã đổi tên khu vực."));
    }

    [HttpPost("~/manage/zones/{id:int}/move")]
    public async Task<IActionResult> MoveZone(int id, [FromBody] ZoneMoveModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        await _branches.MoveZoneAsync(id, model.Direction == "up", ct);
        return Ok(ApiResult.Ok(new { id }));
    }

    // ------------------------------------------------------------------ Bàn

    [HttpPost("~/manage/zones/{id:int}/tables/create")]
    public async Task<IActionResult> CreateTable(int id, [FromBody] TableFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        var tableId = await _branches.CreateTableAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id = tableId }, "Đã thêm bàn."));
    }

    [HttpPost("~/manage/tables/{id:int}/update")]
    public async Task<IActionResult> UpdateTable(int id, [FromBody] TableFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        await _branches.UpdateTableAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã lưu bàn."));
    }

    [HttpPost("~/manage/tables/{id:int}/visibility")]
    public async Task<IActionResult> TableVisibility(int id, [FromBody] TableVisibilityModel model, CancellationToken ct)
    {
        await _branches.SetTableActiveAsync(id, model.IsActive, ct);
        return Ok(ApiResult.Ok(new { id, model.IsActive }, model.IsActive ? "Đã hiện bàn." : "Đã ẩn bàn."));
    }

    // ------------------------------------------------------------------ Mã QR (R16)

    [HttpGet("{id:int}/qr")]
    public async Task<IActionResult> QrInfo(int id, CancellationToken ct) =>
        Ok(ApiResult.Ok(await _qr.GetInfoAsync(id, RequestBaseUrl(), ct)));

    [HttpGet("{id:int}/qr.png")]
    public async Task<IActionResult> QrImage(int id, bool download, CancellationToken ct)
    {
        var info = await _qr.GetInfoAsync(id, RequestBaseUrl(), ct);
        var png = _qr.RenderPng(info.Url);
        return download ? File(png, "image/png", info.FileName) : File(png, "image/png");
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private async Task<BranchListModel> BuildListAsync(int? selectedId, CancellationToken ct)
    {
        var branches = await _branches.ListAsync(ct);
        // Chi nhánh được chọn phải nằm trong danh sách của tenant; không có thì chọn chi nhánh đầu tiên.
        var selected = branches.Any(b => b.Id == selectedId) ? selectedId : branches.FirstOrDefault()?.Id;
        return new BranchListModel(branches, selected, await _usage.GetAsync(ct));
    }
}
