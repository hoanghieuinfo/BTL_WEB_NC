// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTL_WEB_NC.Areas.Pos.Infrastructure;

namespace BTL_WEB_NC.Areas.Pos.Controllers;

[Area("Pos")]
[Authorize(Roles = AppRoles.Staff)]
[PosExceptionFilter]
public abstract class PosControllerBase : Controller
{
    protected const string NoBranch = "Tài khoản chưa được phân công chi nhánh. Vui lòng liên hệ chủ quán.";
}