// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
namespace BTL_WEB_NC.Infrastructure.Tenancy;

/// <summary>Entity xóa mềm (R42); bộ lọc tenant gộp thêm điều kiện !IsDeleted.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}
