// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>Ném ra khi SaveChanges phát hiện bản ghi vi phạm quy tắc tenant (mục 3.2.2c).</summary>
public class TenantViolationException : InvalidOperationException
{
    public TenantViolationException(string message) : base(message)
    {
    }
}
