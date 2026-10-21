// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Infrastructure.Files;

/// <summary>Kết quả lưu ảnh: thành công thì có Path (đường dẫn web), thất bại thì có Error (tiếng Việt).</summary>
public record ImageUploadResult(string? Path, string? Error)
{
    public bool Success => Path is not null;
}

public interface IImageUploadService
{
    /// <summary>Kiểm tra và lưu ảnh vào wwwroot/uploads/{tenantId}/, trả về đường dẫn dạng /uploads/{tenantId}/{guid}.ext.</summary>
    Task<ImageUploadResult> SaveAsync(IFormFile file, int tenantId, CancellationToken ct = default);

    /// <summary>Xóa file ảnh đã lưu (bỏ qua nếu đường dẫn rỗng hoặc không nằm trong /uploads/).</summary>
    void Delete(string? path);
}

/// <summary>
/// Upload ảnh an toàn theo Bảng 3.22 (R39):
/// - chỉ JPG/PNG/WEBP, kiểm tra CẢ phần mở rộng VÀ chữ ký file (magic bytes) – file .jpg chứa nội dung khác bị từ chối;
/// - tối đa 2 MB;
/// - đổi tên bằng GUID (không dùng tên người dùng gửi lên), lưu theo thư mục tenant.
/// </summary>
public class ImageUploadService : IImageUploadService
{
    public const long MaxBytes = 2 * 1024 * 1024;
    private const string UploadFolder = "uploads";

    // Chữ ký đầu file của từng định dạng được phép.
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffSignature = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpSignature = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP" tại byte 8–11

    private readonly string _webRootPath;

    public ImageUploadService(string webRootPath)
    {
        _webRootPath = webRootPath;
    }

    public async Task<ImageUploadResult> SaveAsync(IFormFile file, int tenantId, CancellationToken ct = default)
    {
        if (file.Length == 0)
        {
            return new(null, "File ảnh rỗng.");
        }
        if (file.Length > MaxBytes)
        {
            var mb = (file.Length / 1024d / 1024d).ToString("0.#");
            return new(null, $"Ảnh nặng {mb} MB, vượt giới hạn 2 MB. Vui lòng chọn ảnh nhỏ hơn.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return new(null, "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");
        }

        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            if (read < header.Length || !SignatureMatches(extension, header))
            {
                return new(null, "Nội dung file không phải ảnh hợp lệ.");
            }
        }

        var folder = Path.Combine(_webRootPath, UploadFolder, tenantId.ToString());
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{(extension == ".jpeg" ? ".jpg" : extension)}";

        await using (var output = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
        {
            await file.CopyToAsync(output, ct);
        }

        return new($"/{UploadFolder}/{tenantId}/{fileName}", null);
    }

    public void Delete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith($"/{UploadFolder}/", StringComparison.Ordinal) || path.Contains(".."))
        {
            return;
        }

        var physical = Path.Combine(_webRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physical))
        {
            File.Delete(physical);
        }
    }

    /// <summary>Chữ ký phải khớp đúng loại của phần mở rộng (PNG đặt đuôi .jpg cũng bị từ chối).</summary>
    private static bool SignatureMatches(string extension, byte[] header) => extension switch
    {
        ".jpg" or ".jpeg" => header.AsSpan(0, 3).SequenceEqual(JpegSignature),
        ".png" => header.AsSpan(0, 8).SequenceEqual(PngSignature),
        ".webp" => header.AsSpan(0, 4).SequenceEqual(RiffSignature) && header.AsSpan(8, 4).SequenceEqual(WebpSignature),
        _ => false
    };
}
