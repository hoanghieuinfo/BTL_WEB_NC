// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Ajax;

public static class AjaxRequestExtensions
{
    /// <summary>Request AJAX: đường dẫn /api hoặc header X-Requested-With do ajax.js gửi (mục 3.2.6).</summary>
    public static bool IsAjaxRequest(this HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        || string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
