// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Areas.System.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_WEB_NC.Tests.SystemArea;

/// <summary>
/// MH06 – phần chưa có ở SystemTenantServiceTests (D2): lọc/tìm/phân trang A03 (R05), kích hoạt và 404 ở A04 (R06),
/// đổi gói A05 (R08) khi vượt số chi nhánh, gói hoặc tenant không tồn tại.
/// </summary>
public class SystemTenantListAndPlanTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _context;
    private readonly SystemTenantService _service;

    public SystemTenantListAndPlanTests()
    {
        _provider = IdentityTestHost.Create(_db);
        _scope = _provider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _service = new SystemTenantService(_context, _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
    }

    // ---------------------------------------------------------------- A03: lọc, tìm, phân trang

    [Fact]
    public async Task TimTheoSlugVaSoDienThoai()
    {
        await SetTenantAsync(TestDb.TenantB, t => t.Phone = "0987654321");

        var bySlug = await _service.GetPagedTenantsAsync(new TenantFilterModel { Keyword = " QUAN-A " });
        var byPhone = await _service.GetPagedTenantsAsync(new TenantFilterModel { Keyword = "098765" });

        Assert.Equal(TestDb.TenantA, Assert.Single(bySlug.Items).Id);
        Assert.Equal(TestDb.TenantB, Assert.Single(byPhone.Items).Id);
    }

    [Fact]
    public async Task LocTheoTrangThaiVaGoi()
    {
        var chain = new ServicePlan { Name = "Chuỗi", MaxBranches = 10, MaxStaff = 50, PricePerMonth = 499000 };
        _context.ServicePlans.Add(chain);
        await _context.SaveChangesAsync();
        await SetTenantAsync(TestDb.TenantB, t => { t.Status = TenantStatus.Locked; t.ServicePlanId = chain.Id; });

        var locked = await _service.GetPagedTenantsAsync(new TenantFilterModel { Status = TenantStatus.Locked });
        var onChain = await _service.GetPagedTenantsAsync(new TenantFilterModel { PlanId = chain.Id });
        var activeOnChain = await _service.GetPagedTenantsAsync(new TenantFilterModel { Status = TenantStatus.Active, PlanId = chain.Id });

        Assert.Equal(TestDb.TenantB, Assert.Single(locked.Items).Id);
        Assert.Equal(TestDb.TenantB, Assert.Single(onChain.Items).Id);
        Assert.Empty(activeOnChain.Items);
    }

    [Fact]
    public async Task PhanTrang_8QuanMotTrang_MoiNhatTruoc()
    {
        var plan = await _context.ServicePlans.FirstAsync();
        var start = new DateTime(2026, 1, 1);
        for (var i = 1; i <= 10; i++)
        {
            _context.Tenants.Add(new Tenant
            {
                Name = $"Quán {i}", Slug = $"quan-so-{i}", ServicePlanId = plan.Id,
                Status = TenantStatus.Active, CreatedAt = start.AddDays(i)
            });
        }
        await _context.SaveChangesAsync();

        var page1 = await _service.GetPagedTenantsAsync(new TenantFilterModel { Keyword = "quan-so-", Page = 1 });
        var page2 = await _service.GetPagedTenantsAsync(new TenantFilterModel { Keyword = "quan-so-", Page = 2 });

        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(8, page1.Items.Count);
        Assert.Equal("quan-so-10", page1.Items[0].Slug);
        Assert.Equal(new[] { "quan-so-2", "quan-so-1" }, page2.Items.Select(t => t.Slug));
    }

    [Fact]
    public async Task TrangKhongHopLe_VeTrang1()
    {
        var result = await _service.GetPagedTenantsAsync(new TenantFilterModel { Page = -5 });

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Items.Count);
    }

    // ---------------------------------------------------------------- A04: kích hoạt, 404

    [Fact]
    public async Task KichHoatQuanChoDuyet_ThanhHoatDong()
    {
        await SetTenantAsync(TestDb.TenantA, t => t.Status = TenantStatus.PendingActivation);

        await _service.UpdateStatusAsync(TestDb.TenantA, TenantStatus.Active, null);

        using var fresh = _db.CreateContext(tenantId: null);
        var tenant = await fresh.Tenants.SingleAsync(t => t.Id == TestDb.TenantA);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.LockReason);
    }

    [Fact]
    public async Task DoiTrangThai_TenantKhongTonTai_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateStatusAsync(999, TenantStatus.Locked, "x"));
    }

    // ---------------------------------------------------------------- A05: đổi gói

    [Fact]
    public async Task DoiGoi_SoChiNhanhVuotGioiHan_BiChan()
    {
        await AddBranchesAsync(TestDb.TenantA, 2);
        var oneBranch = await AddPlanAsync(maxBranches: 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ChangePlanAsync(TestDb.TenantA, oneBranch.Id));

        Assert.Contains("2 chi nhánh", ex.Message);
    }

    [Fact]
    public async Task DoiGoi_LenGoiLonHon_LuuGoiMoi()
    {
        await AddBranchesAsync(TestDb.TenantA, 2);
        var bigger = await AddPlanAsync(maxBranches: 5);

        await _service.ChangePlanAsync(TestDb.TenantA, bigger.Id);

        using var fresh = _db.CreateContext(tenantId: null);
        Assert.Equal(bigger.Id, (await fresh.Tenants.SingleAsync(t => t.Id == TestDb.TenantA)).ServicePlanId);
    }

    [Fact]
    public async Task DoiGoi_TenantHoacGoiKhongTonTai_404()
    {
        var plan = await _context.ServicePlans.FirstAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ChangePlanAsync(999, plan.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ChangePlanAsync(TestDb.TenantA, 999));
    }

    // ---------------------------------------------------------------- Hỗ trợ

    private async Task SetTenantAsync(int tenantId, Action<Tenant> change)
    {
        change(await _context.Tenants.SingleAsync(t => t.Id == tenantId));
        await _context.SaveChangesAsync();
    }

    private async Task<ServicePlan> AddPlanAsync(int maxBranches)
    {
        var plan = new ServicePlan { Name = $"Gói {Guid.NewGuid():N}", MaxBranches = maxBranches, MaxStaff = 10, PricePerMonth = 0 };
        _context.ServicePlans.Add(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    /// <summary>Chi nhánh là ITenantEntity → thêm qua context của chính tenant đó (SaveChanges tự gán TenantId).</summary>
    private async Task AddBranchesAsync(int tenantId, int count)
    {
        using var db = _db.CreateContext(tenantId);
        for (var i = 1; i <= count; i++)
        {
            db.Branches.Add(new Branch
            {
                Name = $"Chi nhánh {i}", Slug = $"cn-{i}", Address = "Hà Nội",
                OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0)
            });
        }
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _db.Dispose();
    }
}
