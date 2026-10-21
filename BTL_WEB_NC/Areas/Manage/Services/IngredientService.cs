// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IIngredientService
{
    Task<IngredientListResult> ListAsync(IngredientListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> UnitsAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CreateAsync(IngredientFormModel model, CancellationToken ct = default);
    Task UpdateAsync(int id, IngredientFormModel model, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Danh mục nguyên liệu (P06 bước 1, R18). Mọi truy vấn đi qua _db.Ingredients – Global Query Filter gộp
/// TenantId và !IsDeleted – nên nguyên liệu của quán khác hoặc đã xóa coi như không có → 404.
/// </summary>
public class IngredientService : IIngredientService
{
    private readonly AppDbContext _db;

    public IngredientService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IngredientListResult> ListAsync(IngredientListQuery query, CancellationToken ct = default)
    {
        var ingredients = _db.Ingredients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim();
            ingredients = ingredients.Where(i => i.Name.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(query.Unit))
        {
            var unit = query.Unit.Trim();
            ingredients = ingredients.Where(i => i.Unit == unit);
        }

        var total = await ingredients.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)IngredientListQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);

        var items = await ingredients
            .OrderBy(i => i.Name)
            .Skip((page - 1) * IngredientListQuery.PageSize)
            .Take(IngredientListQuery.PageSize)
            .Select(i => new IngredientRow(
                i.Id, i.Name, i.Unit, i.MinStock,
                // _db.Products đã lọc món xóa mềm → "Dùng trong N món" chỉ đếm món còn trên thực đơn.
                _db.Products.Count(p => p.Recipes.Any(r => r.IngredientId == i.Id)),
                _db.Recipes.Any(r => r.IngredientId == i.Id)
                    || _db.StockReceiptItems.Any(x => x.IngredientId == i.Id)
                    || _db.BranchStocks.Any(s => s.IngredientId == i.Id && s.Quantity != 0)))
            .ToListAsync(ct);

        return new IngredientListResult(items, page, total, query);
    }

    public async Task<IReadOnlyList<string>> UnitsAsync(CancellationToken ct = default) =>
        await _db.Ingredients.Select(i => i.Unit).Distinct().OrderBy(u => u).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => _db.Ingredients.CountAsync(ct);

    public async Task<int> CreateAsync(IngredientFormModel model, CancellationToken ct = default)
    {
        var name = model.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: null, ct);

        var ingredient = new Ingredient { Name = name, Unit = model.Unit.Trim(), MinStock = model.MinStock!.Value };
        _db.Ingredients.Add(ingredient);
        await SaveAsync(name, exceptId: null, ct);
        return ingredient.Id;
    }

    public async Task UpdateAsync(int id, IngredientFormModel model, CancellationToken ct = default)
    {
        var ingredient = await FindAsync(id, ct);
        var name = model.Name.Trim();
        var unit = model.Unit.Trim();
        await EnsureUniqueNameAsync(name, exceptId: id, ct);

        // Định lượng, tồn kho và phiếu nhập đều lưu theo đơn vị cơ sở: đổi "g" thành "kg" khi đã có số liệu
        // sẽ làm mọi con số cũ sai nghĩa (P06 bước 1) → chỉ cho đổi khi nguyên liệu chưa được dùng ở đâu.
        if (!string.Equals(unit, ingredient.Unit, StringComparison.Ordinal) && await IsUsedAsync(id, ct))
        {
            throw new FieldValidationException("unit",
                $"Không đổi được đơn vị cơ sở vì nguyên liệu đã có định lượng, tồn kho hoặc phiếu nhập theo \"{ingredient.Unit}\".");
        }

        ingredient.Name = name;
        ingredient.Unit = unit;
        ingredient.MinStock = model.MinStock!.Value;
        await SaveAsync(name, exceptId: id, ct);
    }

    /// <summary>
    /// Xóa mềm (R42): tồn kho và phiếu nhập cũ vẫn trỏ tới nguyên liệu. Chặn khi món đang bán còn định lượng
    /// dùng nó – nếu không, trừ kho (P07) sẽ trừ vào một nguyên liệu chủ quán không còn thấy.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var ingredient = await FindAsync(id, ct);
        var productCount = await _db.Products.CountAsync(p => p.Recipes.Any(r => r.IngredientId == id), ct);
        if (productCount > 0)
        {
            throw new BusinessRuleException(
                $"\"{ingredient.Name}\" đang dùng trong định lượng của {productCount} món. Bỏ nguyên liệu khỏi các món đó trước khi xóa.");
        }

        ingredient.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ Hàm phụ

    private async Task<Ingredient> FindAsync(int id, CancellationToken ct) =>
        await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException("Không tìm thấy nguyên liệu.");

    private async Task<bool> IsUsedAsync(int id, CancellationToken ct) =>
        await _db.Recipes.AnyAsync(r => r.IngredientId == id, ct)
        || await _db.StockReceiptItems.AnyAsync(x => x.IngredientId == id, ct)
        || await _db.BranchStocks.AnyAsync(s => s.IngredientId == id && s.Quantity != 0, ct);

    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await NameTakenAsync(name, exceptId, ct))
        {
            throw DuplicateName(name);
        }
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct) =>
        _db.Ingredients.AnyAsync(i => i.Name == name && i.Id != exceptId, ct);

    private async Task SaveAsync(string name, int? exceptId, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Hai request đồng thời cùng qua bước kiểm tra → chỉ mục UNIQUE (TenantId, Name) lọc IsDeleted chặn lại.
            if (await NameTakenAsync(name, exceptId, ct))
            {
                throw DuplicateName(name);
            }
            throw;
        }
    }

    private static FieldValidationException DuplicateName(string name) =>
        new("name", $"Quán đã có nguyên liệu \"{name}\".");
}
