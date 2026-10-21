// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Areas.Manage.Models;

/// <summary>Form thêm/sửa món theo BM04a. Không có TenantId: TenantId do AppDbContext tự gán.</summary>
public class ProductFormModel : IValidatableObject
{
    public const decimal MaxPrice = 100_000_000;

    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên món.")]
    [StringLength(100, ErrorMessage = "Tên món tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập giá bán.")]
    [Range(1, (double)MaxPrice, ErrorMessage = "Giá bán phải là số nguyên lớn hơn 0 và không quá 100.000.000 đ.")]
    public decimal? Price { get; set; }

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>Ảnh mới (không bắt buộc). Khi sửa mà không chọn ảnh thì giữ ảnh cũ.</summary>
    public IFormFile? Image { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price.HasValue && Price.Value != decimal.Truncate(Price.Value))
        {
            yield return new ValidationResult("Giá bán phải là số nguyên.", new[] { nameof(Price) });
        }
    }
}

public class AvailabilityModel
{
    public bool IsAvailable { get; set; }
}

/// <summary>Tham số lọc của A06 (đều lấy từ query string).</summary>
public class ProductListQuery
{
    public const int PageSize = 6;

    public int? CategoryId { get; set; }

    /// <summary>"selling" (Đang bán), "stopped" (Ngừng bán) hoặc rỗng (mọi trạng thái).</summary>
    public string? Status { get; set; }

    public string? Q { get; set; }

    /// <summary>Chỉ lấy món chưa có định lượng (R15).</summary>
    public bool NoRecipe { get; set; }

    public int Page { get; set; } = 1;

    public bool HasFilter => CategoryId.HasValue || !string.IsNullOrWhiteSpace(Status) || !string.IsNullOrWhiteSpace(Q) || NoRecipe;
}

public record ProductRow(
    int Id, string Name, int CategoryId, string CategoryName, decimal Price, string? ImagePath,
    string? Description, bool IsAvailable, int RecipeCount, int OrderCount)
{
    public bool HasRecipe => RecipeCount > 0;
}

public record ProductListResult(IReadOnlyList<ProductRow> Items, int Page, int TotalCount, ProductListQuery Query)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)ProductListQuery.PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * ProductListQuery.PageSize + 1;
    public int To => Math.Min(Page * ProductListQuery.PageSize, TotalCount);
}

/// <summary>Kết quả xóa: món đã có trong hóa đơn thì chỉ xóa mềm (R42).</summary>
public record ProductDeleteResult(bool SoftDeleted);

// ------------------------------------------------------------------ Định lượng (BM04b)

public class RecipeItemModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên liệu.")]
    public int IngredientId { get; set; }

    [Range(0.001, 1_000_000, ErrorMessage = "Định lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; }
}

public class RecipeSaveModel
{
    public List<RecipeItemModel> Items { get; set; } = new();
}

public record IngredientOption(int Id, string Name, string Unit);

public record RecipeLine(int IngredientId, decimal Quantity);

public record RecipeEditorData(int ProductId, string ProductName, IReadOnlyList<IngredientOption> Ingredients, IReadOnlyList<RecipeLine> Items);
