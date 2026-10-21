// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>Endpoint phụ cho danh mục món (R13) – dùng trong MH10, trả JSON kèm bộ đếm mới.</summary>
[Route("manage/categories")]
public class CategoriesController : ManageControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CategoryFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        var id = await _categories.CreateAsync(model, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã thêm danh mục."));
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _categories.UpdateAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã lưu danh mục."));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã xóa danh mục."));
    }
}
