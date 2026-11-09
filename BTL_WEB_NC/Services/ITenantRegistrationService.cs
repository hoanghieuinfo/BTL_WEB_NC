// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Models;

namespace BTL_WEB_NC.Services;

public interface ITenantRegistrationService
{
    Task<bool> IsEmailRegisteredAsync(string email, CancellationToken ct = default);
    Task<bool> IsSlugTakenAsync(string slug, CancellationToken ct = default);
    bool IsReservedSlug(string slug);
    Task<int> RegisterAsync(RegisterTenantViewModel model, CancellationToken ct = default);
}
