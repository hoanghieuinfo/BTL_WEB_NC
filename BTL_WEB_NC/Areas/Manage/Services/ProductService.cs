// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Files;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IProductService
{
    Task<ProductListResult> ListAsync(ProductListQuery query, CancellationToken ct = default);
    Task<int> CreateAsync(ProductFormModel model, CancellationToken ct = default);
    Task UpdateAsync(int id, ProductFormModel model, CancellationToken ct = default);
    Task<ProductDeleteResult> DeleteAsync(int id, CancellationToken ct = default);
    Task SetAvailabilityAsync(int id, bool isAvailable, CancellationToken ct = default);
    Task<RecipeEditorData> GetRecipeAsync(int id, CancellationToken ct = default);
    Task SaveRecipeAsync(int id, IReadOnlyList<RecipeItemModel> items, CancellationToken ct = default);
}

/// <summary>
/// Nghiệp vụ món và định lượng (UC10, R14, R15, R21). Hai lớp bảo vệ đa tenant:
/// 1) đọc/sửa/xóa theo ID trên URL: tra qua _db.Products (đã lọc) → món tenant khác coi như không có → 404;
/// 2) khóa ngoại trong request (CategoryId, IngredientId): tra lại qua DbSet đã lọc TRƯỚC khi gán (mục 3.2.2e),
///    vì Global Query Filter chỉ bảo vệ khi đọc – không chặn INSERT trỏ sang danh mục của quán khác.
/// </summary>
public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IImageUploadService _images;

    public ProductService(AppDbContext db, TenantContext tenant, IImageUploadService images)
    {
        _db = db;
        _tenant = tenant;
        _images = images;
    }

    // ------------------------------------------------------------------ A06: danh sách

    public async Task<ProductListResult> ListAsync(ProductListQuery query, CancellationToken ct = default)
    {
        var products = _db.Products.AsNoTracking();

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }
        if (query.Status == "selling")
        {
            products = products.Where(p => p.IsAvailable);
        }
        else if (query.Status == "stopped")
        {
            products = products.Where(p => !p.IsAvailable);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim();
            products = products.Where(p => p.Name.Contains(keyword)); // EF tham số hóa, không nối chuỗi SQL (R37)
        }
        if (query.NoRecipe)
        {
            products = products.Where(p => !p.Recipes.Any());
        }

        var total = await products.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)ProductListQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);

        var items = await products
            .OrderBy(p => p.Category.SortOrder).ThenBy(p => p.Category.Name).ThenBy(p => p.Name)
            .Skip((page - 1) * ProductListQuery.PageSize)
            .Take(ProductListQuery.PageSize)
            .Select(p => new ProductRow(
                p.Id, p.Name, p.CategoryId, p.Category.Name, p.Price, p.ImagePath, p.Description, p.IsAvailable,
                p.Recipes.Count(),
                _db.OrderItems.Where(i => i.ProductId == p.Id).Select(i => i.OrderId).Distinct().Count()))
            .ToListAsync(ct);

        return new ProductListResult(items, page, total, query);
    }

    // ------------------------------------------------------------------ A07: thêm, sửa, xóa

    public async Task<int> CreateAsync(ProductFormModel model, CancellationToken ct = default)
    {
        await EnsureCategoryAsync(model.CategoryId!.Value, ct);
        var name = model.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: null, ct);

        var imagePath = await SaveImageAsync(model.Image, ct);
        var product = new Product
        {
            // Không gán TenantId: AppDbContext tự gán theo tenant hiện tại khi lưu.
            CategoryId = model.CategoryId.Value,
            Name = name,
            Price = model.Price!.Value,
            Description = NormalizeDescription(model.Description),
            IsAvailable = model.IsAvailable,
            ImagePath = imagePath
        };
        _db.Products.Add(product);

        try
        {
            await SaveAsync(name, exceptId: null, ct);
        }
        catch
        {
            _images.Delete(imagePath); // không để lại file mồ côi khi lưu CSDL thất bại
            throw;
        }
        return product.Id;
    }

    public async Task UpdateAsync(int id, ProductFormModel model, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        await EnsureCategoryAsync(model.CategoryId!.Value, ct);
        var name = model.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: id, ct);

        var newImagePath = await SaveImageAsync(model.Image, ct);
        var oldImagePath = product.ImagePath;

        product.CategoryId = model.CategoryId.Value;
        product.Name = name;
        product.Price = model.Price!.Value;
        product.Description = NormalizeDescription(model.Description);
        product.IsAvailable = model.IsAvailable;
        if (newImagePath is not null)
        {
            product.ImagePath = newImagePath;
        }

        try
        {
            await SaveAsync(name, exceptId: id, ct);
        }
        catch
        {
            _images.Delete(newImagePath);
            throw;
        }

        // Chỉ xóa ảnh cũ sau khi CSDL đã trỏ sang ảnh mới.
        if (newImagePath is not null)
        {
            _images.Delete(oldImagePath);
        }
    }

    /// <summary>
    /// UC10 – xóa món: món đã có trong hóa đơn thì chỉ xóa mềm (R42) để hóa đơn cũ và báo cáo giữ nguyên;
    /// món chưa bán lần nào thì xóa hẳn cùng định lượng của nó.
    /// </summary>
    public async Task<ProductDeleteResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id, ct))
        {
            product.IsDeleted = true;
            await _db.SaveChangesAsync(ct);
            return new ProductDeleteResult(SoftDeleted: true);
        }

        // Một lần SaveChanges = một transaction: xóa định lượng và món cùng thành công hoặc cùng hoàn tác.
        _db.Recipes.RemoveRange(await _db.Recipes.Where(r => r.ProductId == id).ToListAsync(ct));
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);

        _images.Delete(product.ImagePath);
        return new ProductDeleteResult(SoftDeleted: false);
    }

    public async Task SetAvailabilityAsync(int id, bool isAvailable, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        product.IsAvailable = isAvailable;
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ A08: định lượng

    public async Task<RecipeEditorData> GetRecipeAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy món.");

        // Chỉ nguyên liệu của tenant hiện tại, chưa xóa (bộ lọc của Ingredients).
        var ingredients = await _db.Ingredients.AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new IngredientOption(i.Id, i.Name, i.Unit))
            .ToListAsync(ct);

        var items = await _db.Recipes.AsNoTracking()
            .Where(r => r.ProductId == id)
            .OrderBy(r => r.Ingredient.Name)
            .Select(r => new RecipeLine(r.IngredientId, r.Quantity))
            .ToListAsync(ct);

        return new RecipeEditorData(product.Id, product.Name, ingredients, items);
    }

    /// <summary>
    /// Thay toàn bộ định lượng của món. Danh sách rỗng là hợp lệ (món trở thành "chưa định lượng").
    /// Mọi IngredientId phải thuộc tenant hiện tại; thiếu một cái là 404 và không thay đổi gì.
    /// </summary>
    public async Task SaveRecipeAsync(int id, IReadOnlyList<RecipeItemModel> items, CancellationToken ct = default)
    {
        await FindAsync(id, ct);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Quantity <= 0)
            {
                throw new FieldValidationException($"items[{i}].quantity", "Định lượng phải lớn hơn 0.");
            }
            if (items[i].Quantity != Math.Round(items[i].Quantity, 3))
            {
                throw new FieldValidationException($"items[{i}].quantity", "Định lượng tối đa 3 chữ số thập phân.");
            }
            for (var j = 0; j < i; j++)
            {
                if (items[j].IngredientId == items[i].IngredientId)
                {
                    throw new FieldValidationException($"items[{i}].ingredientId", "Nguyên liệu này đã có ở dòng trên.");
                }
            }
        }

        var requestedIds = items.Select(x => x.IngredientId).ToList();
        var validIds = await _db.Ingredients.Where(x => requestedIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (validIds.Count != requestedIds.Count)
        {
            throw new NotFoundException("Không tìm thấy nguyên liệu.");
        }

        // So sánh với định lượng cũ: sửa dòng còn giữ, xóa dòng bị bỏ, thêm dòng mới.
        // (Không xóa hết rồi thêm lại vì cùng khóa (ProductId, IngredientId) trong một lần lưu sẽ xung đột theo dõi của EF.)
        var existing = await _db.Recipes.Where(r => r.ProductId == id).ToListAsync(ct);
        foreach (var recipe in existing)
        {
            var match = items.FirstOrDefault(x => x.IngredientId == recipe.IngredientId);
            if (match is null)
            {
                _db.Recipes.Remove(recipe);
            }
            else
            {
                recipe.Quantity = match.Quantity;
            }
        }
        foreach (var item in items.Where(x => existing.All(r => r.IngredientId != x.IngredientId)))
        {
            _db.Recipes.Add(new Recipe { ProductId = id, IngredientId = item.IngredientId, Quantity = item.Quantity });
        }

        await _db.SaveChangesAsync(ct); // một transaction
    }

    // ------------------------------------------------------------------ Hàm phụ

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("Không tìm thấy món.");

    /// <summary>UC10 6c: danh mục không thuộc tenant (request bị sửa) → 404.</summary>
    private async Task EnsureCategoryAsync(int categoryId, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new NotFoundException("Không tìm thấy danh mục.");
        }
    }

    /// <summary>UC10 6a: tên trùng với món chưa xóa trong cùng tenant → lỗi tại ô tên món.</summary>
    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await NameTakenAsync(name, exceptId, ct))
        {
            throw DuplicateName(name);
        }
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.Name == name && p.Id != exceptId, ct);

    private async Task SaveAsync(string name, int? exceptId, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Hai request đồng thời cùng qua bước kiểm tra → chỉ mục UNIQUE (TenantId, Name) chặn lại.
            if (await NameTakenAsync(name, exceptId, ct))
            {
                throw DuplicateName(name);
            }
            throw;
        }
    }

    private static FieldValidationException DuplicateName(string name) =>
        new("name", $"Quán đã có món \"{name}\". Vui lòng đặt tên khác.");

    /// <summary>UC10 6b: ảnh sai định dạng hoặc quá 2 MB → lỗi tại ô ảnh, không lưu gì.</summary>
    private async Task<string?> SaveImageAsync(IFormFile? image, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Không xác định được tenant.");
        var result = await _images.SaveAsync(image, tenantId, ct);
        return result.Path ?? throw new FieldValidationException("image", result.Error!);
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
