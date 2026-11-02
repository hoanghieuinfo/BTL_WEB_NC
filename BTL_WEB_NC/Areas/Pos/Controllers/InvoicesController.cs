// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Pos.Controllers;

/// <summary>MH17 /pos/invoices – hóa đơn đã thanh toán trong ngày của chi nhánh (UC17, R28). Trang chỉ đọc, không có AJAX.</summary>
[Route("pos/invoices")]
public class InvoicesController : PosControllerBase
{
    private readonly IOrderService _orders;
    private readonly ITableMapService _tables;
    private readonly TenantContext _tenant;

    public InvoicesController(IOrderService orders, ITableMapService tables, TenantContext tenant)
    {
        _orders = orders;
        _tables = tables;
        _tenant = tenant;
    }

    /// <summary>
    /// Ngày luôn là "hôm nay" theo giờ Việt Nam do server tính: không nhận ngày từ request nên nhân viên không xem được ngày khác
    /// (xem theo kỳ là việc của chủ quán ở MH13). Chi nhánh lấy từ claim.
    /// </summary>
    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]   // danh sách chứa số liệu tiền, và phải luôn mới
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (_tenant.TenantId is not int tenantId || _tenant.BranchId is not int branchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResult.Fail(NoBranch));
        }

        var header = await _tables.GetHeaderAsync(tenantId, branchId, ct);
        ViewData["TenantName"] = header.TenantName;
        ViewData["BranchName"] = header.BranchName;
        return View(await _orders.ListPaidAsync(branchId, VietnamTime.Today, ct));
    }
}
