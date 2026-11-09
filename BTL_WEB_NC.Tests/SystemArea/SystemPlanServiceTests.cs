// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.System.Models;
using BTL_WEB_NC.Areas.System.Services;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.SystemArea;

/// <summary>MH07 – gói dịch vụ (R07, BM02): thêm, sửa, trùng tên, ngừng/cung cấp lại.</summary>
public class SystemPlanServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task ThemGoi_LuuDuThongTin_TenDuocCatKhoangTrang()
    {
        using var context = _db.CreateContext(tenantId: null);
        var service = new SystemPlanService(context);

        var id = await service.SavePlanAsync(Form(null, "  Chuoi  ", trialDays: 14));

        var plan = await service.GetPlanByIdAsync(id);
        Assert.NotNull(plan);
        Assert.Equal("Chuoi", plan!.Name);
        Assert.Equal(5, plan.MaxBranches);
        Assert.Equal(14, plan.TrialDays);
    }

    [Theory]
    [InlineData("Chuoi")]
    [InlineData("CHUOI")]
    [InlineData("chuoi ")]
    public async Task ThemGoi_TrungTenKhongPhanBietHoaThuong_BiTuChoi(string name)
    {
        // Tên chữ không dấu: SQLite lower() chỉ đổi chữ ASCII; SQL Server so sánh không phân biệt hoa thường cho cả chữ có dấu.
        using var context = _db.CreateContext(tenantId: null);
        var service = new SystemPlanService(context);
        await service.SavePlanAsync(Form(null, "Chuoi"));

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => service.SavePlanAsync(Form(null, name)));

        Assert.Equal("name", ex.Field);
        Assert.Equal(1, await context.ServicePlans.CountAsync(p => p.Name == "Chuoi"));
    }

    [Fact]
    public async Task SuaGoi_GiuTenCuCuaChinhNo_DuocPhep_TrungGoiKhac_BiTuChoi()
    {
        using var context = _db.CreateContext(tenantId: null);
        var service = new SystemPlanService(context);
        var a = await service.SavePlanAsync(Form(null, "Goi A"));
        await service.SavePlanAsync(Form(null, "Goi B"));

        await service.SavePlanAsync(Form(a, "GOI A", maxBranches: 7)); // đổi hoa thường tên của chính nó: được

        Assert.Equal(7, (await service.GetPlanByIdAsync(a))!.MaxBranches);
        await Assert.ThrowsAsync<FieldValidationException>(() => service.SavePlanAsync(Form(a, "goi b")));
    }

    [Fact]
    public async Task SuaGoi_KhongTonTai_404()
    {
        using var context = _db.CreateContext(tenantId: null);

        await Assert.ThrowsAsync<NotFoundException>(() => new SystemPlanService(context).SavePlanAsync(Form(999, "Bat ky")));
    }

    [Fact]
    public async Task NgungVaCungCapLai_DaoTrangThai()
    {
        using var context = _db.CreateContext(tenantId: null);
        var service = new SystemPlanService(context);
        var id = await service.SavePlanAsync(Form(null, "Tam ngung"));

        await service.ToggleActiveAsync(id);
        Assert.False((await service.GetPlanByIdAsync(id))!.IsActive);

        await service.ToggleActiveAsync(id);
        Assert.True((await service.GetPlanByIdAsync(id))!.IsActive);
    }

    [Fact]
    public async Task NgungCungCap_KhongTonTai_404()
    {
        using var context = _db.CreateContext(tenantId: null);

        await Assert.ThrowsAsync<NotFoundException>(() => new SystemPlanService(context).ToggleActiveAsync(999));
    }

    [Fact]
    public async Task DanhSachGoi_SapTheoGiaTangDan_GomCaGoiDangNgung()
    {
        using var context = _db.CreateContext(tenantId: null);
        var service = new SystemPlanService(context);
        var off = await service.SavePlanAsync(Form(null, "Dat", price: 900000));
        await service.ToggleActiveAsync(off);
        await service.SavePlanAsync(Form(null, "Vua", price: 200000));

        var prices = (await service.GetAllPlansAsync()).Select(p => p.PricePerMonth).ToList();

        Assert.Equal(prices.OrderBy(p => p), prices);
        Assert.Contains(900000m, prices);
    }

    private static ServicePlanFormModel Form(int? id, string name, int maxBranches = 5, int? trialDays = null, decimal price = 0) => new()
    {
        Id = id, Name = name, MaxBranches = maxBranches, MaxStaff = 10, PricePerMonth = price, TrialDays = trialDays, IsActive = true
    };

    public void Dispose() => _db.Dispose();
}
