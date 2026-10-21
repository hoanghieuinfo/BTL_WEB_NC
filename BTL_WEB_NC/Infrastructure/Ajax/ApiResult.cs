// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BTL_WEB_NC.Infrastructure.Ajax;

/// <summary>Định dạng JSON chung cho AJAX: { success, message, data } (mục 3.2.6).</summary>
public record ApiResult(bool Success, string? Message, object? Data)
{
    public static ApiResult Ok(object? data = null, string? message = null) => new(true, message, data);

    public static ApiResult Fail(string message, object? data = null) => new(false, message, data);

    /// <summary>
    /// Lỗi validate: data.errors = { tenTruong: [thông báo] }. Tên trường đổi sang camelCase
    /// để khớp thuộc tính data-error-for trong form (ví dụ "CategoryId" → "categoryId").
    /// </summary>
    public static ApiResult ValidationFailed(ModelStateDictionary modelState, string message = "Dữ liệu chưa hợp lệ.")
    {
        var errors = modelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kv => ToCamelCase(kv.Key),
                kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new(false, message, new { errors });
    }

    /// <summary>Lỗi của một trường do service phát hiện (ví dụ tên món trùng).</summary>
    public static ApiResult FieldError(string field, string error) =>
        new(false, error, new { errors = new Dictionary<string, string[]> { [ToCamelCase(field)] = new[] { error } } });

    private static string ToCamelCase(string key) =>
        string.Join('.', key.Split('.').Select(part => JsonNamingPolicy.CamelCase.ConvertName(part)));
}
