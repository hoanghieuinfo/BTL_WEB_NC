// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Security.Cryptography;
using BTL_WEB_NC.Areas.Manage.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IStaffService
{
    Task<IReadOnlyList<BranchOption>> ActiveBranchesAsync(CancellationToken ct = default);
    Task<StaffListResult> ListAsync(StaffListQuery query, CancellationToken ct = default);
    Task<StaffCreatedResult> CreateAsync(StaffFormModel model, CancellationToken ct = default);
    Task UpdateAsync(string id, StaffEditModel model, CancellationToken ct = default);
    Task<StaffCreatedResult> ResetPasswordAsync(string id, CancellationToken ct = default);
    Task TransferAsync(string id, int branchId, CancellationToken ct = default);
    Task LockAsync(string id, CancellationToken ct = default);
    Task UnlockAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Nhân viên (P04, R17, BM08). AppUser KHÔNG có Global Query Filter (lúc đăng nhập chưa biết tenant – mục 3.2.2a),
/// nên mọi truy vấn tài khoản phải đi qua <see cref="StaffQuery"/>: lọc TenantId của chủ quán VÀ vai trò Staff.
/// Nhờ vậy nhân viên quán khác, chủ quán và Super Admin luôn "không tồn tại" (→ 404) với service này.
/// </summary>
public class StaffService : IStaffService
{
    // Bỏ ký tự dễ nhầm khi đọc cho nhân viên: 0/O, 1/l/I.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly TenantContext _tenant;
    private readonly IPlanUsageService _usage;

    public StaffService(AppDbContext db, UserManager<AppUser> users, TenantContext tenant, IPlanUsageService usage)
    {
        _db = db;
        _users = users;
        _tenant = tenant;
        _usage = usage;
    }

    private int TenantId => _tenant.TenantId ?? throw new InvalidOperationException("Không xác định được tenant.");

    /// <summary>Điểm lọc duy nhất cho tài khoản nhân viên của quán hiện tại.</summary>
    private IQueryable<AppUser> StaffQuery()
    {
        var tenantId = TenantId;
        var staffRoleId = _db.Roles.Where(r => r.Name == AppRoles.Staff).Select(r => r.Id);
        return _db.Users.Where(u =>
            u.TenantId == tenantId
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && staffRoleId.Contains(ur.RoleId)));
    }

    public async Task<IReadOnlyList<BranchOption>> ActiveBranchesAsync(CancellationToken ct = default) =>
        await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Id)
            .Select(b => new BranchOption(b.Id, b.Name, b.Address))
            .ToListAsync(ct);

    // ------------------------------------------------------------------ Danh sách

    public async Task<StaffListResult> ListAsync(StaffListQuery query, CancellationToken ct = default)
    {
        var staff = StaffQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim();
            staff = staff.Where(u => u.FullName.Contains(keyword)
                || (u.Email != null && u.Email.Contains(keyword))
                || (u.PhoneNumber != null && u.PhoneNumber.Contains(keyword)));
        }
        if (query.BranchId.HasValue)
        {
            staff = staff.Where(u => u.BranchId == query.BranchId.Value);
        }
        staff = query.Status switch
        {
            "active" => staff.Where(u => u.IsActive && !u.MustChangePassword),
            "pending" => staff.Where(u => u.IsActive && u.MustChangePassword),
            "locked" => staff.Where(u => !u.IsActive),
            _ => staff
        };

        var total = await staff.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)StaffListQuery.PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);

        var items = await staff
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Skip((page - 1) * StaffListQuery.PageSize)
            .Take(StaffListQuery.PageSize)
            .Select(u => new StaffRow(
                u.Id, u.FullName, u.Email ?? "", u.PhoneNumber, u.BranchId,
                u.Branch != null ? u.Branch.Name : null, // Branches có bộ lọc tenant
                u.StartDate, u.IsActive, u.MustChangePassword))
            .ToListAsync(ct);

        return new StaffListResult(items, page, total, query);
    }

    // ------------------------------------------------------------------ Thêm nhân viên

    public async Task<StaffCreatedResult> CreateAsync(StaffFormModel model, CancellationToken ct = default)
    {
        await EnsureStaffSlotAsync(ct); // R17
        var branch = await FindActiveBranchAsync(model.BranchId!.Value, ct);

        var email = model.Email.Trim();
        // Email duy nhất TOÀN HỆ THỐNG (không chỉ trong quán). Thông báo không nói tài khoản đó thuộc quán nào.
        if (await _users.FindByEmailAsync(email) is not null)
        {
            throw new FieldValidationException("email", "Email này đã có tài khoản trên hệ thống. Dùng email khác.");
        }

        var password = GenerateInitialPassword();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = model.FullName.Trim(),
            PhoneNumber = model.Phone.Trim(),
            TenantId = TenantId,          // từ claim phía server, không bao giờ từ request
            BranchId = branch.Id,
            StartDate = model.StartDate,
            IsActive = true,
            MustChangePassword = true      // R03: nhân viên phải đổi ở lần đăng nhập đầu (Module 1 thực hiện)
        };

        // Tạo tài khoản và gán vai trò là hai lần lưu → gói trong một transaction để không còn tài khoản "không vai trò".
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        ThrowIfFailed(await _users.CreateAsync(user, password));
        ThrowIfFailed(await _users.AddToRoleAsync(user, AppRoles.Staff)); // vai trò cố định trong code
        await transaction.CommitAsync(ct);

        return new StaffCreatedResult(user.Id, user.FullName, email, branch.Name, password);
    }

    // ------------------------------------------------------------------ Sửa, đặt lại mật khẩu

    /// <summary>Chỉ đổi họ tên và số điện thoại; email (tên đăng nhập), chi nhánh, vai trò giữ nguyên.</summary>
    public async Task UpdateAsync(string id, StaffEditModel model, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, ct);
        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.Phone.Trim();
        ThrowIfFailed(await _users.UpdateAsync(user));
    }

    /// <summary>
    /// Cấp mật khẩu khởi tạo mới (cùng hàm sinh với khi tạo tài khoản): mật khẩu cũ hết hiệu lực ngay,
    /// nhân viên phải đổi ở lần đăng nhập tới, phiên đang mở bị thu hồi qua SecurityStamp.
    /// </summary>
    public async Task<StaffCreatedResult> ResetPasswordAsync(string id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, ct);
        if (!user.IsActive)
        {
            throw new BusinessRuleException("Tài khoản đang bị khóa. Hãy mở khóa trước khi đặt lại mật khẩu.");
        }

        var password = GenerateInitialPassword();

        // Xóa mật khẩu cũ, đặt mật khẩu mới (vẫn qua PasswordValidators), đổi SecurityStamp – cùng thành công hoặc cùng hoàn tác.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        ThrowIfFailed(await _users.RemovePasswordAsync(user));
        ThrowIfFailed(await _users.AddPasswordAsync(user, password));
        user.MustChangePassword = true;
        ThrowIfFailed(await _users.UpdateSecurityStampAsync(user)); // lưu luôn MustChangePassword
        await transaction.CommitAsync(ct);

        var branchName = await _db.Branches.Where(b => b.Id == user.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        return new StaffCreatedResult(user.Id, user.FullName, user.Email ?? "", branchName ?? "Chưa phân công", password);
    }

    // ------------------------------------------------------------------ Chuyển chi nhánh, khóa, mở khóa

    /// <summary>Đổi chi nhánh rồi đổi SecurityStamp: phiên cũ (mang branch_id cũ) bị thu hồi trong ≤ 5 phút (mục 3.2.2b).</summary>
    public async Task TransferAsync(string id, int branchId, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, ct);
        var branch = await FindActiveBranchAsync(branchId, ct);
        if (user.BranchId == branch.Id)
        {
            throw new FieldValidationException("branchId", "Nhân viên đang làm ở chi nhánh này. Hãy chọn chi nhánh khác.");
        }

        user.BranchId = branch.Id;
        // UpdateSecurityStampAsync lưu luôn mọi thay đổi khác của user → một lần ghi duy nhất.
        ThrowIfFailed(await _users.UpdateSecurityStampAsync(user));
    }

    /// <summary>Khóa = đánh dấu Đã nghỉ (không xóa – giữ lịch sử hóa đơn, R42) và thu hồi phiên đang mở.</summary>
    public async Task LockAsync(string id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, ct);
        if (!user.IsActive)
        {
            return;
        }

        user.IsActive = false;
        ThrowIfFailed(await _users.UpdateSecurityStampAsync(user));
    }

    /// <summary>Mở khóa cũng chiếm lại một suất của gói → kiểm tra MaxStaff như khi thêm mới (CLAUDE.md mục 6).</summary>
    public async Task UnlockAsync(string id, CancellationToken ct = default)
    {
        var user = await FindStaffAsync(id, ct);
        if (user.IsActive)
        {
            return;
        }

        await EnsureStaffSlotAsync(ct);
        user.IsActive = true;
        ThrowIfFailed(await _users.UpdateSecurityStampAsync(user));
    }

    // ------------------------------------------------------------------ Hàm phụ

    private async Task<AppUser> FindStaffAsync(string id, CancellationToken ct) =>
        await StaffQuery().FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Không tìm thấy nhân viên.");

    /// <summary>BranchId từ request phải thuộc quán (bộ lọc của Branches) và đang hoạt động.</summary>
    private async Task<Branch> FindActiveBranchAsync(int branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");
        if (!branch.IsActive)
        {
            throw new FieldValidationException("branchId", $"Chi nhánh {branch.Name} đang tạm ngưng. Hãy chọn chi nhánh đang hoạt động.");
        }
        return branch;
    }

    private async Task EnsureStaffSlotAsync(CancellationToken ct)
    {
        var usage = await _usage.GetAsync(ct);
        if (usage.StaffCount >= usage.MaxStaff)
        {
            throw new BusinessRuleException(
                $"Đã dùng hết {usage.StaffCount}/{usage.MaxStaff} nhân viên của gói {usage.PlanName}. Khóa bớt tài khoản đã nghỉ hoặc nâng cấp gói.");
        }
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Mật khẩu khởi tạo dạng "Xxxx-xxxx-xxxx" sinh bằng RandomNumberGenerator (ngẫu nhiên mật mã, không dùng Random).
    /// Mỗi nhóm bảo đảm có chữ hoa/thường/số nên luôn đạt chính sách ≥ 8 ký tự, có hoa, thường, số (Bảng 3.22).
    /// </summary>
    public static string GenerateInitialPassword()
    {
        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
        const string all = Upper + Lower + Digits;

        string Group(params string[] sets)
        {
            var chars = sets.Select(Pick).ToArray();
            RandomNumberGenerator.Shuffle(chars.AsSpan());
            return new string(chars);
        }

        return string.Join('-',
            Group(Upper, Lower, Digits, all),
            Group(Upper, Lower, Digits, all),
            Group(Upper, Lower, Digits, all));
    }
}
