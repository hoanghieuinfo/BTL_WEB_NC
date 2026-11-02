// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>
/// MH14/A10 – Sơ đồ bàn. Dữ liệu tính tay được:
/// Quán A – Cầu Giấy (CG): khu "Tầng 1" (SortOrder 1): Bàn 01 Trống (2 chỗ), Bàn 02 Đang phục vụ (4 chỗ), Bàn 03 ngừng dùng;
///   khu "Sân vườn" (SortOrder 2): Bàn 04 Chờ thanh toán.
///   Đơn Bàn 02: Đang phục vụ 90.000 (hiện); Bàn 01 có một đơn Đã thanh toán và một đơn Đã hủy (không hiện).
/// Quán A – Hoàn Kiếm (HK): khu "Tầng 1": Bàn 09. Quán B – Thanh Xuân (TX): khu "Trong nhà": Bàn 01.
/// </summary>
public class TableMapServiceTests
{
    private sealed record Ids(int CauGiay, int HoanKiem, int ThanhXuan, long ServingOrder);

    private static Ids Seed(TestDb testDb)
    {
        using var db = testDb.CreateContext(tenantId: null);
        const int a = TestDb.TenantA, b = TestDb.TenantB;

        var staffA = NewUser("nv-a", a);
        var staffB = NewUser("nv-b", b);
        db.Users.AddRange(staffA, staffB);

        var cg = NewBranch(a, "Cầu Giấy", "cau-giay");
        var hk = NewBranch(a, "Hoàn Kiếm", "hoan-kiem");
        var tx = NewBranch(b, "Thanh Xuân", "thanh-xuan");

        var tang1 = NewZone(a, cg, "Tầng 1", 1);
        var sanVuon = NewZone(a, cg, "Sân vườn", 2);   // thêm sau nhưng SortOrder lớn hơn
        var tang1Hk = NewZone(a, hk, "Tầng 1", 1);
        var trongNha = NewZone(b, tx, "Trong nhà", 1);

        var ban01 = NewTable(a, tang1, "Bàn 01", 2, TableStatus.Empty);
        var ban02 = NewTable(a, tang1, "Bàn 02", 4, TableStatus.Serving);
        var ban03 = NewTable(a, tang1, "Bàn 03", 4, TableStatus.Empty, active: false);
        var ban04 = NewTable(a, sanVuon, "Bàn 04", null, TableStatus.AwaitingPayment);
        var ban09 = NewTable(a, tang1Hk, "Bàn 09", 2, TableStatus.Serving);
        var banTx = NewTable(b, trongNha, "Bàn 01", 2, TableStatus.Serving);

        // Thêm bàn theo thứ tự đảo để chứng minh service sắp xếp chứ không phụ thuộc thứ tự lưu.
        db.AddRange(cg, hk, tx, sanVuon, tang1, tang1Hk, trongNha, ban04, ban02, ban01, ban03, ban09, banTx);

        var serving = NewOrder(a, cg, ban02, "HD-CG-1", staffA, OrderStatus.Serving, 90_000);
        db.Orders.AddRange(
            serving,
            NewOrder(a, cg, ban01, "HD-CG-2", staffA, OrderStatus.Paid, 50_000),
            NewOrder(a, cg, ban01, "HD-CG-3", staffA, OrderStatus.Cancelled, 30_000),
            NewOrder(a, hk, ban09, "HD-HK-1", staffA, OrderStatus.Serving, 70_000),
            NewOrder(b, tx, banTx, "HD-TX-1", staffB, OrderStatus.Serving, 120_000));

        db.SaveChanges();
        return new Ids(cg.Id, hk.Id, tx.Id, serving.Id);
    }

    [Fact]
    public async Task GetTables_GroupsByZoneSortedBySortOrder_AndTablesByName_SkippingInactive()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var zones = await new TableMapService(db).GetTablesAsync(ids.CauGiay);

        Assert.Equal(new[] { "Tầng 1", "Sân vườn" }, zones.Select(z => z.Name));
        Assert.Equal(new[] { "Bàn 01", "Bàn 02" }, zones[0].Tables.Select(t => t.Name));   // Bàn 03 ngừng dùng
        Assert.Equal(new[] { "Bàn 04" }, zones[1].Tables.Select(t => t.Name));
    }

    [Fact]
    public async Task GetTables_ReturnsStoredStatusAndSeats()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var tables = (await new TableMapService(db).GetTablesAsync(ids.CauGiay))
            .SelectMany(z => z.Tables).ToDictionary(t => t.Name);

        Assert.Equal(TableStatus.Empty, tables["Bàn 01"].Status);
        Assert.Equal(2, tables["Bàn 01"].Seats);
        Assert.Equal(TableStatus.Serving, tables["Bàn 02"].Status);
        Assert.Equal(TableStatus.AwaitingPayment, tables["Bàn 04"].Status);
        Assert.Null(tables["Bàn 04"].Seats);
    }

    [Fact]
    public async Task GetTables_AttachesOnlyServingOrder()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var tables = (await new TableMapService(db).GetTablesAsync(ids.CauGiay))
            .SelectMany(z => z.Tables).ToDictionary(t => t.Name);

        Assert.Equal(ids.ServingOrder, tables["Bàn 02"].OrderId);
        Assert.Equal(90_000m, tables["Bàn 02"].OrderTotal);
        // Bàn 01 chỉ có đơn Đã thanh toán và Đã hủy → không gắn đơn nào.
        Assert.Null(tables["Bàn 01"].OrderId);
        Assert.Null(tables["Bàn 01"].OrderTotal);
    }

    [Fact]
    public async Task GetTables_OtherBranchOfSameTenant_IsNotIncluded()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);

        var zones = await new TableMapService(db).GetTablesAsync(ids.HoanKiem);

        var table = Assert.Single(Assert.Single(zones).Tables);
        Assert.Equal("Bàn 09", table.Name);
    }

    [Fact]
    public async Task GetTables_BranchOfAnotherTenant_ReturnsNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var dbB = testDb.CreateContext(TestDb.TenantB);

        // Nhân viên quán B không thấy gì dù biết Id chi nhánh của quán A (R38).
        Assert.Empty(await new TableMapService(dbB).GetTablesAsync(ids.CauGiay));
        // Và vẫn thấy bàn của chính quán mình.
        Assert.Single(await new TableMapService(dbB).GetTablesAsync(ids.ThanhXuan));
    }

    [Fact]
    public async Task GetTables_NoTenantInContext_ReturnsNothing()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(tenantId: null);

        Assert.Empty(await new TableMapService(db).GetTablesAsync(ids.CauGiay));
    }

    [Fact]
    public async Task GetHeader_ReturnsTenantAndBranchNames_AndBlankForForeignBranch()
    {
        using var testDb = new TestDb();
        var ids = Seed(testDb);
        using var db = testDb.CreateContext(TestDb.TenantA);
        var service = new TableMapService(db);

        var header = await service.GetHeaderAsync(TestDb.TenantA, ids.CauGiay);
        Assert.Equal("Quán A", header.TenantName);
        Assert.Equal("Cầu Giấy", header.BranchName);

        var foreign = await service.GetHeaderAsync(TestDb.TenantA, ids.ThanhXuan);   // chi nhánh của quán B
        Assert.Equal("", foreign.BranchName);
    }

    // ---------------------------------------------------------------- Dữ liệu mẫu

    private static AppUser NewUser(string id, int tenantId) => new()
    {
        Id = id, UserName = id + "@test", NormalizedUserName = (id + "@TEST").ToUpperInvariant(), Email = id + "@test",
        NormalizedEmail = (id + "@TEST").ToUpperInvariant(), FullName = "Nhân viên " + id, TenantId = tenantId, IsActive = true
    };

    private static Branch NewBranch(int tenantId, string name, string slug) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = "Hà Nội", Phone = "0241234567",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    private static Zone NewZone(int tenantId, Branch branch, string name, int sortOrder) =>
        new() { TenantId = tenantId, Branch = branch, Name = name, SortOrder = sortOrder };

    private static DiningTable NewTable(int tenantId, Zone zone, string name, int? seats, TableStatus status, bool active = true) =>
        new() { TenantId = tenantId, Zone = zone, Name = name, Seats = seats, Status = status, IsActive = active };

    private static Order NewOrder(int tenantId, Branch branch, DiningTable table, string code, AppUser staff,
        OrderStatus status, decimal subTotal) => new()
    {
        TenantId = tenantId, Branch = branch, Table = table, Code = code, Status = status, CreatedById = staff.Id,
        CreatedAt = DateTime.UtcNow, SubTotal = subTotal, Total = subTotal,
        CancelReason = status == OrderStatus.Cancelled ? "Khách rời đi" : null
    };
}
