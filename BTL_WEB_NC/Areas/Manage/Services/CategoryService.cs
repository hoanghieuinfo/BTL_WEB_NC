// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface ICategoryService
{
    Task<MenuSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<int> CreateAsync(CategoryFormModel model, CancellationToken ct = default);
    Task UpdateAsync(int id, CategoryFormModel model, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Danh mục món (R13). Mọi truy vấn đi qua _db.Categories/_db.Products đã có Global Query Filter
/// (TenantId + !IsDeleted), nên không cần – và không được – tự viết điều kiện TenantId ở đây.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MenuSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        // c.Products cũng bị bộ lọc → không đếm món đã xóa mềm.
        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategorySummary(c.Id, c.Name, c.SortOrder, c.Products.Count()))
            .ToListAsync(ct);

        var total = await _db.Products.CountAsync(ct);
        var missing = await _db.Products.CountAsync(p => !p.Recipes.Any(), ct);

        return new MenuSummary(total, missing, categories);
    }

    public async Task<int> CreateAsync(CategoryFormModel model, CancellationToken ct = default)
    {
        var name = model.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: null, ct);

        var category = new Category { Name = name, SortOrder = model.SortOrder }; // TenantId gán trong SaveChanges
        _db.Categories.Add(category);
        await SaveAsync(name, exceptId: null, ct);
        return category.Id;
    }

    public async Task UpdateAsync(int id, CategoryFormModel model, CancellationToken ct = default)
    {
        var category = await FindAsync(id, ct);
        var name = model.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: id, ct);

        category.Name = name;
        category.SortOrder = model.SortOrder;
        await SaveAsync(name, exceptId: id, ct);
    }

    /// <summary>Xóa mềm; chặn khi danh mục còn món chưa xóa (món phải luôn thuộc một danh mục còn hiệu lực).</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await FindAsync(id, ct);

        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
        {
            throw new BusinessRuleException(
                $"Danh mục \"{category.Name}\" còn {productCount} món. Hãy chuyển các món sang danh mục khác hoặc xóa món trước.");
        }

        category.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Category> FindAsync(int id, CancellationToken ct) =>
        await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Không tìm thấy danh mục.");

    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await NameTakenAsync(name, exceptId, ct))
        {
            throw DuplicateName(name);
        }
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct) =>
        _db.Categories.AnyAsync(c => c.Name == name && c.Id != exceptId, ct);

    /// <summary>Hai request cùng lúc có thể cùng vượt qua bước kiểm tra trên → chỉ mục UNIQUE chặn lại, báo cùng lỗi ô.</summary>
    private async Task SaveAsync(string name, int? exceptId, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            if (await NameTakenAsync(name, exceptId, ct))
            {
                throw DuplicateName(name);
            }
            throw;
        }
    }

    private static FieldValidationException DuplicateName(string name) =>
        new("name", $"Quán đã có danh mục \"{name}\".");
}
