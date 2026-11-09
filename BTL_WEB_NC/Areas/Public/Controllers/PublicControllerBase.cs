// [AI] Claude hỗ trợ – Phụ lục Z.7
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Public.Controllers;

[Area("Public")]
[AllowAnonymous]
public abstract class PublicControllerBase : Controller
{
    /// <summary>PublicSite:BaseUrl nếu có cấu hình (giống BranchQrService), không thì lấy từ request.</summary>
    protected string BaseUrl
    {
        get
        {
            var configured = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["PublicSite:BaseUrl"];
            return (string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured).TrimEnd('/');
        }
    }
}
