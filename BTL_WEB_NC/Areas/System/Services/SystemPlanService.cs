// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.System.Services;

public class SystemPlanService : ISystemPlanService
{
    private readonly AppDbContext _db;

    public SystemPlanService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServicePlan>> GetAllPlansAsync(CancellationToken ct = default)
    {
        return await _db.ServicePlans
            .IgnoreQueryFilters()
            .OrderBy(p => p.PricePerMonth)
            .ToListAsync(ct);
    }

    public async Task<ServicePlan?> GetPlanByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.ServicePlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<int> SavePlanAsync(ServicePlanFormModel model, CancellationToken ct = default)
    {
        var cleanName = model.Name.Trim();

        if (model.Id.HasValue && model.Id.Value > 0)
        {
            var plan = await _db.ServicePlans
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == model.Id.Value, ct)
                ?? throw new NotFoundException("Không tìm thấy gói dịch vụ.");

            // Kiểm tra trùng tên với gói khác
            var duplicate = await _db.ServicePlans
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Id != plan.Id && p.Name.ToLower() == cleanName.ToLower(), ct);

            if (duplicate) throw new FieldValidationException("name", "Tên gói dịch vụ đã tồn tại.");

            plan.Name = cleanName;
            plan.MaxBranches = model.MaxBranches;
            plan.MaxStaff = model.MaxStaff;
            plan.PricePerMonth = model.PricePerMonth;
            plan.TrialDays = model.TrialDays;
            plan.IsActive = model.IsActive;

            await _db.SaveChangesAsync(ct);
            return plan.Id;
        }
        else
        {
            var duplicate = await _db.ServicePlans
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Name.ToLower() == cleanName.ToLower(), ct);

            if (duplicate) throw new FieldValidationException("name", "Tên gói dịch vụ đã tồn tại.");

            var newPlan = new ServicePlan
            {
                Name = cleanName,
                MaxBranches = model.MaxBranches,
                MaxStaff = model.MaxStaff,
                PricePerMonth = model.PricePerMonth,
                TrialDays = model.TrialDays,
                IsActive = model.IsActive
            };

            _db.ServicePlans.Add(newPlan);
            await _db.SaveChangesAsync(ct);
            return newPlan.Id;
        }
    }

    public async Task ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var plan = await _db.ServicePlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy gói dịch vụ.");

        plan.IsActive = !plan.IsActive;
        await _db.SaveChangesAsync(ct);
    }
}
