// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IBranchQrService
{
    /// <param name="requestBaseUrl">scheme://host của request, dùng khi chưa cấu hình PublicSite:BaseUrl.</param>
    Task<BranchQrInfo> GetInfoAsync(int branchId, string requestBaseUrl, CancellationToken ct = default);

    byte[] RenderPng(string url);
}

/// <summary>
/// Mã QR thực đơn công khai của chi nhánh (R16): {base}/{slug-quan}/thuc-don/{slug-chi-nhanh}.
/// Trang thực đơn công khai do Module 3 làm; ở đây chỉ sinh đường dẫn và ảnh.
/// </summary>
public class BranchQrService : IBranchQrService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public BranchQrService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<BranchQrInfo> GetInfoAsync(int branchId, string requestBaseUrl, CancellationToken ct = default)
    {
        // Branches có bộ lọc tenant → chi nhánh của quán khác → 404.
        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name, b.Slug, b.TenantId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");

        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == branch.TenantId)
            .Select(t => new { t.Name, t.Slug })
            .SingleAsync(ct);

        var baseUrl = (_config["PublicSite:BaseUrl"] is { Length: > 0 } configured ? configured : requestBaseUrl).TrimEnd('/');
        var url = $"{baseUrl}/{tenant.Slug}/thuc-don/{branch.Slug}";

        return new BranchQrInfo(branch.Id, branch.Name, tenant.Name, url, $"qr-{tenant.Slug}-{branch.Slug}.png");
    }

    /// <summary>PNG 20 px mỗi ô, mức sửa lỗi Q (~25%) để mã vẫn quét được khi in mờ hoặc bị xước.</summary>
    public byte[] RenderPng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20);
    }
}
