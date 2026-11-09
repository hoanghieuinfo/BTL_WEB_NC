// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Auth;
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        AppDbContext db,
        ITenantRegistrationService registrationService,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _registrationService = registrationService;
        _logger = logger;
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
    [EnableRateLimiting(AccountRateLimits.LoginPolicy)]
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

        // R02, R03: đổi mật khẩu khởi tạo được xét trước returnUrl, sau đó mới tới trang theo vai trò.
        // Mọi đích đều là đường dẫn nội bộ (returnUrl đã qua Url.IsLocalUrl) nên dùng LocalRedirect.
        var localReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var target = PostLoginRedirect.Resolve(user.MustChangePassword, localReturnUrl, await _userManager.GetRolesAsync(user));
        return LocalRedirect(target);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bảng 3.22: không đưa chi tiết lỗi nội bộ ra giao diện; chi tiết chỉ ghi vào log.
            _logger.LogError(ex, "Đăng ký quán thất bại cho slug {Slug}", model.Slug);
            ModelState.AddModelError(string.Empty, "Không thể đăng ký lúc này. Vui lòng thử lại sau.");
            ViewBag.Plans = await _db.ServicePlans.IgnoreQueryFilters().Where(p => p.IsActive).OrderBy(p => p.PricePerMonth).ToListAsync(ct);
            return View(model);
        }
    }

    // ---------------------------------------------------------------- AJAX Endpoints A01, A02

    [AllowAnonymous]
    [HttpGet("api/account/check-email")]
    [EnableRateLimiting(AccountRateLimits.AccountCheckPolicy)]
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
    [EnableRateLimiting(AccountRateLimits.AccountCheckPolicy)]
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

        return LocalRedirect(PostLoginRedirect.Resolve(false, null, await _userManager.GetRolesAsync(user)));
    }
}
