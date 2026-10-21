// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Mã QR thực đơn công khai (R16).</summary>
public class BranchQrServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly int _branchA;
    private readonly int _branchB;

    public BranchQrServiceTests()
    {
        using var db = _db.CreateContext(tenantId: null);
        var a = NewBranch(TestDb.TenantA, "Cầu Giấy", "cau-giay");
        var b = NewBranch(TestDb.TenantB, "Thanh Xuân", "thanh-xuan");
        db.Branches.AddRange(a, b);
        db.SaveChanges();
        (_branchA, _branchB) = (a.Id, b.Id);
    }

    [Fact]
    public async Task DuongDan_DungMauSlugQuanThucDonSlugChiNhanh()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var info = await new BranchQrService(db, Config(null)).GetInfoAsync(_branchA, "https://localhost:7199");

        Assert.Equal("https://localhost:7199/quan-a/thuc-don/cau-giay", info.Url);
        Assert.Equal("qr-quan-a-cau-giay.png", info.FileName);
    }

    [Fact]
    public async Task DuongDan_UuTienCauHinhPublicSite()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var info = await new BranchQrService(db, Config("https://cafesaas.vn/")).GetInfoAsync(_branchA, "https://localhost:7199");

        Assert.Equal("https://cafesaas.vn/quan-a/thuc-don/cau-giay", info.Url);
    }

    [Fact]
    public async Task ChiNhanhCuaTenantKhac_Tra404()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        await Assert.ThrowsAsync<NotFoundException>(() => new BranchQrService(db, Config(null)).GetInfoAsync(_branchB, "https://x"));
    }

    [Fact]
    public void AnhQr_LaFilePng()
    {
        using var db = _db.CreateContext(TestDb.TenantA);
        var png = new BranchQrService(db, Config(null)).RenderPng("https://cafesaas.vn/quan-a/thuc-don/cau-giay");

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8));
    }

    private static IConfiguration Config(string? baseUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(baseUrl is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["PublicSite:BaseUrl"] = baseUrl })
            .Build();

    private static Branch NewBranch(int tenantId, string name, string slug) => new()
    {
        TenantId = tenantId, Name = name, Slug = slug, Address = "Hà Nội",
        OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(22, 0), IsActive = true
    };

    public void Dispose() => _db.Dispose();
}
