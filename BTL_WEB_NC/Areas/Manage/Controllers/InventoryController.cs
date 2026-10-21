// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Security.Claims;
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>
/// MH12 – Kho nguyên liệu (P06, R18, R19, BM07). Ba tab là ba URL riêng (Tồn kho, Nguyên liệu, Phiếu nhập)
/// + trang Lập phiếu nhập. Danh sách tải bằng AJAX (Partial View), thao tác ghi trả JSON.
/// </summary>
[Route("manage/inventory")]
public class InventoryController : ManageControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IIngredientService _ingredients;

    public InventoryController(IInventoryService inventory, IIngredientService ingredients)
    {
        _inventory = inventory;
        _ingredients = ingredients;
    }

    // ------------------------------------------------------------------ Tồn kho

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View("Stock", new StockPageModel(await HeaderAsync("stock", ct), await _inventory.BranchesAsync(ct)));

    [HttpGet("stock")]
    public async Task<IActionResult> Stock([FromQuery] StockListQuery query, CancellationToken ct) =>
        PartialView("_StockList", await _inventory.StockAsync(query, ct));

    [HttpGet("stock/{branchId:int}/{ingredientId:int}")]
    public async Task<IActionResult> StockDetail(int branchId, int ingredientId, CancellationToken ct) =>
        PartialView("_StockDetail", await _inventory.StockDetailAsync(branchId, ingredientId, ct));

    // ------------------------------------------------------------------ Nguyên liệu

    [HttpGet("ingredients")]
    public async Task<IActionResult> Ingredients(CancellationToken ct) =>
        View(new IngredientPageModel(await HeaderAsync("ingredients", ct), await _ingredients.UnitsAsync(ct)));

    [HttpGet("ingredients/list")]
    public async Task<IActionResult> IngredientList([FromQuery] IngredientListQuery query, CancellationToken ct) =>
        PartialView("_IngredientList", await _ingredients.ListAsync(query, ct));

    [HttpPost("ingredients/create")]
    public async Task<IActionResult> CreateIngredient([FromBody] IngredientFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        var id = await _ingredients.CreateAsync(model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã thêm nguyên liệu."));
    }

    [HttpPost("ingredients/{id:int}/update")]
    public async Task<IActionResult> UpdateIngredient(int id, [FromBody] IngredientFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        await _ingredients.UpdateAsync(id, model, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã lưu nguyên liệu."));
    }

    [HttpPost("ingredients/{id:int}/delete")]
    public async Task<IActionResult> DeleteIngredient(int id, CancellationToken ct)
    {
        await _ingredients.DeleteAsync(id, ct);
        return Ok(ApiResult.Ok(new { id }, "Đã xóa nguyên liệu."));
    }

    // ------------------------------------------------------------------ Phiếu nhập

    [HttpGet("receipts")]
    public async Task<IActionResult> Receipts(CancellationToken ct) =>
        View(new ReceiptPageModel(await HeaderAsync("receipts", ct), await _inventory.BranchesAsync(ct),
            VietnamTime.Today.ToString("yyyy-MM")));

    [HttpGet("receipts/list")]
    public async Task<IActionResult> ReceiptList([FromQuery] ReceiptListQuery query, CancellationToken ct) =>
        PartialView("_ReceiptList", await _inventory.ListReceiptsAsync(query, ct));

    [HttpGet("receipts/{id:int}")]
    public async Task<IActionResult> ReceiptDetail(int id, CancellationToken ct) =>
        PartialView("_ReceiptDetail", await _inventory.GetReceiptAsync(id, ct));

    /// <summary>Trang Lập phiếu nhập; nút "Nhập hàng" ở tab Tồn kho truyền sẵn chi nhánh và nguyên liệu.</summary>
    [HttpGet("receipts/new")]
    public async Task<IActionResult> NewReceipt(int? branchId, int? ingredientId, CancellationToken ct) =>
        View(await _inventory.NewReceiptAsync(branchId, ingredientId, ct));

    [HttpPost("receipts/create")]
    public async Task<IActionResult> CreateReceipt([FromBody] ReceiptFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResult.ValidationFailed(ModelState));
        }

        // Người nhập lấy từ cookie đăng nhập, không nhận từ request.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var created = await _inventory.CreateReceiptAsync(model, userId, ct);
        return Ok(ApiResult.Ok(created, $"Đã lưu phiếu nhập {created.Code}."));
    }

    private async Task<InventoryHeaderModel> HeaderAsync(string tab, CancellationToken ct) =>
        new(tab, await _ingredients.CountAsync(ct));
}
