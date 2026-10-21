// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Ajax;
using BTL_WEB_NC.Infrastructure.Files;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// AutoValidateAntiforgeryToken toàn cục: mọi POST/PUT/DELETE phải có token (Bảng 3.22 – CSRF).
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Razor mặc định mã hóa mọi ký tự ngoài Latin cơ bản thành &#x...; → giữ nguyên chữ tiếng Việt.
// Các ký tự nguy hiểm (< > & " ') vẫn luôn được mã hóa nên không ảnh hưởng chống XSS.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// AJAX gửi token trong header này; layout đặt token vào thẻ meta (mục 3.2.6).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// TenantContext (Scoped) giữ tenant của request hiện tại; AppDbContext đọc nó để lọc dữ liệu (mục 3.2.2).
builder.Services.AddScoped<TenantContext>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache();

// Service của Area Manage (Module 2).
builder.Services.AddScoped<IPlanUsageService, PlanUsageService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IBranchQrService, BranchQrService>();
builder.Services.AddSingleton<IImageUploadService>(sp =>
    new ImageUploadService(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath));

// Identity – chính sách theo Bảng 3.22 (R36).
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

// Khóa/chuyển chi nhánh nhân viên đổi SecurityStamp → phiên cũ bị thu hồi sau tối đa 5 phút.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

// Cookie đăng nhập (Bảng 3.22 – R37, R39).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/dang-nhap";
    options.LogoutPath = "/dang-xuat";
    options.AccessDeniedPath = "/khong-co-quyen";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = AjaxCookieEvents.OnRedirectToLogin,
        OnRedirectToAccessDenied = AjaxCookieEvents.OnRedirectToAccessDenied
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Tự áp migration và seed dữ liệu mẫu khi CSDL còn trống (chỉ môi trường phát triển).
    await SeedData.InitializeAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    // Ảnh do người dùng tải lên: cấm trình duyệt đoán kiểu nội dung (Bảng 3.22 – tải lên file độc hại).
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        }
    }
});
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapAreaControllerRoute(
    name: "manage",
    areaName: "Manage",
    pattern: "manage/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
