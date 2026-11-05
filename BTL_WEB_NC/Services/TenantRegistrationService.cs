// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Services;

public class TenantRegistrationService : ITenantRegistrationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    // Danh sách đường dẫn dành riêng của hệ thống không cho tenant sử dụng (mục 3.2.2d)
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "manage", "pos", "api", "dang-ky", "dang-nhap", "tai-khoan",
        "uploads", "css", "js", "lib", "images", "sitemap.xml", "robots.txt",
        "home", "account", "admin", "dashboard", "superadmin"
    };

    public TenantRegistrationService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> IsEmailRegisteredAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToUpperInvariant();
        return await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct);
    }

    public bool IsReservedSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return true;
        return ReservedSlugs.Contains(slug.Trim());
    }

    public async Task<bool> IsSlugTakenAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return true;
        var clean = slug.Trim().ToLowerInvariant();
        if (IsReservedSlug(clean)) return true;

        // Bỏ qua bộ lọc Query Filter để kiểm tra slug trên toàn bộ hệ thống
        return await _db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == clean, ct);
    }

    public async Task<int> RegisterAsync(RegisterTenantViewModel model, CancellationToken ct = default)
    {
        var cleanSlug = model.Slug.Trim().ToLowerInvariant();
        var cleanEmail = model.Email.Trim();

        if (IsReservedSlug(cleanSlug))
            throw new FieldValidationException("slug", "Đường dẫn quán trùng với từ khóa dành riêng của hệ thống.");

        if (await IsSlugTakenAsync(cleanSlug, ct))
            throw new FieldValidationException("slug", "Đường dẫn quán đã được sử dụng. Vui lòng chọn đường dẫn khác.");

        if (await IsEmailRegisteredAsync(cleanEmail, ct))
            throw new FieldValidationException("email", "Địa chỉ email đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác.");

        var plan = await _db.ServicePlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == model.ServicePlanId && p.IsActive, ct)
            ?? throw new FieldValidationException("servicePlanId", "Gói dịch vụ được chọn không hợp lệ hoặc đã ngừng cung cấp.");

        var now = VietnamTime.Now;

        // Đăng ký toàn bộ tenant, chi nhánh mặc định và tài khoản chủ quán trong 1 CSDL Transaction (Bảng 3.3, Bảng 3.22)
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Tạo Tenant mới ở trạng thái Chờ kích hoạt
            var tenant = new Tenant
            {
                Name = model.ShopName.Trim(),
                Slug = cleanSlug,
                Phone = model.PhoneNumber.Trim(),
                ServicePlanId = plan.Id,
                Status = TenantStatus.PendingActivation,
                PlanExpiresAt = plan.TrialDays.HasValue ? now.Date.AddDays(plan.TrialDays.Value) : null,
                CreatedAt = now
            };

            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(ct);

            // 2. Tạo chi nhánh mặc định (TenantId gán tường minh vì request hiện tại chưa có context tenant)
            var defaultBranch = new Branch
            {
                TenantId = tenant.Id,
                Name = "Chi nhánh chính",
                Slug = "chi-nhanh-chinh",
                Address = "Đang cập nhật địa chỉ",
                Phone = model.PhoneNumber.Trim(),
                OpenTime = new TimeOnly(7, 0),
                CloseTime = new TimeOnly(22, 30),
                IsActive = true
            };

            _db.Branches.Add(defaultBranch);
            await _db.SaveChangesAsync(ct);

            // 3. Tạo tài khoản Chủ quán (TenantAdmin)
            var user = new AppUser
            {
                UserName = cleanEmail,
                Email = cleanEmail,
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                TenantId = tenant.Id,
                // Chủ quán quản lý mọi chi nhánh nên không gắn chi nhánh, giống SeedData (claim branch_id chỉ dành cho Staff).
                BranchId = null,
                StartDate = DateOnly.FromDateTime(now),
                IsActive = true,
                MustChangePassword = false
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                var firstError = createResult.Errors.FirstOrDefault()?.Description ?? "Không thể tạo tài khoản người dùng.";
                throw new FieldValidationException("password", firstError);
            }

            // Gán vai trò TenantAdmin
            await _userManager.AddToRoleAsync(user, AppRoles.TenantAdmin);

            await transaction.CommitAsync(ct);
            return tenant.Id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
