// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>MH13 – A09 và hóa đơn trong kỳ (P08, R20, BM09). Số liệu tính tay ở ReportTestData.</summary>
public class ReportServiceTests
{
    private static readonly DateOnly D = new(2026, 10, 19);

    [Fact]
    public async Task MotNgay_KhopSoTinhTay()
    {
        using var testDb = new TestDb();
        ReportTestData.Seed(testDb, D);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var report = await new ReportService(db).RevenueAsync(new ReportQuery { From = D, To = D });

        // Chỉ 3 đơn Đã thanh toán trong ngày D: 80.000 + 29.000 + 90.000 (đơn 23:59:59 vẫn tính).
        Assert.Equal(new RevenueSummary(199_000, 3, 10_000, 1), report.Summary);
        Assert.Equal(66_333m, report.Summary.Average); // 199.000 / 3 làm tròn về đồng
        // Kỳ trước (D−1): 1 đơn 29.000 lúc 23:59.
        Assert.Equal(new RevenueSummary(29_000, 1, 0, 0), report.Previous);
        Assert.Equal(586.2m, report.Change.Revenue);   // (199.000 − 29.000) / 29.000
        Assert.Equal(200.0m, report.Change.Orders);
        Assert.Equal(128.7m, report.Change.Average);   // (66.333 − 29.000) / 29.000

        Assert.Equal(new[] { ("Cầu Giấy", 109_000m, 54.8m), ("Hoàn Kiếm", 90_000m, 45.2m) },
            report.Branches.Select(b => (b.Name, b.Revenue, b.Share)));

        // Top món: bỏ món hủy (Sinh tố bơ), bỏ đơn chưa thanh toán; doanh thu = đơn giá × số lượng.
        Assert.Equal(new[] { (1, "Trà đào cam sả", 2, 90_000m), (2, "Cà phê sữa đá", 3, 87_000m), (3, "Bạc xỉu", 1, 32_000m) },
            report.TopProducts.Select(p => (p.Rank, p.Name, p.Quantity, p.Revenue)));
    }

    [Fact]
    public async Task TheoNgay_DuMoiNgay_NgayKhongBanBang0_KyTruocCungDoDai()
    {
        using var testDb = new TestDb();
        ReportTestData.Seed(testDb, D);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var report = await new ReportService(db).RevenueAsync(new ReportQuery { From = D.AddDays(-2), To = D });

        Assert.Equal((D.AddDays(-5), D.AddDays(-3)), (report.PreviousFrom, report.PreviousTo));
        Assert.Equal(new[] { (D.AddDays(-2), 0m), (D.AddDays(-1), 29_000m), (D, 199_000m) },
            report.Days.Select(x => (x.Date, x.Revenue)));
        Assert.All(report.Days, x => Assert.Equal(0m, x.PreviousRevenue));
        Assert.Null(report.Change.Revenue); // kỳ trước = 0 → "Mới phát sinh"
    }

    [Fact]
    public async Task KhongLanSoLieuQuanKhac()
    {
        using var testDb = new TestDb();
        var ids = ReportTestData.Seed(testDb, D);

        await using (var db = testDb.CreateContext(TestDb.TenantB))
        {
            var report = await new ReportService(db).RevenueAsync(new ReportQuery { From = D, To = D });
            Assert.Equal(new RevenueSummary(350_000, 1, 0, 0), report.Summary);
            Assert.Equal("Trà sữa", Assert.Single(report.TopProducts).Name);
        }

        await using var dbA = testDb.CreateContext(TestDb.TenantA);
        var service = new ReportService(dbA);
        await Assert.ThrowsAsync<NotFoundException>(() => service.RevenueAsync(new ReportQuery { From = D, To = D, BranchId = ids.BranchB }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.OrdersAsync(new ReportQuery { From = D, To = D, BranchId = ids.BranchB }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.OrderDetailAsync(ids.OrderB));
    }

    [Fact]
    public async Task LocMotChiNhanh()
    {
        using var testDb = new TestDb();
        var ids = ReportTestData.Seed(testDb, D);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var report = await new ReportService(db).RevenueAsync(new ReportQuery { From = D, To = D, BranchId = ids.BranchHK });

        Assert.Equal(new RevenueSummary(90_000, 1, 0, 0), report.Summary);
        Assert.Equal(100m, Assert.Single(report.Branches).Share);
    }

    [Theory]
    [InlineData(0, -1, "to")]     // from > to
    [InlineData(-366, 0, "from")] // 367 ngày
    public void KhoangNgayKhongHopLe_LoiTaiO(int fromOffset, int toOffset, string field)
    {
        using var testDb = new TestDb();
        using var db = testDb.CreateContext(TestDb.TenantA);

        var ex = Assert.Throws<FieldValidationException>(() =>
            new ReportService(db).ResolvePeriod(new ReportQuery { From = D.AddDays(fromOffset), To = D.AddDays(toOffset) }));
        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public void KhoangDung366Ngay_HopLe_ThieuNgay_Lay14NgayGanNhat()
    {
        using var testDb = new TestDb();
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new ReportService(db);

        Assert.Equal(366, service.ResolvePeriod(new ReportQuery { From = D.AddDays(-365), To = D }).Days);
        var period = service.ResolvePeriod(new ReportQuery());
        Assert.Equal((VietnamTime.Today.AddDays(-13), VietnamTime.Today), (period.From, period.To));
    }

    [Fact]
    public async Task DanhSachHoaDon_ChiDaThanhToanTrongKy_MoiNhatTruoc()
    {
        using var testDb = new TestDb();
        ReportTestData.Seed(testDb, D);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var list = await new ReportService(db).OrdersAsync(new ReportQuery { From = D, To = D });

        Assert.Equal(new[] { "HD-HK-1", "HD-CG-2", "HD-CG-1" }, list.Items.Select(o => o.Code));
        Assert.Equal("Trần Văn Nam", list.Items[0].CashierName);
        Assert.Equal(10_000m, list.Items[2].Discount);
    }

    [Fact]
    public async Task ChiTietHoaDon_TheoBM06_CoMonHuyVaTienThua()
    {
        using var testDb = new TestDb();
        var ids = ReportTestData.Seed(testDb, D);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var detail = await new ReportService(db).OrderDetailAsync(ids.OrderDiscounted);

        Assert.Equal(("Quán A", "Cầu Giấy", "Bàn 03", "Tầng 1"), (detail.TenantName, detail.BranchName, detail.TableName, detail.ZoneName));
        Assert.Equal((90_000m, 10_000m, 80_000m, "Khách quen"), (detail.SubTotal, detail.Discount, detail.Total, detail.DiscountReason));
        Assert.Equal(PaymentMethod.Cash, detail.PaymentMethod);
        Assert.Equal(20_000m, detail.ChangeDue);
        Assert.Equal(new[] { 58_000m, 32_000m }, detail.Lines.Select(l => l.Amount));
    }

    [Fact]
    public async Task TongQuan_HomNaySoVoiHomQua_NguyenLieuCanNhap()
    {
        using var testDb = new TestDb();
        var today = VietnamTime.Today;
        var ids = ReportTestData.Seed(testDb, today);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        var model = await Dashboard(db).GetAsync(branchId: null);

        Assert.Equal(new RevenueSummary(199_000, 3, 10_000, 1), model.TodaySummary);
        Assert.Equal(new RevenueSummary(29_000, 1, 0, 0), model.YesterdaySummary);
        // Âm kho trước; dưới mức xếp theo tỷ lệ tồn/mức (Trà đen 60% < Cà phê 64%). "Đào ngâm" đã xóa không hiện; không có dòng quán B.
        Assert.Equal(new[] { ("Sữa đặc", "Cầu Giấy"), ("Trà đen", "Hoàn Kiếm"), ("Cà phê bột", "Cầu Giấy") },
            model.LowStock.Select(r => (r.IngredientName, r.BranchName)));
        Assert.Equal(1, model.NegativeCount);

        var hk = await Dashboard(db).GetAsync(ids.BranchHK);
        Assert.Equal(new RevenueSummary(90_000, 1, 0, 0), hk.TodaySummary);
        Assert.Equal("Trà đen", Assert.Single(hk.LowStock).IngredientName);

        // Chi nhánh quán khác trên URL → về "Tất cả chi nhánh" của quán mình.
        var other = await Dashboard(db).GetAsync(ids.BranchB);
        Assert.Null(other.BranchId);
        Assert.Equal(199_000m, other.TodaySummary.Revenue);
    }

    private static DashboardService Dashboard(BTL_WEB_NC.Data.AppDbContext db)
    {
        var tenant = new TenantContext { TenantId = TestDb.TenantA };
        return new DashboardService(db, new ReportService(db), new PlanUsageService(db, tenant), new InventoryService(db));
    }
}
