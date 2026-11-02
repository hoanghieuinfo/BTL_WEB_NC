// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Security.Claims;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Pos.Controllers;

/// <summary>MH15 – đơn hàng tại bàn. Trang đơn (GET /pos/orders/{id}), A12 mở đơn, A13–A15, A29 tạm tính, A30 hủy đơn, A16 thanh toán, MH16 trang in (/print).</summary>
[Route("pos/orders")]
public class OrdersController : PosControllerBase
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;
    private readonly ITableMapService _tables;
    private readonly TenantContext _tenant;

    public OrdersController(IOrderService orders, IPaymentService payments, ITableMapService tables, TenantContext tenant)
    {
        _orders = orders;
        _payments = payments;
        _tables = tables;
        _tenant = tenant;
    }

    /// <summary>MH15: trang đơn hàng của một bàn. Đơn không thuộc chi nhánh của nhân viên hiện trang "không tìm thấy" (404).</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Index(long id, CancellationToken ct)
    {
        if (_tenant.TenantId is not int tenantId || _tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        // Đây là request trang (không phải AJAX) nên không để filter trả JSON: bắt NotFoundException và vẽ trang thân thiện.
        try
        {
            var order = await _orders.GetAsync(branchId, id, ct);
            var header = await _tables.GetHeaderAsync(tenantId, branchId, ct);
            ViewData["TenantName"] = header.TenantName;
            ViewData["BranchName"] = header.BranchName;
            return View(order);
        }
        catch (NotFoundException)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }
    }

    /// <summary>
    /// MH16: trang in phiếu tạm tính (đơn Đang phục vụ) hoặc hóa đơn BM06 (đơn Đã thanh toán). Là GET chỉ đọc: không đổi trạng thái bàn
    /// (tạm tính đã đổi ở A29). Đơn đã hủy, của chi nhánh khác hoặc quán khác → trang "không tìm thấy" (404).
    /// </summary>
    [HttpGet("{id:long}/print")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]   // phiếu chứa số liệu tiền: không để trình duyệt/proxy lưu đệm
    public async Task<IActionResult> Print(long id, CancellationToken ct)
    {
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        try
        {
            return View(await _orders.GetPrintAsync(branchId, id, ct));
        }
        catch (NotFoundException)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenOrderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId || User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        return Ok(ApiResult.Ok(await _orders.OpenAsync(branchId, userId, model, ct), "Đã mở đơn."));
    }

    /// <summary>A13: thêm món, đổi số lượng, ghi chú. Trả đơn đã tính lại tổng tiền để trình duyệt vẽ lại.</summary>
    [HttpPost("{id:long}/items")]
    public async Task<IActionResult> SaveItem(long id, [FromBody] SaveOrderItemModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        var message = model.ItemId.HasValue ? "Đã cập nhật món." : "Đã thêm món.";
        return Ok(ApiResult.Ok(await _orders.SaveItemAsync(branchId, id, model, ct), message));
    }

    /// <summary>A14: hủy một món (bắt buộc lý do). Trả đơn đã tính lại tổng tiền để trình duyệt vẽ lại.</summary>
    [HttpPost("{id:long}/items/{itemId:long}/cancel")]
    public async Task<IActionResult> CancelItem(long id, long itemId, [FromBody] CancelOrderItemModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        return Ok(ApiResult.Ok(await _orders.CancelItemAsync(branchId, id, itemId, model, ct), "Đã hủy món."));
    }

    /// <summary>A15: chuyển đơn sang bàn trống khác. Trả đơn (đã ở bàn mới) để trình duyệt vẽ lại.</summary>
    [HttpPost("{id:long}/transfer")]
    public async Task<IActionResult> Transfer(long id, [FromBody] MoveOrderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        var detail = await _orders.TransferAsync(branchId, id, model, ct);
        return Ok(ApiResult.Ok(detail, $"Đã chuyển sang {detail.TableName}."));
    }

    /// <summary>A15: gộp đơn vào đơn đang phục vụ của bàn khác. Trả đơn đích để trình duyệt chuyển sang.</summary>
    [HttpPost("{id:long}/merge")]
    public async Task<IActionResult> Merge(long id, [FromBody] MoveOrderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        var merged = await _orders.MergeAsync(branchId, id, model, ct);
        return Ok(ApiResult.Ok(merged, $"Đã gộp vào {merged.TargetTableName}."));
    }

    /// <summary>A29: tạm tính – đổi bàn sang Chờ thanh toán. Trình duyệt vẽ lại đơn rồi mở trang in phiếu (MH16). Không có body.</summary>
    [HttpPost("{id:long}/provisional")]
    public async Task<IActionResult> Provisional(long id, CancellationToken ct)
    {
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        return Ok(ApiResult.Ok(await _orders.ProvisionalAsync(branchId, id, ct), "Đã tạm tính. Bàn chuyển sang Chờ thanh toán."));
    }

    /// <summary>A30: hủy đơn (đơn đã có món thì bắt buộc lý do). Bàn về Trống; trình duyệt quay lại sơ đồ bàn.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelOrderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        return Ok(ApiResult.Ok(await _orders.CancelOrderAsync(branchId, id, model, ct), "Đã hủy đơn."));
    }

    /// <summary>A16: thanh toán và trừ kho. Tổng tiền do server tính lại; BranchId và thu ngân lấy từ claim.</summary>
    [HttpPost("{id:long}/pay")]
    public async Task<IActionResult> Pay(long id, [FromBody] PayOrderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }
        if (_tenant.BranchId is not int branchId || User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        return Ok(ApiResult.Ok(await _payments.PayAsync(branchId, userId, id, model, ct), "Đã thanh toán."));
    }
}
