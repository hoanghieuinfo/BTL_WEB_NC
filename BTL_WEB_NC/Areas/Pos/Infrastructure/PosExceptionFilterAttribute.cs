// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Infrastructure;
using BTL_WEB_NC.Areas.Pos.Services;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BTL_WEB_NC.Areas.Pos.Infrastructure;

/// <summary>Giữ cách ánh xạ của Manage (404, 400, 409 RowVersion) và thêm ConflictException → 409.</summary>
public class PosExceptionFilterAttribute : ManageExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ConflictException e)
        {
            context.Result = new ObjectResult(ApiResult.Fail(e.Message)) { StatusCode = StatusCodes.Status409Conflict };
            context.ExceptionHandled = true;
            return;
        }

        base.OnException(context);
    }
}