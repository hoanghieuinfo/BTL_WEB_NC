// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models;

namespace BTL_WEB_NC.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Lấy danh sách các gói dịch vụ đang hoạt động để hiển thị bảng giá BM02 (R10)
        var plans = await _db.ServicePlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.PricePerMonth)
            .ToListAsync(ct);

        return View(plans);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
