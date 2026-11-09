// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Models.Entities;

namespace BTL_WEB_NC.Areas.System.Services;

public interface ISystemTenantService
{
    Task<TenantPagedResult> GetPagedTenantsAsync(TenantFilterModel filter, CancellationToken ct = default);
    Task<TenantSummaryDto?> GetTenantDetailAsync(int id, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, TenantStatus status, string? lockReason, CancellationToken ct = default);
    Task ChangePlanAsync(int id, int newPlanId, CancellationToken ct = default);
    /// <summary>Gia hạn gói (A31): đặt ngày hết hạn mới, phải sau hôm nay.</summary>
    Task ExtendExpiryAsync(int id, DateTime newExpiry, CancellationToken ct = default);
    Task<SystemDashboardViewModel> GetDashboardOverviewAsync(CancellationToken ct = default);
}
