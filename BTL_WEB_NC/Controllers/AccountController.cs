// [AI] Claude hỗ trợ – Phụ lục Z.7
// TẠM: thay bằng bản của Module 1 khi gộp repo
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Controllers;

/// <summary>Đăng nhập/đăng xuất tối giản để thử Module 2. Claim tenant_id do AppUserClaimsPrincipalFactory sinh.</summary>
public class AccountController : Controller
{
    private const string InvalidLogin = "Email hoặc mật khẩu không đúng.";

    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public AccountController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, AppDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("dang-nhap")]
    public IActionResult Login(string? returnUrl = null, bool locked = false)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (locked)
        {
            ModelState.AddModelError(string.Empty, "Quán đã bị tạm ngưng. Vui lòng liên hệ quản trị hệ thống.");
        }
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("dang-nhap")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Cùng một thông báo cho "không có email" và "sai mật khẩu" để không dò được email.
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, InvalidLogin);
            return View(model);
        }

        // lockoutOnFailure: true → sai 5 lần thì khóa 15 phút (R36).
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.");
            return View(model);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, InvalidLogin);
            return View(model);
        }

        if (user.TenantId.HasValue)
        {
            var status = await _db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Status).FirstAsync();
            if (status != TenantStatus.Active)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Quán chưa được kích hoạt hoặc đang bị tạm ngưng.");
                return View(model);
            }
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        // R02: chuyển tới giao diện theo vai trò. Pos và System thuộc module khác, tạm về trang chủ.
        if (await _userManager.IsInRoleAsync(user, AppRoles.TenantAdmin))
        {
            return Redirect("/manage");
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("dang-xuat")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/dang-nhap");
    }

    [AllowAnonymous]
    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied() => View();
}
