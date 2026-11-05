// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models;
using BTL_WEB_NC.Models.Entities;
using BTL_WEB_NC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Controllers;

public class AccountController : Controller
{
    private const string InvalidLogin = "Email hoặc mật khẩu không đúng.";

    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITenantRegistrationService _registrationService;

    public AccountController(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        AppDbContext db,
        ITenantRegistrationService registrationService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _registrationService = registrationService;
    }

    [AllowAnonymous]
    [HttpGet("dang-nhap")]
    public IActionResult Login(string? returnUrl = null, bool locked = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

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

        // Nếu là tài khoản thuộc tenant: kiểm tra trạng thái tenant
        if (user.TenantId.HasValue)
        {
            var status = await _db.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == user.TenantId)
                .Select(t => t.Status)
                .FirstOrDefaultAsync();

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

        // Bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên (R03)
        if (user.MustChangePassword)
        {
            return RedirectToAction("ChangePassword", "Account");
        }

        // R02: Chuyển tới giao diện tương ứng theo vai trò đã phân quyền
        if (await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
        {
            return Redirect("/system");
        }
        if (await _userManager.IsInRoleAsync(user, AppRoles.TenantAdmin))
        {
            return Redirect("/manage");
        }
        if (await _userManager.IsInRoleAsync(user, AppRoles.Staff))
        {
            return Redirect("/pos");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost("dang-xuat")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ---------------------------------------------------------------- Đăng ký mở quán (MH02, UC01, R01)

    [AllowAnonymous]
    [HttpGet("dang-ky")]
    public async Task<IActionResult> Register(int? planId = null, CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Plans = await _db.ServicePlans
            .IgnoreQueryFilters()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PricePerMonth)
            .ToListAsync(ct);

        return View(new RegisterTenantViewModel { ServicePlanId = planId ?? 1 });
    }

    [AllowAnonymous]
    [HttpPost("dang-ky")]
    public async Task<IActionResult> Register(RegisterTenantViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Plans = await _db.ServicePlans.IgnoreQueryFilters().Where(p => p.IsActive).OrderBy(p => p.PricePerMonth).ToListAsync(ct);
            return View(model);
        }

        try
        {
            await _registrationService.RegisterAsync(model, ct);
            return View("RegisterSuccess", model);
        }
        catch (FieldValidationException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            ViewBag.Plans = await _db.ServicePlans.IgnoreQueryFilters().Where(p => p.IsActive).OrderBy(p => p.PricePerMonth).ToListAsync(ct);
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Đã có lỗi xảy ra trong quá trình đăng ký: " + ex.Message);
            ViewBag.Plans = await _db.ServicePlans.IgnoreQueryFilters().Where(p => p.IsActive).OrderBy(p => p.PricePerMonth).ToListAsync(ct);
            return View(model);
        }
    }

    // ---------------------------------------------------------------- AJAX Endpoints A01, A02

    [AllowAnonymous]
    [HttpGet("api/account/check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(new { available = false, message = "Vui lòng nhập địa chỉ email." });
        }
        var taken = await _registrationService.IsEmailRegisteredAsync(email, ct);
        return Ok(new
        {
            available = !taken,
            message = taken ? "Email này đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác." : "Email khả dụng."
        });
    }

    [AllowAnonymous]
    [HttpGet("api/account/check-slug")]
    public async Task<IActionResult> CheckSlug([FromQuery] string? slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Ok(new { available = false, message = "Vui lòng nhập đường dẫn riêng của quán." });
        }
        var clean = slug.Trim().ToLowerInvariant();
        if (_registrationService.IsReservedSlug(clean))
        {
            return Ok(new
            {
                available = false,
                message = "Đường dẫn trùng với từ khóa hệ thống. Vui lòng chọn đường dẫn khác.",
                suggestion = $"{clean}-{new Random().Next(10, 99)}"
            });
        }
        var taken = await _registrationService.IsSlugTakenAsync(clean, ct);
        return Ok(new
        {
            available = !taken,
            message = taken ? "Đường dẫn quán đã được đăng ký." : "Đường dẫn khả dụng!",
            suggestion = taken ? $"{clean}-{new Random().Next(10, 99)}" : null
        });
    }

    // ---------------------------------------------------------------- Đổi mật khẩu (MH18, R03)

    [Authorize]
    [HttpGet("tai-khoan/doi-mat-khau")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost("tai-khoan/doi-mat-khau")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }
            return View(model);
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";

        if (await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin)) return Redirect("/system");
        if (await _userManager.IsInRoleAsync(user, AppRoles.TenantAdmin)) return Redirect("/manage");
        if (await _userManager.IsInRoleAsync(user, AppRoles.Staff)) return Redirect("/pos");
        return RedirectToAction("Index", "Home");
    }
}
