// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Globalization;
using System.Text;

namespace BTL_WEB_NC.Infrastructure;

/// <summary>Chuẩn hóa chuỗi để tìm kiếm không phân biệt hoa/thường và dấu tiếng Việt: "Cà Phê" và "ca phe" khớp nhau.</summary>
public static class TextSearch
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 'đ' không tách được thành chữ + dấu nên phải đổi riêng.
        var decomposed = text.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                }
                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }
}
