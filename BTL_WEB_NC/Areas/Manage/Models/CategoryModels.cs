// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Areas.Manage.Models;

public class CategoryFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
    [StringLength(50, ErrorMessage = "Tên danh mục tối đa 50 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 999, ErrorMessage = "Thứ tự hiển thị từ 0 đến 999.")]
    public int SortOrder { get; set; }
}

public record CategorySummary(int Id, string Name, int SortOrder, int ProductCount);

/// <summary>
/// Bộ đếm của MH10: "N món trong M danh mục", số món mỗi danh mục, số món chưa định lượng.
/// Trả kèm mọi phản hồi ghi để JS cập nhật giao diện mà không tải lại trang.
/// </summary>
public record MenuSummary(int TotalProducts, int MissingRecipeCount, IReadOnlyList<CategorySummary> Categories)
{
    public int CategoryCount => Categories.Count;
}
