// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Text.Json;
using BTL_WEB_NC.Areas.Manage.Infrastructure;
using BTL_WEB_NC.Areas.Manage.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace BTL_WEB_NC.Tests.Manage;

/// <summary>Lỗi nghiệp vụ của service → mã HTTP đúng mục 3.2.6.</summary>
public class ManageExceptionFilterTests
{
    [Fact]
    public void NotFound_Tra404()
    {
        var context = Run(new NotFoundException("Không tìm thấy danh mục."));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void LoiTaiO_Tra400_CoDataErrors()
    {
        var context = Run(new FieldValidationException("name", "Quán đã có món \"Bạc xỉu\"."));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("Quán đã có món \"Bạc xỉu\".", json.RootElement.GetProperty("data").GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public void LoiKhac_KhongXuLy()
    {
        var context = Run(new InvalidOperationException("lỗi hệ thống"));

        Assert.Null(context.Result);
        Assert.False(context.ExceptionHandled);
    }

    private static ExceptionContext Run(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, new List<IFilterMetadata>()) { Exception = exception };
        new ManageExceptionFilterAttribute().OnException(context);
        return context;
    }
}
