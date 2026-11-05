// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Models.Entities;

namespace BTL_WEB_NC.Areas.System.Services;

public interface ISystemPlanService
{
    Task<IReadOnlyList<ServicePlan>> GetAllPlansAsync(CancellationToken ct = default);
    Task<ServicePlan?> GetPlanByIdAsync(int id, CancellationToken ct = default);
    Task<int> SavePlanAsync(ServicePlanFormModel model, CancellationToken ct = default);
    Task ToggleActiveAsync(int id, CancellationToken ct = default);
}
