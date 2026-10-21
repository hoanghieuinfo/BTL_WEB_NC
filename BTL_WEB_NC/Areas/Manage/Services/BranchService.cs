// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Globalization;
using System.Text;
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IBranchService
{
    Task<IReadOnlyList<BranchRow>> ListAsync(CancellationToken ct = default);
    Task<string> GetTenantSlugAsync(CancellationToken ct = default);
    Task<ZoneBoardModel> GetBoardAsync(int branchId, CancellationToken ct = default);
    Task<int> CreateBranchAsync(BranchFormModel model, CancellationToken ct = default);
    Task UpdateBranchAsync(int id, BranchFormModel model, CancellationToken ct = default);
    Task<int> CreateZoneAsync(int branchId, ZoneFormModel model, CancellationToken ct = default);
    Task RenameZoneAsync(int zoneId, ZoneFormModel model, CancellationToken ct = default);
    Task MoveZoneAsync(int zoneId, bool up, CancellationToken ct = default);
    Task<int> CreateTableAsync(int zoneId, TableFormModel model, CancellationToken ct = default);
    Task UpdateTableAsync(int tableId, TableFormModel model, CancellationToken ct = default);
    Task SetTableActiveAsync(int tableId, bool isActive, CancellationToken ct = default);
}

/// <summary>
/// Chi nhánh, khu vực, bàn (P03, R11, R12). Mọi ID trên URL (BranchId, ZoneId, TableId) đều tra qua DbSet đã có
/// Global Query Filter → bản ghi của tenant khác coi như không tồn tại → 404 (R38). Không xóa chi nhánh/bàn:
/// chi nhánh "Tạm ngưng", bàn "Ẩn" (R42 – dữ liệu đã phát sinh hóa đơn).
/// </summary>
public class BranchService : IBranchService
{
    private readonly AppDbContext _db;
    private readonly IPlanUsageService _usage;
    private readonly IPlanLimitGuard _limits;

    public BranchService(AppDbContext db, IPlanUsageService usage, IPlanLimitGuard limits)
    {
        _db = db;
        _usage = usage;
        _limits = limits;
    }

    // ------------------------------------------------------------------ Đọc

    public async Task<IReadOnlyList<BranchRow>> ListAsync(CancellationToken ct = default) =>
        await _db.Branches.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new BranchRow(
                b.Id, b.Name, b.Slug, b.Address, b.Phone, b.OpenTime, b.CloseTime, b.IsActive,
                _db.DiningTables.Count(t => t.Zone.BranchId == b.Id && t.IsActive),
                b.Zones.Count()))
            .ToListAsync(ct);

    /// <summary>Slug của quán hiện tại – hiện làm tiền tố "/{slug-quan}/thuc-don/" trong form chi nhánh.</summary>
    public async Task<string> GetTenantSlugAsync(CancellationToken ct = default) =>
        await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _db.CurrentTenantId)
            .Select(t => t.Slug)
            .SingleAsync(ct);

    public async Task<ZoneBoardModel> GetBoardAsync(int branchId, CancellationToken ct = default)
    {
        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");

        var zones = await _db.Zones.AsNoTracking()
            .Where(z => z.BranchId == branchId)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Id)
            .Select(z => new ZoneView(z.Id, z.Name, z.SortOrder,
                z.Tables.OrderBy(t => t.Name)
                    .Select(t => new TableView(t.Id, t.Name, t.Seats, t.IsActive, t.Status))
                    .ToList()))
            .ToListAsync(ct);

        return new ZoneBoardModel(branch.Id, branch.Name, zones);
    }

    // ------------------------------------------------------------------ Chi nhánh

    /// <summary>
    /// R11: đếm chi nhánh hiện có (kể cả đang tạm ngưng) so với MaxBranches của gói trước khi thêm.
    /// Đếm và thêm nằm trong một transaction đã khóa dòng Tenants (PlanLimitGuard) → hai request đồng thời không cùng vượt giới hạn.
    /// </summary>
    public async Task<int> CreateBranchAsync(BranchFormModel model, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _limits.LockTenantAsync(ct);

        var usage = await _usage.GetAsync(ct);
        if (usage.BranchCount >= usage.MaxBranches)
        {
            throw new BusinessRuleException(
                $"Đã dùng hết {usage.BranchCount}/{usage.MaxBranches} chi nhánh của gói {usage.PlanName}. Nâng cấp gói để thêm chi nhánh.");
        }

        var name = model.Name.Trim();
        var slug = model.Slug.Trim();
        await EnsureUniqueBranchAsync(name, slug, exceptId: null, ct);

        var branch = new Branch(); // TenantId gán tự động khi lưu
        Apply(branch, model, name, slug);
        _db.Branches.Add(branch);
        await SaveBranchAsync(name, slug, exceptId: null, ct);
        await transaction.CommitAsync(ct);
        return branch.Id;
    }

    public async Task UpdateBranchAsync(int id, BranchFormModel model, CancellationToken ct = default)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");

        var name = model.Name.Trim();
        var slug = model.Slug.Trim();
        await EnsureUniqueBranchAsync(name, slug, exceptId: id, ct);

        // Tạm ngưng khi còn đơn đang phục vụ sẽ để lại đơn dở dang ở chi nhánh đã ngưng → chặn (giống quy tắc ẩn bàn).
        if (branch.IsActive && !model.IsActive)
        {
            var serving = await _db.Orders.CountAsync(o => o.BranchId == id && o.Status == OrderStatus.Serving, ct);
            if (serving > 0)
            {
                throw new BusinessRuleException(
                    $"Chi nhánh còn {serving} đơn đang phục vụ. Hãy thanh toán hoặc hủy các đơn trước khi tạm ngưng.");
            }
        }

        Apply(branch, model, name, slug);
        await SaveBranchAsync(name, slug, exceptId: id, ct);
    }

    private static void Apply(Branch branch, BranchFormModel model, string name, string slug)
    {
        branch.Name = name;
        branch.Slug = slug;
        branch.Address = model.Address.Trim();
        branch.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        branch.OpenTime = model.OpenTime!.Value;
        branch.CloseTime = model.CloseTime!.Value;
        branch.IsActive = model.IsActive;
    }

    private async Task EnsureUniqueBranchAsync(string name, string slug, int? exceptId, CancellationToken ct)
    {
        if (await _db.Branches.AnyAsync(b => b.Name == name && b.Id != exceptId, ct))
        {
            throw new FieldValidationException("name", $"Quán đã có chi nhánh \"{name}\".");
        }

        var owner = await _db.Branches.Where(b => b.Slug == slug && b.Id != exceptId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        if (owner is not null)
        {
            var suggestion = await FindFreeSlugAsync(SuggestSlug(name) is { Length: > 0 } s ? s : slug, exceptId, ct);
            throw new FieldValidationException("slug",
                $"Đường dẫn \"{slug}\" đã dùng cho chi nhánh {owner}. Thử \"{suggestion}\".");
        }
    }

    private async Task SaveBranchAsync(string name, string slug, int? exceptId, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Request đồng thời cùng qua bước kiểm tra → chỉ mục UNIQUE chặn lại; báo đúng ô bị trùng.
            _db.ChangeTracker.Clear();
            await EnsureUniqueBranchAsync(name, slug, exceptId, ct);
            throw;
        }
    }

    /// <summary>Slug còn trống trong tenant: "ba-dinh", rồi "ba-dinh-2", "ba-dinh-3"…</summary>
    private async Task<string> FindFreeSlugAsync(string baseSlug, int? exceptId, CancellationToken ct)
    {
        var taken = await _db.Branches.Where(b => b.Id != exceptId && b.Slug.StartsWith(baseSlug)).Select(b => b.Slug).ToListAsync(ct);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Gợi ý slug từ tên: bỏ dấu tiếng Việt (đ → d), chữ thường, ký tự khác chữ/số thành "-".
    /// branches.js dùng cùng thuật toán để gợi ý ngay khi gõ.
    /// </summary>
    public static string SuggestSlug(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue; // dấu thanh, dấu mũ…
            }
            builder.Append(ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? ch : '-');
        }

        var slug = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }

    // ------------------------------------------------------------------ Khu vực

    public async Task<int> CreateZoneAsync(int branchId, ZoneFormModel model, CancellationToken ct = default)
    {
        // BranchId từ URL phải thuộc tenant hiện tại – tra qua DbSet đã lọc trước khi gán (mục 3.2.2e).
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            throw new NotFoundException("Không tìm thấy chi nhánh.");
        }

        var name = model.Name.Trim();
        await EnsureUniqueZoneAsync(branchId, name, exceptId: null, ct);

        var maxSort = await _db.Zones.Where(z => z.BranchId == branchId).MaxAsync(z => (int?)z.SortOrder, ct) ?? 0;
        var zone = new Zone { BranchId = branchId, Name = name, SortOrder = maxSort + 1 };
        _db.Zones.Add(zone);
        await _db.SaveChangesAsync(ct);
        return zone.Id;
    }

    public async Task RenameZoneAsync(int zoneId, ZoneFormModel model, CancellationToken ct = default)
    {
        var zone = await FindZoneAsync(zoneId, ct);
        var name = model.Name.Trim();
        await EnsureUniqueZoneAsync(zone.BranchId, name, exceptId: zoneId, ct);

        zone.Name = name;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Đổi thứ tự với khu vực liền kề trong cùng chi nhánh (một lần lưu cho cả hai dòng).</summary>
    public async Task MoveZoneAsync(int zoneId, bool up, CancellationToken ct = default)
    {
        var zone = await FindZoneAsync(zoneId, ct);
        var siblings = await _db.Zones.Where(z => z.BranchId == zone.BranchId)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Id).ToListAsync(ct);

        var index = siblings.FindIndex(z => z.Id == zoneId);
        var target = up ? index - 1 : index + 1;
        if (target < 0 || target >= siblings.Count)
        {
            return; // đã ở đầu/cuối
        }

        // Đánh số lại liên tục rồi đổi chỗ, tránh trường hợp hai khu vực trùng SortOrder.
        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SortOrder = i + 1;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Zone> FindZoneAsync(int zoneId, CancellationToken ct) =>
        await _db.Zones.FirstOrDefaultAsync(z => z.Id == zoneId, ct)
        ?? throw new NotFoundException("Không tìm thấy khu vực.");

    private async Task EnsureUniqueZoneAsync(int branchId, string name, int? exceptId, CancellationToken ct)
    {
        if (await _db.Zones.AnyAsync(z => z.BranchId == branchId && z.Name == name && z.Id != exceptId, ct))
        {
            throw new FieldValidationException("name", $"Chi nhánh đã có khu vực \"{name}\".");
        }
    }

    // ------------------------------------------------------------------ Bàn

    public async Task<int> CreateTableAsync(int zoneId, TableFormModel model, CancellationToken ct = default)
    {
        var zone = await FindZoneAsync(zoneId, ct); // ZoneId của tenant khác → 404, không tạo bàn
        var name = model.Name.Trim();
        await EnsureUniqueTableAsync(zone, name, exceptId: null, ct);

        var table = new DiningTable
        {
            ZoneId = zone.Id, Name = name, Seats = model.Seats, IsActive = model.IsActive, Status = TableStatus.Empty
        };
        _db.DiningTables.Add(table);
        await _db.SaveChangesAsync(ct);
        return table.Id;
    }

    public async Task UpdateTableAsync(int tableId, TableFormModel model, CancellationToken ct = default)
    {
        var table = await FindTableAsync(tableId, ct);
        var currentZone = await FindZoneAsync(table.ZoneId, ct);

        var zone = currentZone;
        if (model.ZoneId.HasValue && model.ZoneId.Value != table.ZoneId)
        {
            zone = await FindZoneAsync(model.ZoneId.Value, ct); // ZoneId của tenant khác → 404
            if (zone.BranchId != currentZone.BranchId)
            {
                throw new BusinessRuleException("Chỉ chuyển được bàn sang khu vực trong cùng chi nhánh.");
            }
        }

        var name = model.Name.Trim();
        await EnsureUniqueTableAsync(zone, name, exceptId: tableId, ct);
        if (table.IsActive && !model.IsActive)
        {
            await EnsureNoServingOrderAsync(table, ct);
        }

        table.ZoneId = zone.Id;
        table.Name = name;
        table.Seats = model.Seats;
        table.IsActive = model.IsActive;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>R12: ẩn bàn thay vì xóa; không ẩn bàn đang có đơn phục vụ.</summary>
    public async Task SetTableActiveAsync(int tableId, bool isActive, CancellationToken ct = default)
    {
        var table = await FindTableAsync(tableId, ct);
        if (!isActive)
        {
            await EnsureNoServingOrderAsync(table, ct);
        }

        table.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<DiningTable> FindTableAsync(int tableId, CancellationToken ct) =>
        await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
        ?? throw new NotFoundException("Không tìm thấy bàn.");

    private async Task EnsureNoServingOrderAsync(DiningTable table, CancellationToken ct)
    {
        if (await _db.Orders.AnyAsync(o => o.TableId == table.Id && o.Status == OrderStatus.Serving, ct))
        {
            throw new BusinessRuleException($"{table.Name} đang có đơn phục vụ. Hãy thanh toán hoặc chuyển bàn trước khi ẩn.");
        }
    }

    /// <summary>Tên bàn duy nhất trong khu vực, kể cả bàn đang ẩn (khớp chỉ mục UNIQUE (ZoneId, Name)).</summary>
    private async Task EnsureUniqueTableAsync(Zone zone, string name, int? exceptId, CancellationToken ct)
    {
        if (await _db.DiningTables.AnyAsync(t => t.ZoneId == zone.Id && t.Name == name && t.Id != exceptId, ct))
        {
            throw new FieldValidationException("name", $"Khu vực {zone.Name} đã có {name}.");
        }
    }
}
