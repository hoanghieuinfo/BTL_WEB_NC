// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>
/// MH10 – Thực đơn & định lượng (UC10). Controller mỏng: kiểm tra ModelState, gọi service, trả kết quả.
/// Lỗi nghiệp vụ (404 tenant khác, tên trùng, ảnh sai…) do ManageExceptionFilter chuyển thành JSON.
/// POST được AutoValidateAntiforgeryToken toàn cục kiểm tra token trong header RequestVerificationToken.
/// </summary>
[Route("manage/products")]
public class ProductsController : ManageControllerBase
{
    // Ảnh tối đa 2 MB nhưng cho request tới 5 MB để ảnh quá cỡ vẫn tới service và nhận lỗi tại ô (UC10 6b) thay vì 413.
    private const long MaxRequestBytes = 5 * 1024 * 1024;

    private readonly IProductService _products;
    private readonly ICategoryService _categories;

    public ProductsController(IProductService products, ICategoryService categories)
    {
        _products = products;
        _categories = categories;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _categories.GetSummaryAsync(ct));

    /// <summary>A06 – lọc, tìm kiếm, phân trang món; trả Partial View.</summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] ProductListQuery query, CancellationToken ct) =>
        PartialView("_ProductList", await _products.ListAsync(query, ct));

    /// <summary>A07 – thêm món (multipart vì có ảnh).</summary>
    [HttpPost("create")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create([FromForm] ProductFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        var id = await _products.CreateAsync(model, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã thêm món."));
    }

    /// <summary>A07 – sửa món.</summary>
    [HttpPost("{id:int}/update")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Update(int id, [FromForm] ProductFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _products.UpdateAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã lưu thay đổi."));
    }

    /// <summary>A07 – xóa món (đã bán → xóa mềm).</summary>
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _products.DeleteAsync(id, ct);
        var message = result.SoftDeleted
            ? "Đã ẩn món khỏi thực đơn. Hóa đơn cũ vẫn giữ nguyên."
            : "Đã xóa món.";
        return Ok(ApiResult.Ok(new { id, result.SoftDeleted, summary = await _categories.GetSummaryAsync(ct) }, message));
    }

    /// <summary>Endpoint phụ (họ A07) – bật/tắt Đang bán ngay trên dòng.</summary>
    [HttpPost("{id:int}/availability")]
    public async Task<IActionResult> Availability(int id, [FromBody] AvailabilityModel model, CancellationToken ct)
    {
        await _products.SetAvailabilityAsync(id, model.IsAvailable, ct);
        return Ok(ApiResult.Ok(new { id, model.IsAvailable }, model.IsAvailable ? "Món đã mở bán." : "Món đã ngừng bán."));
    }

    /// <summary>Endpoint phụ (họ A08) – dữ liệu cho hộp thoại định lượng.</summary>
    [HttpGet("{id:int}/recipe")]
    public async Task<IActionResult> Recipe(int id, CancellationToken ct) =>
        Ok(ApiResult.Ok(await _products.GetRecipeAsync(id, ct)));

    /// <summary>A08 – lưu định lượng.</summary>
    [HttpPost("{id:int}/recipe")]
    public async Task<IActionResult> SaveRecipe(int id, [FromBody] RecipeSaveModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _products.SaveRecipeAsync(id, model.Items, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã lưu định lượng."));
    }
}
