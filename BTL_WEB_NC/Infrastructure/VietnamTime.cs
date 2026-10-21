// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Infrastructure;

/// <summary>
/// Quy ước của nhóm: CreatedAt, PaidAt… lưu theo giờ Việt Nam (UTC+7), không lưu UTC.
/// Mọi quán đều ở Việt Nam nên báo cáo "hôm nay" nhóm theo ngày trực tiếp, không cần đổi múi giờ.
/// Dùng hàm này thay cho DateTime.Now để không phụ thuộc múi giờ của máy chủ.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);
}
