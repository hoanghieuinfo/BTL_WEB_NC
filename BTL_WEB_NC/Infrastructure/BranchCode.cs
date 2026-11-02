// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Globalization;
using System.Text;

namespace BTL_WEB_NC.Infrastructure;

/// <summary>Mã viết tắt chi nhánh dùng trong số hóa đơn: "Cầu Giấy" → "CG".</summary>
public static class BranchCode
{
    public static string FromName(string name)
    {
        var normalized = name.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(ch);
            }
        }

        var initials = plain.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word[0])
            .Where(c => char.IsLetterOrDigit(c))
            .Take(4)
            .ToArray();

        return initials.Length == 0 ? "CN" : new string(initials).ToUpperInvariant();
    }
}