// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Text.Json;
using BTL_WEB_NC.Infrastructure.Ajax;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BTL_WEB_NC.Tests.Ajax;

/// <summary>Định dạng JSON và mã 401/403 cho AJAX (mục 3.2.6).</summary>
public class AjaxInfrastructureTests
{
    [Fact]
    public void ValidationFailed_TraDataErrorsTheoTenTruongCamelCase()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Tên món đã tồn tại.");
        modelState.AddModelError("CategoryId", "Vui lòng chọn danh mục.");

        var result = ApiResult.ValidationFailed(modelState);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var errors = doc.RootElement.GetProperty("data").GetProperty("errors");

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Tên món đã tồn tại.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("Vui lòng chọn danh mục.", errors.GetProperty("categoryId")[0].GetString());
    }

    [Theory]
    [InlineData(true, StatusCodes.Status401Unauthorized)]
    [InlineData(false, StatusCodes.Status302Found)]
    public async Task ChuaDangNhap_Ajax401_RequestThuong302(bool ajax, int expectedStatus)
    {
        var context = CreateRedirectContext(ajax);

        await AjaxCookieEvents.OnRedirectToLogin(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    [Fact]
    public async Task KhongDuQuyen_Ajax403()
    {
        var context = CreateRedirectContext(ajax: true);

        await AjaxCookieEvents.OnRedirectToAccessDenied(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(bool ajax)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/manage/products/list";
        http.Response.Body = new MemoryStream();
        if (ajax)
        {
            http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        }

        var scheme = new AuthenticationScheme("Identity.Application", null, typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(
            http, scheme, new CookieAuthenticationOptions(), new AuthenticationProperties(), "/dang-nhap");
    }
}
