// [AI] Claude hỗ trợ – Phụ lục Z.7
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB_NC.Areas.Manage.Controllers;

/// <summary>MH08 /manage. Bước 1b: trang tạm "Xin chào" để thử đăng nhập; nội dung thật làm ở Bước 6.</summary>
public class DashboardController : ManageControllerBase
{
    [HttpGet]
    public IActionResult Index() => View();
}
