// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Pos.Services;

public interface IMenuService
{
    /// <summary>A11: tìm món theo từ khóa và danh mục. Danh mục không thuộc quán → NotFoundException (404).</summary>
    Task<MenuSearchResult> SearchAsync(string? keyword, int? categoryId, CancellationToken ct = default);
}

/// <summary>
/// A11 (R24) – danh sách món khi bán. Chỉ món Đang bán và chưa xóa của quán hiện tại: Global Query Filter lo tenant
/// và xóa mềm, service lọc thêm IsAvailable. Tìm không phân biệt hoa/thường và dấu nên chạy trong bộ nhớ trên thực đơn
/// của một quán (vài chục đến vài trăm món); SQL Server tìm theo dấu phụ thuộc collation nên không dùng LIKE.
/// </summary>
public class MenuService : IMenuService
{
    private readonly AppDbContext _db;

    public MenuService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MenuSearchResult> SearchAsync(string? keyword, int? categoryId, CancellationToken ct = default)
    {
        // Khóa ngoại nhận từ request phải tra lại qua DbSet đã lọc (CLAUDE.md – Đa tenant); danh mục quán khác → 404.
        if (categoryId is int id && !await _db.Categories.AnyAsync(c => c.Id == id, ct))
        {
            throw new NotFoundException("Không tìm thấy danh mục.");
        }

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.Products.Any(p => p.IsAvailable))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PosCategoryDto(c.Id, c.Name))
            .ToListAsync(ct);

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsAvailable)
            .Select(p => new PosProductDto(p.Id, p.Name, p.Price, p.ImagePath, p.CategoryId))
            .ToListAsync(ct);

        // Sắp theo thứ tự danh mục rồi theo tên; món thuộc danh mục đã bị ẩn/xóa mềm không hiện.
        var order = categories.Select((c, index) => (c.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var tokens = TextSearch.Normalize(keyword).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matched = products
            .Where(p => order.ContainsKey(p.CategoryId))
            .Where(p => categoryId is null || p.CategoryId == categoryId)
            .Where(p =>
            {
                var name = TextSearch.Normalize(p.Name);
                return tokens.All(t => name.Contains(t));   // mọi từ trong ô tìm kiếm đều phải có trong tên món
            })
            .OrderBy(p => order[p.CategoryId])
            .ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        return new MenuSearchResult(categories, matched);
    }
}
