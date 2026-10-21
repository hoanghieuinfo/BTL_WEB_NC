// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Areas.Manage.Services;

// Service ném các lỗi nghiệp vụ dưới đây; ManageExceptionFilter đổi chúng thành mã HTTP + JSON
// nên controller không cần try/catch (controller mỏng).

/// <summary>
/// Không thấy bản ghi qua DbSet đã lọc tenant → 404. Dùng cho cả ID trên URL lẫn khóa ngoại trong request
/// (R38, UC10 6c): bản ghi của tenant khác được coi như không tồn tại, không trả 403 để không lộ là nó có thật.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message = "Không tìm thấy dữ liệu.") : base(message)
    {
    }
}

/// <summary>Lỗi gắn với một ô nhập (ví dụ tên món trùng – UC10 6a, ảnh sai – 6b) → 400, data.errors.{field}.</summary>
public class FieldValidationException : Exception
{
    public FieldValidationException(string field, string message) : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}

/// <summary>Vi phạm quy tắc nghiệp vụ không gắn với ô nào (ví dụ xóa danh mục còn món) → 400.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
