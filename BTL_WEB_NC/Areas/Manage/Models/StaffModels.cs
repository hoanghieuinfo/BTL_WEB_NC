// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.ComponentModel.DataAnnotations;
using BTL_WEB_NC.Areas.Manage.Services;

namespace BTL_WEB_NC.Areas.Manage.Models;

/// <summary>
/// Phiếu thông tin nhân viên (BM08). Cố ý KHÔNG có trường vai trò hay TenantId: vai trò luôn là Staff,
/// TenantId lấy từ TenantContext phía server (Bảng 3.22 – chống leo thang quyền). Trường lạ trong JSON bị bỏ qua.
/// </summary>
public class StaffFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại gồm đúng 10 chữ số.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn chi nhánh làm việc.")]
    public int? BranchId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập ngày vào làm.")]
    public DateOnly? StartDate { get; set; }
}

/// <summary>Sửa nhân viên: chỉ họ tên và số điện thoại (BM08). Email, chi nhánh, vai trò không đổi ở đây.</summary>
public class StaffEditModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại gồm đúng 10 chữ số.")]
    public string Phone { get; set; } = string.Empty;
}

public class StaffTransferModel
{
    [Required(ErrorMessage = "Vui lòng chọn chi nhánh mới.")]
    public int? BranchId { get; set; }
}

/// <summary>Tham số lọc danh sách nhân viên (query string).</summary>
public class StaffListQuery
{
    public const int PageSize = 10;

    public string? Q { get; set; }
    public int? BranchId { get; set; }

    /// <summary>"active" (Đang làm), "pending" (Chưa đổi mật khẩu), "locked" (Đã nghỉ) hoặc rỗng.</summary>
    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public bool HasFilter => !string.IsNullOrWhiteSpace(Q) || BranchId.HasValue || !string.IsNullOrWhiteSpace(Status);
}

public record StaffRow(
    string Id, string FullName, string Email, string? Phone, int? BranchId, string? BranchName,
    DateOnly? StartDate, bool IsActive, bool MustChangePassword);

public record StaffListResult(IReadOnlyList<StaffRow> Items, int Page, int TotalCount, StaffListQuery Query)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)StaffListQuery.PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * StaffListQuery.PageSize + 1;
    public int To => Math.Min(Page * StaffListQuery.PageSize, TotalCount);
}

/// <summary>Kết quả tạo tài khoản hoặc đặt lại mật khẩu – mật khẩu khởi tạo chỉ có ở đây, hiện một lần rồi bỏ.</summary>
public record StaffCreatedResult(string Id, string FullName, string Email, string BranchName, string InitialPassword);

public record BranchOption(int Id, string Name, string Address);

public record StaffPageModel(PlanUsage Usage, IReadOnlyList<BranchOption> Branches);
