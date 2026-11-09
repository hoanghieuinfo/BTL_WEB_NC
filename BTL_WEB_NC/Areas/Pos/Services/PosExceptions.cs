// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Areas.Pos.Services;

/// <summary>Xung đột trạng thái do thiết bị khác vừa thao tác (ví dụ bàn vừa có khách) → 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}