// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>
/// MH09 – chi nhánh, khu vực, bàn. Trọng tâm: giới hạn gói (R11), cô lập tenant với BranchId/ZoneId/TableId (R38),
/// ẩn/tạm ngưng thay vì xóa và không ẩn khi còn đơn đang phục vụ (R12, R42).
/// Quán A dùng gói Chuỗi (5 chi nhánh), quán B gói Cơ bản (1 chi nhánh) – giống dữ liệu mẫu.
/// </summary>
public class BranchServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly int _branchA;
    private readonly int _zoneA1;
    private readonly int _zoneA2;
    private readonly int _servingTableA;
    private readonly int _freeTableA;
    private readonly int _hiddenTableA;
    private readonly int _branchB;
    private readonly int _zoneB;
    private readonly int _tableB;
    private readonly int _otherBranchZoneA;

    public BranchServiceTests()
    {
        using var db = _db.CreateContext(tenantId: null);

        var chain = new ServicePlan { Name = "Chuỗi", MaxBranches = 5, MaxStaff = 50, PricePerMonth = 499_000 };
        db.ServicePlans.Add(chain);
        db.Tenants.Single(t => t.Id == TestDb.TenantA).ServicePlan = chain;

        var cauGiay = NewBranch(TestDb.TenantA, "Cầu Giấy", "cau-giay");
        var hoanKiem = NewBranch(TestDb.TenantA, "Hoàn Kiếm", "hoan-kiem");
        var zoneA1 = new Zone { TenantId = TestDb.TenantA, Branch = cauGiay, Name = "Tầng 1", SortOrder = 1 };
        var zoneA2 = new Zone { TenantId = TestDb.TenantA, Branch = cauGiay, Name = "Sân vườn", SortOrder = 2 };
        var zoneHk = new Zone { TenantId = TestDb.TenantA, Branch = hoanKiem, Name = "Tầng 1", SortOrder = 1 };
        var serving = new DiningTable { TenantId = TestDb.TenantA, Zone = zoneA1, Name = "Bàn 01", Status = TableStatus.Serving, IsActive = true };
        var free = new DiningTable { TenantId = TestDb.TenantA, Zone = zoneA1, Name = "Bàn 02", IsActive = true };
        var hidden = new DiningTable { TenantId = TestDb.TenantA, Zone = zoneA1, Name = "Bàn 08", IsActive = false };

        var thanhXuan = NewBranch(TestDb.TenantB, "Thanh Xuân", "thanh-xuan");
        var zoneB = new Zone { TenantId = TestDb.TenantB, Branch = thanhXuan, Name = "Trong nhà", SortOrder = 1 };
        var tableB = new DiningTable { TenantId = TestDb.TenantB, Zone = zoneB, Name = "Bàn 01", IsActive = true };

        var staff = new AppUser
        {
            Id = "staff-a", UserName = "nv@a.test", NormalizedUserName = "NV@A.TEST", Email = "nv@a.test",
            NormalizedEmail = "NV@A.TEST", FullName = "Nhân viên A", TenantId = TestDb.TenantA, IsActive = true
        };
        var order = new Order
        {
            TenantId = TestDb.TenantA, Branch = cauGiay, Table = serving, Code = "HD-CG-261021-999", Status = OrderStatus.Serving,
            CreatedById = staff.Id, CreatedAt = new DateTime(2026, 10, 21, 9, 0, 0), SubTotal = 0, Total = 0
        };

        db.Users.Add(staff);
        db.AddRange(cauGiay, hoanKiem, zoneA1, zoneA2, zoneHk, serving, free, hidden, thanhXuan, zoneB, tableB, order);
        db.SaveChanges();

        (_branchA, _zoneA1, _zoneA2, _otherBranchZoneA) = (cauGiay.Id, zoneA1.Id, zoneA2.Id, zoneHk.Id);
        (_servingTableA, _freeTableA, _hiddenTableA) = (serving.Id, free.Id, hidden.Id);
        (_branchB, _zoneB, _tableB) = (thanhXuan.Id, zoneB.Id, tableB.Id);
    }

    // ------------------------------------------------------------------ Giới hạn gói (R11)

    [Fact]
    public async Task ThemChiNhanh_GoiCoBanDaDu1Tren1_BiChan_KhongTao()
    {
        using (var db = _db.CreateContext(TestDb.TenantB))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, TestDb.TenantB).CreateBranchAsync(Form("Hà Đông", "ha-dong")));
            Assert.Contains("1/1", ex.Message);
        }

        using var check = _db.CreateContext(TestDb.TenantB);
        Assert.Equal(1, await check.Branches.CountAsync());
    }

    [Fact]
    public async Task ThemChiNhanh_TamNgungVanTinhSuat_DuNamThiBiChan()
    {
        using (var seed = _db.CreateContext(TestDb.TenantA))
        {
            (await seed.Branches.SingleAsync(b => b.Id == _branchA)).IsActive = true;
            seed.Branches.Single(b => b.Slug == "hoan-kiem").IsActive = false; // tạm ngưng vẫn chiếm suất
            await seed.SaveChangesAsync();
        }

        using var db = _db.CreateContext(TestDb.TenantA);
        var service = Service(db, TestDb.TenantA);
        await service.CreateBranchAsync(Form("Tây Hồ", "tay-ho"));
        await service.CreateBranchAsync(Form("Ba Đình", "ba-dinh"));
        await service.CreateBranchAsync(Form("Long Biên", "long-bien")); // 5/5

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBranchAsync(Form("Đống Đa", "dong-da")));
        Assert.Equal(5, await db.Branches.CountAsync());
    }

    // ------------------------------------------------------------------ Cô lập tenant (R38)

    [Fact]
    public async Task ChiNhanhCuaTenantKhac_SuaXemKhuVucThemKhuVuc_Tra404_KhongTao()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = Service(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateBranchAsync(_branchB, Form("Đổi", "doi")));
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetBoardAsync(_branchB));
            await Assert.ThrowsAsync<NotFoundException>(() => service.CreateZoneAsync(_branchB, new ZoneFormModel { Name = "Lén" }));
        }

        using var check = _db.CreateContext(tenantId: null);
        Assert.False(await check.Zones.IgnoreQueryFilters().AnyAsync(z => z.Name == "Lén"));
        Assert.Equal("Thanh Xuân", (await check.Branches.IgnoreQueryFilters().SingleAsync(b => b.Id == _branchB)).Name);
    }

    [Fact]
    public async Task KhuVucVaBanCuaTenantKhac_Tra404_KhongTao_KhongDoi()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = Service(db, TestDb.TenantA);
            await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTableAsync(_zoneB, Table("Bàn lén")));
            await Assert.ThrowsAsync<NotFoundException>(() => service.RenameZoneAsync(_zoneB, new ZoneFormModel { Name = "Đổi" }));
            await Assert.ThrowsAsync<NotFoundException>(() => service.MoveZoneAsync(_zoneB, up: true));
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateTableAsync(_tableB, Table("Đổi")));
            await Assert.ThrowsAsync<NotFoundException>(() => service.SetTableActiveAsync(_tableB, false));
            // Chuyển bàn của A sang khu vực của B
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateTableAsync(_freeTableA, Table("Bàn 02", zoneId: _zoneB)));
        }

        using var check = _db.CreateContext(tenantId: null);
        Assert.False(await check.DiningTables.IgnoreQueryFilters().AnyAsync(t => t.Name == "Bàn lén"));
        var tableB = await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == _tableB);
        Assert.Equal("Bàn 01", tableB.Name);
        Assert.True(tableB.IsActive);
        Assert.Equal(_zoneA1, (await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == _freeTableA)).ZoneId);
    }

    // ------------------------------------------------------------------ Quy tắc chi nhánh

    [Fact]
    public async Task ThemChiNhanh_SlugTrungTrongTenant_LoiTaiOSlug_CoGoiY()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db, TestDb.TenantA).CreateBranchAsync(Form("Ba Đình", "cau-giay")));

        Assert.Equal("slug", ex.Field);
        Assert.Contains("ba-dinh", ex.Message);
    }

    [Fact]
    public async Task ThemChiNhanh_TenTrung_LoiTaiOTen_TrungVoiTenantKhacDuocPhep()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = Service(db, TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => service.CreateBranchAsync(Form("Cầu Giấy", "cau-giay-2")));
        Assert.Equal("name", ex.Field);

        var id = await service.CreateBranchAsync(Form("Thanh Xuân", "thanh-xuan")); // trùng tên/slug với quán B
        Assert.True(id > 0);
    }

    [Theory]
    [InlineData("Cau-Giay")]
    [InlineData("cầu-giấy")]
    [InlineData("cau giay")]
    [InlineData("-cau")]
    public void Slug_SaiDinhDang_LoiValidate(string slug)
    {
        var errors = Validate(Form("Tây Hồ", slug));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(BranchFormModel.Slug)));
    }

    [Fact]
    public void GioDongCua_KhongSauGioMoCua_LoiValidate()
    {
        var model = Form("Tây Hồ", "tay-ho");
        model.OpenTime = new TimeOnly(7, 0);
        model.CloseTime = new TimeOnly(6, 0);

        var errors = Validate(model);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(BranchFormModel.CloseTime)));
    }

    [Theory]
    [InlineData("Bà Đình Đông", "ba-dinh-dong")]
    [InlineData("  Cầu Giấy (cơ sở 2) ", "cau-giay-co-so-2")]
    [InlineData("Phố Đường Láng", "pho-duong-lang")]
    public void GoiYSlug_BoDauTiengViet(string name, string expected)
    {
        Assert.Equal(expected, BranchService.SuggestSlug(name));
    }

    [Fact]
    public async Task TamNgungChiNhanh_ConDonDangPhucVu_BiChan()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var model = Form("Cầu Giấy", "cau-giay");
            model.IsActive = false;
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, TestDb.TenantA).UpdateBranchAsync(_branchA, model));
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        Assert.True((await check.Branches.SingleAsync(b => b.Id == _branchA)).IsActive);
    }

    [Fact]
    public async Task TamNgungChiNhanh_KhongConDon_DuocPhep()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var model = Form("Hoàn Kiếm", "hoan-kiem");
        model.IsActive = false;
        var id = await db.Branches.Where(b => b.Slug == "hoan-kiem").Select(b => b.Id).SingleAsync();

        await Service(db, TestDb.TenantA).UpdateBranchAsync(id, model);

        Assert.False((await db.Branches.SingleAsync(b => b.Id == id)).IsActive);
    }

    // ------------------------------------------------------------------ Khu vực, bàn (R12)

    [Fact]
    public async Task AnBan_DangCoDonPhucVu_BiChan()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, TestDb.TenantA).SetTableActiveAsync(_servingTableA, false));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(db, TestDb.TenantA).UpdateTableAsync(_servingTableA, Table("Bàn 01", isActive: false)));
    }

    [Fact]
    public async Task AnBanTrong_ChiDoiIsActive_KhongXoa()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            await Service(db, TestDb.TenantA).SetTableActiveAsync(_freeTableA, false);
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        var table = await check.DiningTables.SingleAsync(t => t.Id == _freeTableA);
        Assert.False(table.IsActive);
    }

    [Fact]
    public async Task ThemBan_TrungTenVoiBanDangAn_LoiTaiOTen()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db, TestDb.TenantA).CreateTableAsync(_zoneA1, Table("Bàn 08")));

        Assert.Equal("name", ex.Field);
        Assert.Equal("Khu vực Tầng 1 đã có Bàn 08.", ex.Message);
    }

    [Fact]
    public async Task ThemBan_TuGanTenant_VaTrangThaiTrong()
    {
        int id;
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            id = await Service(db, TestDb.TenantA).CreateTableAsync(_zoneA2, Table("SV-01"));
        }

        using var check = _db.CreateContext(tenantId: null);
        var table = await check.DiningTables.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        Assert.Equal(TestDb.TenantA, table.TenantId);
        Assert.Equal(TableStatus.Empty, table.Status);
    }

    [Fact]
    public async Task ChuyenBan_SangKhuVucKhacChiNhanh_BiChan_CungChiNhanhDuocPhep()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = Service(db, TestDb.TenantA);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateTableAsync(_freeTableA, Table("Bàn 02", zoneId: _otherBranchZoneA)));
        await service.UpdateTableAsync(_freeTableA, Table("SV-02", zoneId: _zoneA2));

        Assert.Equal(_zoneA2, (await db.DiningTables.SingleAsync(t => t.Id == _freeTableA)).ZoneId);
    }

    [Fact]
    public async Task DoiThuTuKhuVuc()
    {
        using (var db = _db.CreateContext(TestDb.TenantA))
        {
            var service = Service(db, TestDb.TenantA);
            await service.MoveZoneAsync(_zoneA2, up: true);
            await service.MoveZoneAsync(_zoneA2, up: true); // đã ở đầu → không đổi
        }

        using var check = _db.CreateContext(TestDb.TenantA);
        var board = await Service(check, TestDb.TenantA).GetBoardAsync(_branchA);
        Assert.Equal(new[] { "Sân vườn", "Tầng 1" }, board.Zones.Select(z => z.Name));
    }

    [Fact]
    public async Task ThemKhuVuc_TenTrungTrongChiNhanh_Loi_ChiNhanhKhacDuocPhep()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var service = Service(db, TestDb.TenantA);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => service.CreateZoneAsync(_branchA, new ZoneFormModel { Name = "Tầng 1" }));
        Assert.Equal("name", ex.Field);

        var hoanKiem = await db.Branches.Where(b => b.Slug == "hoan-kiem").Select(b => b.Id).SingleAsync();
        Assert.True(await service.CreateZoneAsync(hoanKiem, new ZoneFormModel { Name = "Ban công" }) > 0);
    }

    [Fact]
    public async Task DanhSachChiNhanh_DemBanDangDungVaKhuVuc_ChiCuaTenant()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var rows = await Service(db, TestDb.TenantA).ListAsync();

        Assert.Equal(new[] { "Cầu Giấy", "Hoàn Kiếm" }, rows.Select(r => r.Name));
        var cauGiay = rows[0];
        Assert.Equal(2, cauGiay.TableCount); // Bàn 08 đang ẩn không tính
        Assert.Equal(2, cauGiay.ZoneCount);
    }

    // ------------------------------------------------------------------ Hàm phụ

    private static BranchService Service(AppDbContext db, int tenantId)
    {
        var tenant = new TenantContext { TenantId = tenantId };
        return new(db, new PlanUsageService(db, tenant), new PlanLimitGuard(db, tenant));
    }

    private static BranchFormModel Form(string name, string slug) => new()
    {
        Name = name, Slug = slug, Address = "Hà Nội", Phone = "0241234567",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static TableFormModel Table(string name, int? zoneId = null, bool isActive = true) =>
        new() { Name = name, Seats = 4, IsActive = isActive, ZoneId = zoneId };

    private static Branch NewBranch(int tenantId, string name, string slug) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = "Hà Nội",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(object model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    public void Dispose() => _db.Dispose();
}
