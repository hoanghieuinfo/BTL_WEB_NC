// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Globalization;
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryBranchOption>> BranchesAsync(CancellationToken ct = default);
    Task<StockListResult> StockAsync(StockListQuery query, CancellationToken ct = default);
    Task<StockDetail> StockDetailAsync(int branchId, int ingredientId, CancellationToken ct = default);
    Task<ReceiptListResult> ListReceiptsAsync(ReceiptListQuery query, CancellationToken ct = default);
    Task<ReceiptDetail> GetReceiptAsync(int id, CancellationToken ct = default);
    Task<NewReceiptPageModel> NewReceiptAsync(int? branchId, int? ingredientId, CancellationToken ct = default);
    Task<ReceiptCreatedResult> CreateReceiptAsync(ReceiptFormModel model, string userId, CancellationToken ct = default);
}

/// <summary>
/// Tồn kho theo chi nhánh và phiếu nhập kho (P06, R19, BM07). Mọi ID nhận từ request (BranchId, IngredientId,
/// mã phiếu) được tra lại qua DbSet đã lọc tenant trước khi dùng → của quán khác thì 404 (mục 3.2.2e).
/// </summary>
public class InventoryService : IInventoryService
{
    private const int MaxCodeAttempts = 3;

    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Mọi chi nhánh của quán, kể cả tạm ngưng: được nhập kho trước khi mở lại.</summary>
    public async Task<IReadOnlyList<InventoryBranchOption>> BranchesAsync(CancellationToken ct = default) =>
        await _db.Branches.AsNoTracking()
            .OrderByDescending(b => b.IsActive).ThenBy(b => b.Name)
            .Select(b => new InventoryBranchOption(b.Id, b.Name, b.IsActive))
            .ToListAsync(ct);

    // ------------------------------------------------------------------ Tồn kho

    /// <summary>
    /// Mọi nguyên liệu chưa xóa LEFT JOIN tồn của chi nhánh: nguyên liệu chưa có dòng BranchStocks hiện tồn 0.
    /// Tình trạng (Âm kho / Dưới mức / Đủ) tính khi hiển thị, không lưu (Bảng 3.15). Một quán chỉ có vài chục
    /// đến vài trăm nguyên liệu nên lọc, đếm, sắp xếp trong bộ nhớ sau một truy vấn.
    /// </summary>
    public async Task<StockListResult> StockAsync(StockListQuery query, CancellationToken ct = default)
    {
        var branch = await FindBranchAsync(query.BranchId, ct);
        var all = await StockRowsAsync(branch.Id, ct);

        var summary = new StockSummary(
            all.Count,
            all.Count(r => r.Status == StockStatus.Negative),
            all.Count(r => r.Status == StockStatus.Low));

        IEnumerable<StockRow> rows = all;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim();
            rows = rows.Where(r => r.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase));
        }
        if (query.NeedOnly)
        {
            rows = rows.Where(r => r.Status != StockStatus.Enough);
        }

        var filtered = rows.OrderBy(r => r.Status).ThenBy(r => r.Name, StringComparer.CurrentCulture).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)StockListQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        var items = filtered.Skip((page - 1) * StockListQuery.PageSize).Take(StockListQuery.PageSize).ToList();

        return new StockListResult(branch, items, page, filtered.Count, summary, query);
    }

    public async Task<StockDetail> StockDetailAsync(int branchId, int ingredientId, CancellationToken ct = default)
    {
        var branch = await FindBranchAsync(branchId, ct);
        var stock = (await StockRowsAsync(branch.Id, ct, ingredientId)).FirstOrDefault()
            ?? throw new NotFoundException("Không tìm thấy nguyên liệu.");

        // r.Product đi qua bộ lọc của Products → món đã xóa mềm tự bị loại.
        var usedIn = await _db.Recipes.AsNoTracking()
            .Where(r => r.IngredientId == ingredientId)
            .OrderBy(r => r.Product.Name)
            .Select(r => new StockUsage(r.Product.Name, r.Quantity))
            .ToListAsync(ct);

        var history = await _db.StockReceiptItems.AsNoTracking()
            .Where(x => x.IngredientId == ingredientId && x.StockReceipt.BranchId == branch.Id)
            .OrderByDescending(x => x.StockReceipt.ReceivedDate).ThenByDescending(x => x.StockReceiptId)
            .Take(10)
            .Select(x => new StockHistoryLine(
                x.StockReceiptId, x.StockReceipt.Code, x.StockReceipt.ReceivedDate, x.PurchaseUnit, x.Quantity, x.ConversionRate))
            .ToListAsync(ct);

        return new StockDetail(branch, stock, usedIn, history);
    }

    private async Task<List<StockRow>> StockRowsAsync(int branchId, CancellationToken ct, int? ingredientId = null)
    {
        var ingredients = _db.Ingredients.AsNoTracking();
        if (ingredientId.HasValue)
        {
            ingredients = ingredients.Where(i => i.Id == ingredientId.Value);
        }

        return await (
            from i in ingredients
            join s in _db.BranchStocks.Where(s => s.BranchId == branchId) on i.Id equals s.IngredientId into stocks
            from s in stocks.DefaultIfEmpty()
            select new StockRow(i.Id, i.Name, i.Unit, s == null ? 0 : s.Quantity, i.MinStock, s == null ? null : s.UpdatedAt))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ Phiếu nhập: danh sách, chi tiết

    public async Task<ReceiptListResult> ListReceiptsAsync(ReceiptListQuery query, CancellationToken ct = default)
    {
        // Lọc theo chi nhánh của quán khác → 404, thống nhất với tab Tồn kho và trang Báo cáo.
        if (query.BranchId.HasValue)
        {
            await FindBranchAsync(query.BranchId, ct);
        }
        var receipts = _db.StockReceipts.AsNoTracking();
        if (query.BranchId.HasValue)
        {
            receipts = receipts.Where(r => r.BranchId == query.BranchId.Value);
        }
        if (query.MonthStart is { } from)
        {
            var to = from.AddMonths(1);
            receipts = receipts.Where(r => r.ReceivedDate >= from && r.ReceivedDate < to);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim();
            receipts = receipts.Where(r => r.Code.Contains(keyword) || (r.Supplier != null && r.Supplier.Contains(keyword)));
        }

        var total = await receipts.CountAsync(ct);
        // Cộng trên SQL (SUM), không tải mọi phiếu của kỳ về bộ nhớ.
        var periodTotal = await receipts.SumAsync(r => r.TotalAmount, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)ReceiptListQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);

        var items = await receipts
            .OrderByDescending(r => r.ReceivedDate).ThenByDescending(r => r.Id)
            .Skip((page - 1) * ReceiptListQuery.PageSize)
            .Take(ReceiptListQuery.PageSize)
            .Select(r => new ReceiptRow(r.Id, r.Code, r.ReceivedDate, r.Branch.Name, r.Supplier, r.CreatedBy.FullName, r.TotalAmount))
            .ToListAsync(ct);

        return new ReceiptListResult(items, page, total, periodTotal, query);
    }

    public async Task<ReceiptDetail> GetReceiptAsync(int id, CancellationToken ct = default)
    {
        var receipt = await _db.StockReceipts.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Id, r.Code, r.ReceivedDate, BranchName = r.Branch.Name, r.Supplier, CreatedBy = r.CreatedBy.FullName, r.TotalAmount })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy phiếu nhập.");

        // LEFT JOIN thay vì x.Ingredient: bộ lọc của Ingredients (gộp !IsDeleted) sẽ làm INNER JOIN bỏ mất dòng
        // của nguyên liệu đã xóa mềm. Không dùng IgnoreQueryFilters trong Area Manage → dòng đó vẫn hiện, chỉ thiếu tên.
        var lines = await (
            from x in _db.StockReceiptItems.AsNoTracking()
            where x.StockReceiptId == id
            join i in _db.Ingredients on x.IngredientId equals i.Id into found
            from i in found.DefaultIfEmpty()
            orderby x.Id
            select new ReceiptDetailLine(
                i == null ? "(nguyên liệu đã xóa)" : i.Name, i == null ? "" : i.Unit,
                x.PurchaseUnit, x.ConversionRate, x.Quantity, x.UnitPrice))
            .ToListAsync(ct);

        return new ReceiptDetail(receipt.Id, receipt.Code, receipt.ReceivedDate, receipt.BranchName, receipt.Supplier,
            receipt.CreatedBy, receipt.TotalAmount, lines);
    }

    // ------------------------------------------------------------------ Lập phiếu nhập (P06 bước 2–3)

    public async Task<NewReceiptPageModel> NewReceiptAsync(int? branchId, int? ingredientId, CancellationToken ct = default)
    {
        var branches = await BranchesAsync(ct);
        var ingredients = await _db.Ingredients.AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new IngredientOption(i.Id, i.Name, i.Unit))
            .ToListAsync(ct);

        // Giá trị điền sẵn từ nút "Nhập hàng" chỉ dùng khi thuộc quán hiện tại; lạ thì bỏ qua.
        var branch = branches.Any(b => b.Id == branchId) ? branchId : null;
        var ingredient = ingredients.Any(i => i.Id == ingredientId) ? ingredientId : null;
        return new NewReceiptPageModel(branches, ingredients, branch, ingredient, await NextCodeAsync(ct), VietnamTime.Today);
    }

    /// <summary>
    /// Lưu phiếu nhập: một transaction gồm sinh số phiếu, ghi phiếu + chi tiết, cộng tồn = Quantity × ConversionRate.
    /// TotalAmount luôn tính lại ở server (không tin tổng do trình duyệt gửi).
    /// </summary>
    public async Task<ReceiptCreatedResult> CreateReceiptAsync(ReceiptFormModel model, string userId, CancellationToken ct = default)
    {
        Validate(model);
        var branch = await FindBranchAsync(model.BranchId, ct);

        // Khóa ngoại trong từng dòng: thiếu một nguyên liệu (của quán khác hoặc đã xóa) → 404, chưa ghi gì.
        var ingredientIds = model.Lines.Select(l => l.IngredientId).Distinct().ToList();
        var found = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id), ct);
        if (found != ingredientIds.Count)
        {
            throw new NotFoundException("Không tìm thấy nguyên liệu.");
        }

        var supplier = string.IsNullOrWhiteSpace(model.Supplier) ? null : model.Supplier.Trim();
        var added = model.Lines
            .GroupBy(l => l.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Round(l.Quantity * l.ConversionRate, 3, MidpointRounding.AwayFromZero)));

        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var receipt = new StockReceipt
                {
                    BranchId = branch.Id,
                    Code = await NextCodeAsync(ct),
                    Supplier = supplier,
                    ReceivedDate = model.ReceivedDate!.Value,
                    CreatedById = userId,
                    TotalAmount = model.Lines.Sum(l => InventoryMath.LineAmount(l.Quantity, l.UnitPrice))
                };
                foreach (var line in model.Lines)
                {
                    receipt.Items.Add(new StockReceiptItem
                    {
                        IngredientId = line.IngredientId,
                        PurchaseUnit = line.PurchaseUnit.Trim(),
                        ConversionRate = line.ConversionRate,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice
                    });
                }
                _db.StockReceipts.Add(receipt);

                var now = VietnamTime.Now;
                var stocks = await _db.BranchStocks
                    .Where(s => s.BranchId == branch.Id && ingredientIds.Contains(s.IngredientId))
                    .ToListAsync(ct);
                foreach (var (ingredientId, quantity) in added)
                {
                    var stock = stocks.FirstOrDefault(s => s.IngredientId == ingredientId);
                    if (stock is null)
                    {
                        // Chi nhánh chưa có dòng tồn của nguyên liệu này → tạo mới từ 0.
                        stock = new BranchStock { BranchId = branch.Id, IngredientId = ingredientId, Quantity = 0 };
                        _db.BranchStocks.Add(stock);
                    }
                    stock.Quantity += quantity;
                    stock.UpdatedAt = now;
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new ReceiptCreatedResult(receipt.Id, receipt.Code);
            }
            catch (DbUpdateException) when (attempt < MaxCodeAttempts && !ct.IsCancellationRequested)
            {
                // Request khác vừa lấy cùng số phiếu (UQ TenantId, Code), vừa tạo cùng dòng tồn, hoặc POS vừa trừ kho
                // (RowVersion) → hoàn tác, bỏ mọi thay đổi đang theo dõi rồi làm lại với số liệu mới nhất.
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Số phiếu PN-YYMM-XXX: YYMM là tháng lúc lưu (giờ VN), XXX tăng dần trong quán theo tháng.</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var prefix = $"PN-{VietnamTime.Now:yyMM}-";
        var codes = await _db.StockReceipts.Where(r => r.Code.StartsWith(prefix)).Select(r => r.Code).ToListAsync(ct);
        var max = codes
            .Select(c => int.TryParse(c.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return prefix + (max + 1).ToString("000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Số dòng, ngày nhập không ở tương lai; kiểm tra lại các số của từng dòng (DataAnnotations ở controller
    /// đã chặn, nhưng service không tin nơi gọi – CHECK > 0 của CSDL là lớp cuối).
    /// </summary>
    private static void Validate(ReceiptFormModel model)
    {
        if (model.Lines.Count == 0)
        {
            throw new FieldValidationException("lines", "Phiếu nhập cần ít nhất một dòng nguyên liệu.");
        }
        if (model.Lines.Count > ReceiptFormModel.MaxLines)
        {
            throw new FieldValidationException("lines", $"Một phiếu tối đa {ReceiptFormModel.MaxLines} dòng.");
        }
        if (model.ReceivedDate > VietnamTime.Today)
        {
            throw new FieldValidationException("receivedDate", "Ngày nhập không được sau hôm nay.");
        }
        for (var i = 0; i < model.Lines.Count; i++)
        {
            var line = model.Lines[i];
            if (line.ConversionRate <= 0)
            {
                throw new FieldValidationException($"lines[{i}].conversionRate", "Phải lớn hơn 0.");
            }
            if (line.Quantity <= 0)
            {
                throw new FieldValidationException($"lines[{i}].quantity", "Phải lớn hơn 0.");
            }
            if (line.UnitPrice < 0 || line.UnitPrice != decimal.Truncate(line.UnitPrice))
            {
                throw new FieldValidationException($"lines[{i}].unitPrice", "Đơn giá là số nguyên từ 0 trở lên.");
            }
        }
    }

    private async Task<BranchOption> FindBranchAsync(int? branchId, CancellationToken ct) =>
        await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new BranchOption(b.Id, b.Name, b.Address))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy chi nhánh.");
}
