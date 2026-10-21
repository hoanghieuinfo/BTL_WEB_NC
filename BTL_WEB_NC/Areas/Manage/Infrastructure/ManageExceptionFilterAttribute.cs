// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BTL_WEB_NC.Areas.Manage.Infrastructure;

/// <summary>
/// Đổi lỗi nghiệp vụ của service thành phản hồi { success, message, data } với mã đúng mục 3.2.6:
/// NotFoundException → 404, FieldValidationException/BusinessRuleException → 400.
/// Lỗi khác để nguyên (500) cho middleware xử lý lỗi chung.
/// </summary>
public class ManageExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var (status, body) = context.Exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, ApiResult.Fail(e.Message)),
            FieldValidationException e => (StatusCodes.Status400BadRequest, ApiResult.FieldError(e.Field, e.Message)),
            BusinessRuleException e => (StatusCodes.Status400BadRequest, ApiResult.Fail(e.Message)),
            _ => (0, null)
        };

        if (body is null)
        {
            return;
        }

        context.Result = new ObjectResult(body) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
