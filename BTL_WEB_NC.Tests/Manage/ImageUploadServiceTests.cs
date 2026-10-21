// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Files;
using Microsoft.AspNetCore.Http;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Upload ảnh (Bảng 3.22, R39): kiểm tra cả phần mở rộng và chữ ký file, tối đa 2 MB, tên GUID.</summary>
public class ImageUploadServiceTests : IDisposable
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49 };
    private static readonly byte[] Webp = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56 };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "btl-upload-test-" + Guid.NewGuid().ToString("N"));
    private readonly ImageUploadService _service;

    public ImageUploadServiceTests()
    {
        _service = new ImageUploadService(_root);
    }

    [Fact]
    public async Task FileJpgNhungNoiDungKhongPhaiAnh_BiTuChoi()
    {
        var result = await _service.SaveAsync(File("anh.jpg", "<script>alert(1)</script> day khong phai anh"u8.ToArray()), 1);

        Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root, "uploads")));
    }

    [Fact]
    public async Task PngThatNhungDatDuoiJpg_BiTuChoi()
    {
        var result = await _service.SaveAsync(File("anh.jpg", Png), 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DuoiGif_BiTuChoi()
    {
        var result = await _service.SaveAsync(File("anh.gif", "GIF89a......"u8.ToArray()), 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task QuaHaiMB_BiTuChoi()
    {
        var big = new byte[ImageUploadService.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);

        var result = await _service.SaveAsync(File("lon.jpg", big), 1);

        Assert.False(result.Success);
        Assert.Contains("2 MB", result.Error);
    }

    [Theory]
    [InlineData("caphe.JPG", ".jpg")]
    [InlineData("caphe.jpeg", ".jpg")]
    [InlineData("caphe.png", ".png")]
    [InlineData("caphe.webp", ".webp")]
    public async Task AnhHopLe_LuuTenGuidTrongThuMucTenant(string fileName, string expectedExtension)
    {
        var bytes = expectedExtension switch { ".png" => Png, ".webp" => Webp, _ => Jpeg };

        var result = await _service.SaveAsync(File(fileName, bytes), tenantId: 7);

        Assert.True(result.Success);
        Assert.StartsWith("/uploads/7/", result.Path);
        Assert.EndsWith(expectedExtension, result.Path);
        Assert.DoesNotContain("caphe", result.Path); // không dùng tên file người dùng gửi
        Assert.True(System.IO.File.Exists(Path.Combine(_root, result.Path!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Xoa_ChiXoaTrongThuMucUploads()
    {
        var saved = await _service.SaveAsync(File("a.jpg", Jpeg), 1);
        var outside = Path.Combine(_root, "secret.txt");
        System.IO.File.WriteAllText(outside, "x");

        _service.Delete("/uploads/../secret.txt");
        _service.Delete(saved.Path);

        Assert.True(System.IO.File.Exists(outside));
        Assert.False(System.IO.File.Exists(Path.Combine(_root, saved.Path!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
    }

    private static IFormFile File(string name, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", name);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
