// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>Entity nghiệp vụ thuộc về một tenant; AppDbContext tự lọc và tự gán TenantId (mục 3.2.2a).</summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}
