// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Public.Models;
using BTL_WEB_NC.Areas.Public.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Public.Controllers;

/// <summary>MH04 /{slug-quan}/thuc-don/{slug-chi-nhanh} – không cần đăng nhập (UC18, R29).</summary>
public class PublicMenuController : PublicControllerBase
{
    private readonly IPublicMenuService _menu;

    public PublicMenuController(IPublicMenuService menu)
    {
        _menu = menu;
    }

    [HttpGet("{tenantSlug:length(1,60)}/thuc-don/{branchSlug:length(1,60)}")]
    public async Task<IActionResult> Index(string tenantSlug, string branchSlug, CancellationToken ct)
    {
        var result = await _menu.GetAsync(tenantSlug, branchSlug, ct);
        switch (result.Status)
        {
            case PublicMenuStatus.Found:
                // Canonical dựng từ slug trong CSDL, không từ URL khách gõ.
                ViewData["Canonical"] = $"{BaseUrl}/{result.Page!.TenantSlug}/thuc-don/{result.Page.BranchSlug}";
                return View(result.Page);

            case PublicMenuStatus.Suspended:
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return View("Suspended", result.TenantName);

            default:
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("NotFound");
        }
    }
}
