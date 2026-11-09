// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Pos.Services;

public interface IPaymentService
{
    /// <summary>
    /// A16 (UC16, R26, R30): thanh toán đơn và trừ kho trong MỘT giao dịch. Server tính lại tổng tiền từ chi tiết đơn trong CSDL.
    /// Lỗi → toàn bộ hoàn tác (đơn, tồn kho, bàn). Đơn đã thanh toán/hủy hoặc RowVersion lệch → ConflictException / DbUpdateConcurrencyException (409).
    /// </summary>
    Task<PaymentResultDto> PayAsync(int branchId, string cashierId, long orderId, PayOrderModel model, CancellationToken ct = default);
}

/// <summary>
/// MH15 – thanh toán (P05 bước 6–7). branchId và cashierId do controller lấy từ claim, không nhận từ request.
/// Gọi <see cref="IInventoryDeductionService"/> (Việc 12) bên trong transaction của chính service này, đúng docs/Viec12_HuongDanTrung.md.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IInventoryDeductionService _inventory;

    public PaymentService(AppDbContext db, IInventoryDeductionService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<PaymentResultDto> PayAsync(int branchId, string cashierId, long orderId, PayOrderModel model, CancellationToken ct = default)
    {
        // Mọi bước ghi nằm trong một transaction. Thoát khỏi khối này mà chưa Commit (do lỗi hay ngoại lệ) thì DisposeAsync tự rollback:
        // đơn, tồn kho và trạng thái bàn cùng trở về như cũ.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Đơn qua DbSet đã lọc tenant + chi nhánh từ claim; khác chi nhánh/quán → 404. Chi tiết đơn đọc từ CSDL để tự tính tiền.
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        // Bấm "Thanh toán" lần hai (hoặc máy khác đã thanh toán/hủy) → 409; kho không bị trừ thêm.
        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        // Gắn RowVersion gốc do trình duyệt gửi: nếu đơn đã đổi kể từ lúc trang được vẽ (thêm/hủy món, chuyển bàn…) thì UPDATE không khớp
        // dòng nào → DbUpdateConcurrencyException → 409, nhân viên không trả nhầm một hóa đơn đã khác.
        _db.Entry(order).Property(o => o.RowVersion).OriginalValue = ParseRowVersion(model.RowVersion);

        // UC16: đơn phải có ít nhất một món chưa hủy.
        var activeItems = order.Items.Where(i => !i.IsCancelled).ToList();
        if (activeItems.Count == 0)
        {
            throw new BusinessRuleException("Đơn chưa có món nào nên không thể thanh toán.");
        }

        // Tổng tiền tính lại từ đơn giá đã lưu lúc bán (R26): không tin con số nào từ trình duyệt.
        var subTotal = activeItems.Sum(i => i.UnitPrice * i.Quantity);

        var discount = model.Discount ?? 0m;
        var discountReason = string.IsNullOrWhiteSpace(model.DiscountReason) ? null : model.DiscountReason.Trim();
        if (discount < 0)
        {
            throw new FieldValidationException(nameof(model.Discount), "Giảm giá không được âm.");
        }
        if (discount > subTotal)
        {
            throw new FieldValidationException(nameof(model.Discount), "Giảm giá không được lớn hơn tổng tiền hàng.");
        }
        if (discount > 0 && discountReason is null)
        {
            throw new FieldValidationException(nameof(model.DiscountReason), "Vui lòng nhập lý do giảm giá.");
        }
        if (discount == 0)
        {
            discountReason = null;   // ràng buộc CK_Orders_DiscountReason chỉ cần lý do khi có giảm giá
        }

        var total = subTotal - discount;
        var method = model.Method!.Value;
        decimal? cashReceived = null;
        var change = 0m;
        if (method == PaymentMethod.Cash)
        {
            if (model.CashReceived is not decimal received)
            {
                throw new FieldValidationException(nameof(model.CashReceived), "Vui lòng nhập tiền khách đưa.");
            }
            if (received < total)
            {
                throw new FieldValidationException(nameof(model.CashReceived), "Tiền khách đưa chưa đủ để thanh toán.");
            }
            cashReceived = received;
            change = received - total;
        }
        // Chuyển khoản: không có tiền khách đưa/tiền thừa; bỏ qua giá trị trình duyệt gửi kèm.

        var paidAt = VietnamTime.Now;   // giờ Việt Nam (CLAUDE.md mục 9), nếu không báo cáo doanh thu theo ngày sẽ lệch
        order.SubTotal = subTotal;
        order.Discount = discount;
        order.DiscountReason = discountReason;
        order.Total = total;
        order.PaymentMethod = method;
        order.CashReceived = cashReceived;
        order.Status = OrderStatus.Paid;
        order.PaidAt = paidAt;
        order.CashierId = cashierId;
        order.Table.Status = TableStatus.Empty;

        // Lưu đơn + bàn trước: RowVersion của đơn lệch sẽ lộ ra ở đây, trước khi chạm tới tồn kho.
        await _db.SaveChangesAsync(ct);

        // Việc 12 (R30): trừ kho theo định lượng. Service không tự mở transaction, nằm trong transaction ở trên; xung đột tồn kho
        // (RowVersion của BranchStocks) ném DbUpdateConcurrencyException → rollback cả đơn và bàn → 409 qua PosExceptionFilter.
        await _inventory.DeductForOrderAsync(order.Id, ct);

        await transaction.CommitAsync(ct);
        return new PaymentResultDto(order.Id, order.Code, subTotal, discount, total, method, cashReceived, change, paidAt);
    }

    private static byte[] ParseRowVersion(string? value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value ?? string.Empty);
            if (bytes.Length > 0)
            {
                return bytes;
            }
        }
        catch (FormatException)
        {
            // rơi xuống thông báo chung bên dưới
        }

        throw new BusinessRuleException("Phiên bản đơn không hợp lệ. Hãy tải lại trang.");
    }
}
