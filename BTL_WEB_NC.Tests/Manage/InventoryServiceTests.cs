// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>MH12 – tồn kho theo chi nhánh và phiếu nhập kho (P06, R19, BM07).</summary>
public class InventoryServiceTests
{
    // ------------------------------------------------------------------ Nhập kho

    [Fact]
    public async Task NhapKho_CongTonSauQuyDoi_TaoDongTonNeuChuaCo_CongDonHaiDong()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Service(db).CreateReceiptAsync(Receipt(ids.BranchA,
                Line(ids.CoffeeA, "kg", 1000, 5, 180_000),        // +5000 g
                Line(ids.MilkA, "hộp", 380, 24, 25_000),          // +9120 ml
                Line(ids.MilkA, "ml", 1, 250.5m, 60),             // cùng nguyên liệu, khác đơn vị: +250,5 ml
                Line(ids.AvocadoA, "kg", 1000, 1.25m, 70_000)),   // Bơ chưa có dòng tồn ở Cầu Giấy: +1250 g
                ids.OwnerA);
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(6000m, await Qty(check, ids.BranchA, ids.CoffeeA));
        Assert.Equal(500m + 9120m + 250.5m, await Qty(check, ids.BranchA, ids.MilkA));
        Assert.Equal(1250m, await Qty(check, ids.BranchA, ids.AvocadoA));
    }

    [Fact]
    public async Task NhapKho_TongTienTinhLaiOServer_LuuNguoiNhapVaChiTiet()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        int receiptId;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var created = await Service(db).CreateReceiptAsync(Receipt(ids.BranchA,
                Line(ids.CoffeeA, "kg", 1000, 5, 180_000),       // 900.000
                Line(ids.MilkA, "hộp", 380, 24, 25_000),         // 600.000
                Line(ids.IceA, "kg", 1000, 2.5m, 3_333)),        // 8.332,5 → làm tròn 8.333
                ids.OwnerA);
            receiptId = created.Id;
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        var receipt = await check.StockReceipts.Include(r => r.Items).SingleAsync(r => r.Id == receiptId);
        Assert.Equal(1_508_333m, receipt.TotalAmount);
        Assert.Equal(ids.OwnerA, receipt.CreatedById);
        Assert.Equal(TestDb.TenantA, receipt.TenantId);
        Assert.Equal(3, receipt.Items.Count);
        Assert.All(receipt.Items, i => Assert.Equal(TestDb.TenantA, i.TenantId));
    }

    [Fact]
    public async Task SoPhieu_TangDanTheoThang_MoiQuanMotDaySo()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        var prefix = $"PN-{VietnamTime.Now:yyMM}-";

        string first, second, otherTenant;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            first = (await Service(db).CreateReceiptAsync(Receipt(ids.BranchA, Line(ids.CoffeeA, "g", 1, 1, 0)), ids.OwnerA)).Code;
            second = (await Service(db).CreateReceiptAsync(Receipt(ids.BranchA, Line(ids.CoffeeA, "g", 1, 1, 0)), ids.OwnerA)).Code;
        }
        await using (var db = testDb.CreateContext(TestDb.TenantB))
        {
            otherTenant = (await Service(db).CreateReceiptAsync(Receipt(ids.BranchB, Line(ids.TeaB, "g", 1, 1, 0)), ids.OwnerB)).Code;
        }

        Assert.Equal(prefix + "001", first);
        Assert.Equal(prefix + "002", second);
        Assert.Equal(prefix + "001", otherTenant); // UQ(TenantId, Code): quán B có dãy số riêng
    }

    [Fact]
    public async Task NhapVaoChiNhanhTamNgung_DuocPhep()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Service(db).CreateReceiptAsync(Receipt(ids.BranchA2, Line(ids.CoffeeA, "kg", 1000, 1, 0)), ids.OwnerA);
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(1000m, await Qty(check, ids.BranchA2, ids.CoffeeA));
    }

    [Fact]
    public async Task ChiNhanhCuaQuanKhac_404_KhongTaoPhieu()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                Service(db).CreateReceiptAsync(Receipt(ids.BranchB, Line(ids.CoffeeA, "kg", 1000, 1, 0)), ids.OwnerA));
        }

        await AssertNoReceiptsAsync(testDb);
    }

    [Fact]
    public async Task NguyenLieuCuaQuanKhac_404_KhongTaoPhieu_TonKhongDoi()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            // Dòng đầu hợp lệ, dòng sau trỏ sang nguyên liệu quán B → cả phiếu bị từ chối.
            await Assert.ThrowsAsync<NotFoundException>(() => Service(db).CreateReceiptAsync(
                Receipt(ids.BranchA, Line(ids.CoffeeA, "kg", 1000, 1, 0), Line(ids.TeaB, "kg", 1000, 1, 0)), ids.OwnerA));
        }

        await AssertNoReceiptsAsync(testDb);
        await using var check = testDb.CreateContext(TestDb.TenantA);
        Assert.Equal(1000m, await Qty(check, ids.BranchA, ids.CoffeeA));
        await using var checkB = testDb.CreateContext(TestDb.TenantB);
        Assert.Equal(300m, await Qty(checkB, ids.BranchB, ids.TeaB));
    }

    [Fact]
    public async Task NguyenLieuDaXoaMem_404()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await new IngredientService(db).DeleteAsync(ids.AvocadoA); // Bơ chưa dùng trong món nào
        }

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                Service(db).CreateReceiptAsync(Receipt(ids.BranchA, Line(ids.AvocadoA, "kg", 1000, 1, 0)), ids.OwnerA));
        }
        await AssertNoReceiptsAsync(testDb);
    }

    public static TheoryData<string, ReceiptFormModel> InvalidReceipts()
    {
        var today = VietnamTime.Today;
        return new TheoryData<string, ReceiptFormModel>
        {
            { "lines", new ReceiptFormModel { BranchId = 0, ReceivedDate = today } },
            { "receivedDate", new ReceiptFormModel { BranchId = 0, ReceivedDate = today.AddDays(1), Lines = { Line(0, "g", 1, 1, 0) } } },
            { "lines[0].conversionRate", new ReceiptFormModel { BranchId = 0, ReceivedDate = today, Lines = { Line(0, "g", 0, 1, 0) } } },
            { "lines[1].quantity", new ReceiptFormModel { BranchId = 0, ReceivedDate = today, Lines = { Line(0, "g", 1, 1, 0), Line(0, "g", 1, -2, 0) } } },
            { "lines[0].unitPrice", new ReceiptFormModel { BranchId = 0, ReceivedDate = today, Lines = { Line(0, "g", 1, 1, 1.5m) } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidReceipts))]
    public async Task PhieuKhongHopLe_LoiTaiO_KhongGhiGi(string field, ReceiptFormModel model)
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        model.BranchId = ids.BranchA;
        foreach (var line in model.Lines)
        {
            line.IngredientId = ids.CoffeeA;
        }

        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => Service(db).CreateReceiptAsync(model, ids.OwnerA));
            Assert.Equal(field, ex.Field);
        }
        await AssertNoReceiptsAsync(testDb);
    }

    // ------------------------------------------------------------------ Tồn kho, chi tiết

    [Fact]
    public async Task TonKho_TinhTinhTrang_NguyenLieuChuaCoDongTonHienTon0()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            var milk = await db.BranchStocks.FirstAsync(s => s.BranchId == ids.BranchA && s.IngredientId == ids.MilkA);
            milk.Quantity = -120;
            await db.SaveChangesAsync();
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        var result = await Service(check).StockAsync(new StockListQuery { BranchId = ids.BranchA });

        // Sắp: Âm kho → Dưới mức → Đủ. Bơ chưa có dòng tồn: 0 < 50 → Dưới mức, chưa cập nhật lần nào.
        Assert.Equal(new[] { "Sữa đặc", "Bơ", "Đá viên", "Cà phê bột" }, result.Items.Select(r => r.Name));
        Assert.Equal(StockStatus.Negative, result.Items[0].Status);
        Assert.Equal(StockStatus.Low, result.Items[1].Status);
        Assert.Null(result.Items[1].UpdatedAt);
        Assert.Equal(0m, result.Items[1].Quantity);
        Assert.Equal(StockStatus.Enough, result.Items[3].Status);
        Assert.Equal(new StockSummary(4, 1, 2), result.Summary);
        Assert.Equal(1, result.Summary.Enough);
        Assert.DoesNotContain(result.Items, r => r.Name == "Trà đen");

        var needOnly = await Service(check).StockAsync(new StockListQuery { BranchId = ids.BranchA, NeedOnly = true });
        Assert.Equal(3, needOnly.TotalCount);
    }

    [Fact]
    public async Task TonKhoChiNhanhQuanKhac_404()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);

        await using var db = testDb.CreateContext(TestDb.TenantA);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).StockAsync(new StockListQuery { BranchId = ids.BranchB }));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).StockDetailAsync(ids.BranchB, ids.TeaB));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).StockDetailAsync(ids.BranchA, ids.TeaB));
    }

    [Fact]
    public async Task ChiTietTon_CoMonDungVaLichSuNhap()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Service(db).CreateReceiptAsync(Receipt(ids.BranchA, Line(ids.CoffeeA, "kg", 1000, 2, 180_000)), ids.OwnerA);
            await Service(db).CreateReceiptAsync(Receipt(ids.BranchA2, Line(ids.CoffeeA, "kg", 1000, 9, 180_000)), ids.OwnerA);
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        var detail = await Service(check).StockDetailAsync(ids.BranchA, ids.CoffeeA);

        Assert.Equal(new[] { "Bạc xỉu", "Cà phê sữa" }, detail.UsedIn.Select(u => u.ProductName));
        var line = Assert.Single(detail.History); // chỉ phiếu của Cầu Giấy
        Assert.Equal(2000m, line.BaseQuantity);
        Assert.Equal(3000m, detail.Stock.Quantity);
    }

    [Fact]
    public async Task DanhSachVaChiTietPhieu_ChiCuaQuanMinh()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        int receiptB;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            await Service(db).CreateReceiptAsync(Receipt(ids.BranchA, Line(ids.CoffeeA, "kg", 1000, 5, 180_000)), ids.OwnerA, default);
        }
        await using (var db = testDb.CreateContext(TestDb.TenantB))
        {
            receiptB = (await Service(db).CreateReceiptAsync(Receipt(ids.BranchB, Line(ids.TeaB, "kg", 1000, 1, 90_000)), ids.OwnerB)).Id;
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        var list = await Service(check).ListReceiptsAsync(new ReceiptListQuery { Month = VietnamTime.Today.ToString("yyyy-MM") });
        var row = Assert.Single(list.Items);
        Assert.Equal("Cầu Giấy", row.BranchName);
        Assert.Equal("Nguyễn Thị Lan", row.CreatedBy);
        Assert.Equal(900_000m, list.PeriodTotal);

        await Assert.ThrowsAsync<NotFoundException>(() => Service(check).GetReceiptAsync(receiptB));
    }

    [Fact]
    public async Task ChiTietPhieu_NguyenLieuDaXoaMemVanHienDong()
    {
        using var testDb = new TestDb();
        var ids = InventoryTestData.Seed(testDb);
        int receiptId;
        await using (var db = testDb.CreateContext(TestDb.TenantA))
        {
            receiptId = (await Service(db).CreateReceiptAsync(
                Receipt(ids.BranchA, Line(ids.AvocadoA, "kg", 1000, 1, 70_000), Line(ids.CoffeeA, "kg", 1000, 1, 180_000)), ids.OwnerA)).Id;
            await new IngredientService(db).DeleteAsync(ids.AvocadoA);
        }

        await using var check = testDb.CreateContext(TestDb.TenantA);
        var detail = await Service(check).GetReceiptAsync(receiptId);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(detail.TotalAmount, detail.Lines.Sum(l => l.Amount));
    }

    // ------------------------------------------------------------------ Hàm phụ

    private static InventoryService Service(BTL_WEB_NC.Data.AppDbContext db) => new(db);

    private static ReceiptFormModel Receipt(int branchId, params ReceiptLineModel[] lines) => new()
    {
        BranchId = branchId, ReceivedDate = VietnamTime.Today, Supplier = "Cà phê Phúc Long Sơn", Lines = lines.ToList()
    };

    private static ReceiptLineModel Line(int ingredientId, string unit, decimal rate, decimal quantity, decimal price) => new()
    {
        IngredientId = ingredientId, PurchaseUnit = unit, ConversionRate = rate, Quantity = quantity, UnitPrice = price
    };

    private static async Task<decimal?> Qty(BTL_WEB_NC.Data.AppDbContext db, int branchId, int ingredientId) =>
        await db.BranchStocks.Where(s => s.BranchId == branchId && s.IngredientId == ingredientId)
            .Select(s => (decimal?)s.Quantity).FirstOrDefaultAsync();

    private static async Task AssertNoReceiptsAsync(TestDb testDb)
    {
        await using var db = testDb.CreateContext(tenantId: null);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM StockReceipts").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM StockReceiptItems").SingleAsync());
    }
}
