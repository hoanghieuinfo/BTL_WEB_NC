// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.System.Controllers;

/// <summary>Controller gốc của Area System – chỉ Super Admin truy cập (Bảng 3.19, R04).</summary>
[Area("System")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public abstract class SystemControllerBase : Controller
{
}
