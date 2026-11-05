// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Areas.System.Models;

/// <summary>MH05 – Dữ liệu trang Tổng quan hệ thống Super Admin (R09, UC06).</summary>
public class SystemDashboardViewModel
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int PendingTenants { get; set; }
    public int LockedTenants { get; set; }

    public IReadOnlyList<MonthlyRegistrationDto> MonthlyRegistrations { get; set; } = new List<MonthlyRegistrationDto>();
    public IReadOnlyList<PlanDistributionDto> PlanDistributions { get; set; } = new List<PlanDistributionDto>();
    public IReadOnlyList<TenantSummaryDto> RecentPendingTenants { get; set; } = new List<TenantSummaryDto>();
}

public record MonthlyRegistrationDto(int Month, int Count);
public record PlanDistributionDto(string PlanName, int Count, double Percentage);

/// <summary>MH06 – Bộ lọc và danh sách Tenant (R05, UC04).</summary>
public class TenantFilterModel
{
    public string? Keyword { get; set; }
    public TenantStatus? Status { get; set; }
    public int? PlanId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
}

public class TenantSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public TenantStatus Status { get; set; }
    public DateTime? PlanExpiresAt { get; set; }
    public string? LockReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public int CurrentBranches { get; set; }
    public int CurrentStaff { get; set; }
}

public class TenantPagedResult
{
    public IReadOnlyList<TenantSummaryDto> Items { get; set; } = new List<TenantSummaryDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class UpdateTenantStatusRequest
{
    [Required]
    public TenantStatus Status { get; set; }
    public string? LockReason { get; set; }
}

public class ChangeTenantPlanRequest
{
    [Required]
    public int PlanId { get; set; }
}

/// <summary>MH07 – Form thêm/sửa gói dịch vụ (R07, UC05).</summary>
public class ServicePlanFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên gói dịch vụ.")]
    [StringLength(50, ErrorMessage = "Tên gói tối đa 50 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số chi nhánh tối đa.")]
    [Range(1, 1000, ErrorMessage = "Số chi nhánh phải lớn hơn 0.")]
    public int MaxBranches { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập số nhân viên tối đa.")]
    [Range(1, 10000, ErrorMessage = "Số nhân viên phải lớn hơn 0.")]
    public int MaxStaff { get; set; } = 3;

    [Required(ErrorMessage = "Vui lòng nhập giá tham khảo.")]
    [Range(0, 100_000_000, ErrorMessage = "Giá tham khảo không hợp lệ.")]
    public decimal PricePerMonth { get; set; }

    [Range(1, 365, ErrorMessage = "Số ngày dùng thử từ 1 đến 365 ngày.")]
    public int? TrialDays { get; set; }

    public bool IsActive { get; set; } = true;
}
