// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>Controller gốc của Area Manage: chỉ Chủ quán (TenantAdmin) vào được (R04, Bảng 3.19).</summary>
[Area("Manage")]
[Authorize(Roles = AppRoles.TenantAdmin)]
public abstract class ManageControllerBase : Controller
{
}
