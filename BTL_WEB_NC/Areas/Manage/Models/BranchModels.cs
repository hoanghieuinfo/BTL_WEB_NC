// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Models.Entities;

namespace BTL_WEB_NC.Areas.Manage.Models;

/// <summary>Phiếu thông tin chi nhánh (BM03). Không có TenantId – AppDbContext tự gán.</summary>
public class BranchFormModel : IValidatableObject
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    [Required(ErrorMessage = "Vui lòng nhập tên chi nhánh.")]
    [StringLength(100, ErrorMessage = "Tên chi nhánh tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đường dẫn thực đơn.")]
    [StringLength(60, ErrorMessage = "Đường dẫn tối đa 60 ký tự.")]
    [RegularExpression(SlugPattern, ErrorMessage = "Chỉ dùng chữ thường không dấu, chữ số và dấu gạch ngang (ví dụ: cau-giay).")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
    public string Address { get; set; } = string.Empty;

    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại gồm đúng 10 chữ số.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giờ mở cửa.")]
    public TimeOnly? OpenTime { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giờ đóng cửa.")]
    public TimeOnly? CloseTime { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OpenTime.HasValue && CloseTime.HasValue && CloseTime.Value <= OpenTime.Value)
        {
            yield return new ValidationResult(
                $"Giờ đóng cửa phải sau giờ mở cửa ({OpenTime.Value:HH\\:mm}).", new[] { nameof(CloseTime) });
        }
    }
}

public class ZoneFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên khu vực.")]
    [StringLength(50, ErrorMessage = "Tên khu vực tối đa 50 ký tự.")]
    public string Name { get; set; } = string.Empty;
}

public class ZoneMoveModel
{
    /// <summary>"up" hoặc "down".</summary>
    [Required]
    [RegularExpression("^(up|down)$")]
    public string Direction { get; set; } = "up";
}

public class TableFormModel
{
    /// <summary>Chỉ dùng khi sửa (chuyển bàn sang khu vực khác cùng chi nhánh). Khi thêm, khu vực lấy từ URL.</summary>
    public int? ZoneId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên bàn.")]
    [StringLength(20, ErrorMessage = "Tên bàn tối đa 20 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "Số chỗ ngồi từ 1 đến 50.")]
    public int? Seats { get; set; }

    public bool IsActive { get; set; } = true;
}

public class TableVisibilityModel
{
    public bool IsActive { get; set; }
}

// ------------------------------------------------------------------ Dữ liệu hiển thị

public record BranchRow(
    int Id, string Name, string Slug, string Address, string? Phone, TimeOnly OpenTime, TimeOnly CloseTime,
    bool IsActive, int TableCount, int ZoneCount);

public record BranchListModel(IReadOnlyList<BranchRow> Branches, int? SelectedId, PlanUsage Usage);

public record TableView(int Id, string Name, int? Seats, bool IsActive, TableStatus Status);

public record ZoneView(int Id, string Name, int SortOrder, IReadOnlyList<TableView> Tables)
{
    public int ActiveTableCount => Tables.Count(t => t.IsActive);
}

public record ZoneBoardModel(int BranchId, string BranchName, IReadOnlyList<ZoneView> Zones)
{
    public int ActiveTableCount => Zones.Sum(z => z.ActiveTableCount);
}

public record BranchPageModel(PlanUsage Usage, string TenantSlug, BranchListModel List, ZoneBoardModel? Board);

public record BranchQrInfo(int BranchId, string BranchName, string TenantName, string Url, string FileName);
