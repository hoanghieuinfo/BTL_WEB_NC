// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

/// <summary>
/// Việc 12 – trừ kho tự động khi thanh toán (P07, R30). Module 3 (Trung) gọi trong action A16
/// <c>POST /pos/orders/{id}/pay</c>; hướng dẫn ở docs/Viec12_HuongDanTrung.md.
/// </summary>
public interface IInventoryDeductionService
{
    /// <summary>
    /// Trừ tồn của chi nhánh bán hàng theo định lượng × số lượng của mọi món chưa hủy trong đơn.
    /// - KHÔNG tự mở transaction: nơi gọi mở transaction bao cả cập nhật đơn, trừ kho, đổi trạng thái bàn.
    /// - Có gọi SaveChangesAsync (trên cùng DbContext nên nằm trong transaction của nơi gọi) để xung đột lộ ra ngay tại đây.
    /// - Ném <see cref="DbUpdateConcurrencyException"/> khi dòng tồn vừa bị request khác sửa (RowVersion) → nơi gọi rollback, trả 409.
    /// - Ném <see cref="NotFoundException"/> khi đơn không thuộc tenant hiện tại.
    /// Chỉ gọi MỘT lần cho mỗi đơn: service không đánh dấu "đã trừ"; A16 chống thanh toán trùng bằng RowVersion của Orders.
    /// </summary>
    Task DeductForOrderAsync(long orderId, CancellationToken ct = default);
}

public class InventoryDeductionService : IInventoryDeductionService
{
    private readonly AppDbContext _db;

    public InventoryDeductionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task DeductForOrderAsync(long orderId, CancellationToken ct = default)
    {
        // _db.Orders đã lọc theo tenant của nhân viên (claim) → đơn quán khác coi như không có.
        var order = await _db.Orders
            .Where(o => o.Id == orderId)
            .Select(o => new { o.BranchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        // P07 bước 1: chỉ món chưa hủy; gộp các dòng cùng món (gọi thêm nhiều lần).
        var sold = await _db.OrderItems
            .Where(i => i.OrderId == orderId && !i.IsCancelled)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);
        if (sold.Count == 0)
        {
            return;
        }

        // Đọc thẳng bảng Recipes (không qua r.Product) để món đã ngừng/xóa mềm sau khi gọi vẫn được trừ.
        // Món chưa định lượng không có dòng nào ở đây → tự bỏ qua (R15: "bán không trừ kho").
        var productIds = sold.Select(s => s.ProductId).ToList();
        var recipes = await _db.Recipes
            .Where(r => productIds.Contains(r.ProductId))
            .Select(r => new { r.ProductId, r.IngredientId, r.Quantity })
            .ToListAsync(ct);
        if (recipes.Count == 0)
        {
            return;
        }

        // P07 bước 2: lượng tiêu hao = định lượng × số lượng món, cộng dồn theo nguyên liệu.
        var usage = recipes
            .Join(sold, r => r.ProductId, s => s.ProductId, (r, s) => new { r.IngredientId, Amount = r.Quantity * s.Quantity })
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var ingredientIds = usage.Keys.ToList();
        var stocks = await _db.BranchStocks
            .Where(s => s.BranchId == order.BranchId && ingredientIds.Contains(s.IngredientId))
            .ToListAsync(ct);

        var now = VietnamTime.Now;
        foreach (var (ingredientId, amount) in usage)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == ingredientId);
            if (stock is null)
            {
                // Chi nhánh chưa có dòng tồn → tạo với tồn 0 rồi trừ (P07 bước 2).
                stock = new BranchStock { BranchId = order.BranchId, IngredientId = ingredientId, Quantity = 0 };
                _db.BranchStocks.Add(stock);
            }
            stock.Quantity -= amount; // P07 bước 3: cho phép âm, không chặn bán hàng
            stock.UpdatedAt = now;
        }

        // RowVersion của BranchStocks là concurrency token: dòng vừa bị sửa ở request khác → DbUpdateConcurrencyException.
        await _db.SaveChangesAsync(ct);
    }
}
