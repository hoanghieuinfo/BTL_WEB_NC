// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Pos.Controllers;

/// <summary>A11 GET /pos/products/search – tìm, lọc món khi bán (R24).</summary>
[Route("pos/products")]
public class ProductsController : PosControllerBase
{
    private readonly IMenuService _menu;

    public ProductsController(IMenuService menu)
    {
        _menu = menu;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] MenuSearchQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        return Ok(ApiResult.Ok(await _menu.SearchAsync(query.Q, query.CategoryId, ct)));
    }
}
