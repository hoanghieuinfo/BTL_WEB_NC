// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Areas.Manage.Services;
using BTL_WEB_NC.Areas.Pos.Models;
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure;
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Areas.Pos.Services;

public interface IOrderService
{
    /// <summary>A12: mở đơn cho bàn trống của chi nhánh.</summary>
    Task<OpenedOrderDto> OpenAsync(int branchId, string userId, OpenOrderModel model, CancellationToken ct = default);

    /// <summary>MH15: chi tiết một đơn của chi nhánh. Đơn không có, của chi nhánh khác hoặc của quán khác → NotFoundException (404).</summary>
    Task<OrderDetailDto> GetAsync(int branchId, long orderId, CancellationToken ct = default);

    /// <summary>A13: thêm một dòng món mới hoặc sửa số lượng/ghi chú của một dòng; trả đơn sau khi tính lại tổng tiền.</summary>
    Task<OrderDetailDto> SaveItemAsync(int branchId, long orderId, SaveOrderItemModel model, CancellationToken ct = default);

    /// <summary>A14: hủy một dòng món (bắt buộc lý do). Dòng được giữ lại với IsCancelled = true (R42); trả đơn sau khi tính lại tổng tiền.</summary>
    Task<OrderDetailDto> CancelItemAsync(int branchId, long orderId, long itemId, CancelOrderItemModel model, CancellationToken ct = default);

    /// <summary>A15: chuyển đơn sang một bàn trống khác của cùng chi nhánh. Bàn cũ về Trống, bàn mới nhận trạng thái của đơn.</summary>
    Task<OrderDetailDto> TransferAsync(int branchId, long orderId, MoveOrderModel model, CancellationToken ct = default);

    /// <summary>A15: gộp đơn vào đơn đang phục vụ của bàn khác. Mọi dòng món chuyển sang đơn đích, đơn nguồn bị hủy (lý do "Gộp vào đơn …").</summary>
    Task<MergedOrderDto> MergeAsync(int branchId, long orderId, MoveOrderModel model, CancellationToken ct = default);

    /// <summary>A29: tạm tính – bàn sang Chờ thanh toán (P05 bước 5). Gọi lại khi bàn đã Chờ thanh toán thì giữ nguyên (in lại phiếu). Đơn phải còn ít nhất một món chưa hủy.</summary>
    Task<OrderDetailDto> ProvisionalAsync(int branchId, long orderId, CancellationToken ct = default);

    /// <summary>A30: hủy cả đơn. Đơn chưa có món chưa hủy thì hủy luôn; đã có món thì bắt buộc lý do (R25). Đơn chuyển Cancelled, bàn về Trống.</summary>
    Task<OrderDetailDto> CancelOrderAsync(int branchId, long orderId, CancelOrderModel model, CancellationToken ct = default);

    /// <summary>
    /// MH16: dữ liệu phiếu in. Đơn Đang phục vụ → phiếu tạm tính; Đã thanh toán → hóa đơn BM06. Đơn đã hủy, của chi nhánh khác hoặc quán khác
    /// → NotFoundException (404): đơn hủy không có gì để in.
    /// </summary>
    Task<PrintOrderDto> GetPrintAsync(int branchId, long orderId, CancellationToken ct = default);

    /// <summary>
    /// MH17 (R28): hóa đơn Đã thanh toán của chi nhánh trong ngày <paramref name="day"/> (giờ Việt Nam), mới nhất trước.
    /// Chỉ đọc; chi nhánh khác và quán khác không bao giờ xuất hiện.
    /// </summary>
    Task<InvoiceListDto> ListPaidAsync(int branchId, DateOnly day, CancellationToken ct = default);
}

/// <summary>
/// MH15 – nghiệp vụ đơn hàng (P05, R23). branchId và userId do controller lấy từ claim.
/// Mọi truy vấn đi qua DbSet đã có Global Query Filter theo tenant.
/// </summary>
public class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OpenedOrderDto> OpenAsync(int branchId, string userId, OpenOrderModel model, CancellationToken ct = default)
    {
        var tableId = model.TableId!.Value;

        // Bàn phải thuộc chi nhánh của nhân viên và còn hoạt động; ngược lại coi như không tồn tại (404).
        var table = await _db.DiningTables
            .FirstOrDefaultAsync(t => t.Id == tableId && t.IsActive && t.Zone.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy bàn.");

        var hasOpenOrder = await _db.Orders.AnyAsync(o => o.TableId == tableId && o.Status == OrderStatus.Serving, ct);
        if (table.Status != TableStatus.Empty || hasOpenOrder)
        {
            throw new ConflictException("Bàn đã có khách. Hãy tải lại sơ đồ bàn.");
        }

        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");
        if (!branch.IsActive)
        {
            throw new BusinessRuleException("Chi nhánh đang tạm ngưng hoạt động, không thể mở đơn mới.");
        }

        var branchCode = string.IsNullOrWhiteSpace(branch.Code) ? BranchCode.FromName(branch.Name) : branch.Code;

        var now = VietnamTime.Now;
        var prefix = $"HD-{branchCode}-{now:yyMMdd}-";
        var sequence = await _db.Orders.CountAsync(o => o.BranchId == branchId && o.Code.StartsWith(prefix), ct) + 1;

        var order = new Order
        {
            BranchId = branchId,
            TableId = tableId,
            Code = $"{prefix}{sequence:000}",
            Status = OrderStatus.Serving,
            GuestCount = model.GuestCount,
            CreatedById = userId,
            CreatedAt = now,
            SubTotal = 0,
            Discount = 0,
            Total = 0
        };
        _db.Orders.Add(order);
        table.Status = TableStatus.Serving;

        try
        {
            // Một SaveChanges là một giao dịch: thêm đơn và đổi trạng thái bàn cùng thành công hoặc cùng hoàn tác.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            // Trùng UQ(BranchId, Code) khi hai máy mở đơn cùng lúc.
            throw new ConflictException("Không tạo được đơn do trùng số hóa đơn. Hãy thử lại.");
        }

        return new OpenedOrderDto(order.Id, order.Code);
    }

    public async Task<OrderDetailDto> GetAsync(int branchId, long orderId, CancellationToken ct = default)
    {
        // Lọc tenant do Global Query Filter; lọc chi nhánh bằng branchId từ claim. Không thấy → 404, không phân biệt
        // "không tồn tại" với "của người khác" để không lộ đơn đó có thật.
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.BranchId == branchId)
            .Select(o => new
            {
                o.Id, o.Code, o.Status, o.TableId,
                TableName = o.Table.Name,
                TableStatus = o.Table.Status,
                ZoneName = o.Table.Zone.Name,
                BranchName = o.Branch.Name,
                StaffName = o.CreatedBy.FullName,
                o.CreatedAt, o.GuestCount, o.SubTotal, o.Discount, o.Total, o.RowVersion
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.Id)
            .Select(i => new OrderLineDto(i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Note, i.IsCancelled, i.CancelReason))
            .ToListAsync(ct);

        return new OrderDetailDto(
            order.Id, order.Code, order.Status,
            order.TableId, order.TableName, order.TableStatus, order.ZoneName,
            order.BranchName, order.StaffName, order.CreatedAt, order.GuestCount,
            lines, order.SubTotal, order.Discount, order.Total, Convert.ToBase64String(order.RowVersion));
    }

    public async Task<OrderDetailDto> SaveItemAsync(int branchId, long orderId, SaveOrderItemModel model, CancellationToken ct = default)
    {
        // Đơn qua DbSet đã lọc tenant + chi nhánh từ claim; khác chi nhánh/quán → 404. Tải kèm món và bàn để cập nhật cùng lúc.
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        var quantity = model.Quantity!.Value;
        var note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();

        if (model.ProductId is int productId)
        {
            // Khóa ngoại từ request: tra qua DbSet đã lọc (quán khác hoặc đã xóa mềm → 404).
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new NotFoundException("Không tìm thấy món.");
            if (!product.IsAvailable)
            {
                throw new BusinessRuleException("Món này đang ngừng bán.");
            }

            // Mỗi lần gọi là một dòng mới; lưu tên và đơn giá lúc bán để sửa giá món sau này không làm đổi đơn.
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity,
                Note = note
            });
        }
        else
        {
            var item = order.Items.FirstOrDefault(i => i.Id == model.ItemId)
                ?? throw new NotFoundException("Không tìm thấy món trong đơn.");
            if (item.IsCancelled)
            {
                throw new ConflictException("Món này đã bị hủy. Hãy tải lại đơn.");
            }

            item.Quantity = quantity;
            item.Note = note;
        }

        // Tổng tiền luôn tính lại từ các dòng chưa hủy, không tin số từ trình duyệt.
        order.SubTotal = order.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
        order.Total = Math.Max(0, order.SubTotal - order.Discount);

        // Gọi thêm/sửa sau khi đã in tạm tính thì phiếu cũ không còn đúng → bàn quay lại Đang phục vụ (P05 bước 5).
        if (order.Table.Status == TableStatus.AwaitingPayment)
        {
            order.Table.Status = TableStatus.Serving;
        }

        // Một SaveChanges = một giao dịch. Hai thiết bị cùng sửa đơn: người sau gặp DbUpdateConcurrencyException (RowVersion) → 409.
        await _db.SaveChangesAsync(ct);
        return await GetAsync(branchId, orderId, ct);
    }

    public async Task<OrderDetailDto> CancelItemAsync(int branchId, long orderId, long itemId, CancelOrderItemModel model, CancellationToken ct = default)
    {
        // Đơn qua DbSet đã lọc tenant + chi nhánh từ claim; khác chi nhánh/quán → 404.
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        // Dòng phải thuộc đúng đơn trên URL; dòng của đơn khác (kể cả đơn quán khác) không nằm trong order.Items → 404.
        var item = order.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Không tìm thấy món trong đơn.");
        if (item.IsCancelled)
        {
            throw new ConflictException("Món này đã bị hủy trước đó. Hãy tải lại đơn.");
        }

        var reason = model.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new FieldValidationException(nameof(model.Reason), "Vui lòng nhập lý do hủy món.");
        }

        // Không xóa dòng (R42): chỉ đánh dấu hủy để hóa đơn và báo cáo vẫn truy vết được. Món hủy không tính tiền và không bị trừ kho.
        item.IsCancelled = true;
        item.CancelReason = reason;

        order.SubTotal = order.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
        // Giảm giá không được vượt tổng mới; ở bước này đơn chưa có giảm giá (A16 mới đặt) nên chỉ chặn âm.
        order.Total = Math.Max(0, order.SubTotal - order.Discount);

        // Đã in tạm tính mà món thay đổi thì phiếu cũ sai → bàn quay lại Đang phục vụ (P05 bước 5), giống A13.
        if (order.Table.Status == TableStatus.AwaitingPayment)
        {
            order.Table.Status = TableStatus.Serving;
        }

        // Một SaveChanges = một giao dịch; RowVersion chặn hai máy cùng sửa đơn (409 qua filter).
        await _db.SaveChangesAsync(ct);
        return await GetAsync(branchId, orderId, ct);
    }

    public async Task<OrderDetailDto> TransferAsync(int branchId, long orderId, MoveOrderModel model, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        var targetId = model.TargetTableId!.Value;
        if (targetId == order.TableId)
        {
            throw new BusinessRuleException("Đơn đang ở bàn này rồi. Hãy chọn bàn khác.");
        }

        var target = await FindActiveTableAsync(branchId, targetId, ct);

        // Bàn đích phải thật sự trống: cả trạng thái bàn lẫn không còn đơn đang phục vụ (thiết bị khác có thể vừa mở đơn).
        var occupied = target.Status != TableStatus.Empty
            || await _db.Orders.AnyAsync(o => o.TableId == targetId && o.Status == OrderStatus.Serving, ct);
        if (occupied)
        {
            throw new ConflictException("Bàn đích đã có khách. Hãy chọn bàn khác.");
        }

        // Bàn mới nhận trạng thái của đơn (Đang phục vụ hoặc Chờ thanh toán – phiếu tạm tính vẫn còn đúng vì món không đổi).
        var source = order.Table;
        target.Status = source.Status == TableStatus.AwaitingPayment ? TableStatus.AwaitingPayment : TableStatus.Serving;
        source.Status = TableStatus.Empty;
        order.Table = target;
        order.TableId = target.Id;

        // Một SaveChanges = một giao dịch: đổi bàn của đơn và trạng thái hai bàn cùng thành công hoặc cùng hoàn tác.
        // Hai thiết bị cùng sửa đơn → DbUpdateConcurrencyException (RowVersion) → 409.
        await _db.SaveChangesAsync(ct);
        return await GetAsync(branchId, orderId, ct);
    }

    public async Task<MergedOrderDto> MergeAsync(int branchId, long orderId, MoveOrderModel model, CancellationToken ct = default)
    {
        var source = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (source.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        var targetTableId = model.TargetTableId!.Value;
        if (targetTableId == source.TableId)
        {
            throw new BusinessRuleException("Không thể gộp đơn vào chính bàn này. Hãy chọn bàn khác.");
        }

        var targetTable = await FindActiveTableAsync(branchId, targetTableId, ct);

        // Bàn đích phải đang có đơn Serving; nếu thiếu (khách vừa thanh toán/chuyển đi) → 409 để tải lại sơ đồ.
        var target = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.TableId == targetTableId && o.BranchId == branchId && o.Status == OrderStatus.Serving)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Bàn đích chưa có đơn đang phục vụ. Hãy tải lại sơ đồ bàn.");

        // Chuyển cả dòng đã hủy (R42: giữ lại để truy vết). Duyệt trên bản sao vì tập hợp bị đổi khi chuyển dòng.
        foreach (var item in source.Items.ToList())
        {
            item.Order = target;
            item.OrderId = target.Id;
        }

        // Gán navigation chỉ được EF "fixup" khi DetectChanges chạy; gọi ngay để target.Items đã có các dòng vừa chuyển trước khi cộng tổng.
        _db.ChangeTracker.DetectChanges();

        target.SubTotal = target.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
        target.Total = Math.Max(0, target.SubTotal - target.Discount);

        // Đơn nguồn không còn món nào: đóng lại với lý do, tổng về 0; bàn nguồn trống.
        source.SubTotal = 0;
        source.Total = 0;
        source.Status = OrderStatus.Cancelled;
        source.CancelReason = $"Gộp vào đơn {target.Code}";
        source.Table.Status = TableStatus.Empty;

        // Hóa đơn đích vừa đổi nội dung nên nếu đã in tạm tính thì phiếu cũ không còn đúng → Đang phục vụ (P05 bước 5).
        targetTable.Status = TableStatus.Serving;

        // Một SaveChanges = một giao dịch: chuyển món, hủy đơn nguồn, đổi trạng thái hai bàn cùng thành công hoặc cùng hoàn tác.
        await _db.SaveChangesAsync(ct);
        return new MergedOrderDto(target.Id, target.Code, targetTable.Name);
    }

    public async Task<OrderDetailDto> ProvisionalAsync(int branchId, long orderId, CancellationToken ct = default)
    {
        // Đơn qua DbSet đã lọc tenant + chi nhánh từ claim; khác chi nhánh/quán → 404.
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        // Phiếu tạm tính không có món nào thì vô nghĩa (và bước thanh toán cũng chặn đơn như vậy).
        if (!order.Items.Any(i => !i.IsCancelled))
        {
            throw new BusinessRuleException("Đơn chưa có món nào nên chưa thể tạm tính.");
        }

        order.Table.Status = TableStatus.AwaitingPayment;

        // Bước này chỉ đổi Bàn nên EF sẽ không đụng tới dòng Orders → RowVersion không bị kiểm tra. Đánh dấu SubTotal đã sửa để EF vẫn UPDATE
        // dòng đơn: nếu máy khác vừa thêm/hủy món (RowVersion đã đổi) thì ném DbUpdateConcurrencyException → 409, tránh in phiếu cũ.
        _db.Entry(order).Property(o => o.SubTotal).IsModified = true;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(branchId, orderId, ct);
    }

    public async Task<OrderDetailDto> CancelOrderAsync(int branchId, long orderId, CancelOrderModel model, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId, ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        if (order.Status != OrderStatus.Serving)
        {
            throw new ConflictException("Đơn đã được thanh toán hoặc đã hủy. Hãy tải lại.");
        }

        // "Đã có món" tính theo món chưa hủy: món đã hủy từng có lý do riêng (A14), đơn chỉ còn dòng hủy thì coi như chưa có món.
        var hasActiveItems = order.Items.Any(i => !i.IsCancelled);
        var reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim();
        if (hasActiveItems && reason is null)
        {
            throw new FieldValidationException(nameof(model.Reason), "Vui lòng nhập lý do hủy đơn.");
        }

        // Không xóa đơn và dòng món (R42). Chưa thanh toán nên chưa trừ kho, không có gì để hoàn lại.
        order.Status = OrderStatus.Cancelled;
        order.CancelReason = reason;
        order.Table.Status = TableStatus.Empty;

        // Một SaveChanges = một giao dịch: đơn và bàn cùng đổi. RowVersion chặn trường hợp máy khác vừa thanh toán/sửa đơn (409 qua filter).
        await _db.SaveChangesAsync(ct);
        return await GetAsync(branchId, orderId, ct);
    }

    public async Task<PrintOrderDto> GetPrintAsync(int branchId, long orderId, CancellationToken ct = default)
    {
        // Lọc tenant do Global Query Filter, lọc chi nhánh bằng branchId từ claim: khác chi nhánh/quán → 404 (không lộ đơn có thật).
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.BranchId == branchId && o.Status != OrderStatus.Cancelled)
            .Select(o => new
            {
                o.Id, o.Code, o.Status, o.TenantId,
                BranchName = o.Branch.Name, BranchAddress = o.Branch.Address, BranchPhone = o.Branch.Phone,
                TableName = o.Table.Name, ZoneName = o.Table.Zone.Name,
                CreatedByName = o.CreatedBy.FullName,
                CashierName = o.Cashier != null ? o.Cashier.FullName : null,
                o.CreatedAt, o.PaidAt, o.GuestCount,
                o.SubTotal, o.Discount, o.DiscountReason, o.Total, o.PaymentMethod, o.CashReceived
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng.");

        // Tenants không phải ITenantEntity (không bị Global Query Filter) nên lọc theo TenantId của chính đơn đã tìm thấy.
        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == order.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "";

        // Món đã hủy không tính tiền nên không in cho khách (vẫn còn trong CSDL – R42).
        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId && !i.IsCancelled)
            .OrderBy(i => i.Id)
            .Select(i => new PrintLineDto(i.ProductName, i.Note, i.Quantity, i.UnitPrice, i.UnitPrice * i.Quantity))
            .ToListAsync(ct);

        if (order.Status == OrderStatus.Paid)
        {
            // Hóa đơn BM06: các con số đã chốt lúc thanh toán (A16), không tính lại.
            return new PrintOrderDto(
                order.Id, order.Code, order.Status, tenantName, order.BranchName, order.BranchAddress, order.BranchPhone,
                order.TableName, order.ZoneName, order.CashierName ?? order.CreatedByName, order.PaidAt ?? order.CreatedAt, order.GuestCount,
                lines, order.SubTotal, order.Discount, order.DiscountReason, order.Total, order.PaymentMethod, order.CashReceived);
        }

        // Phiếu tạm tính: đơn còn mở, chưa có giảm giá (đặt lúc thanh toán) → tổng tính từ chính các dòng sẽ in để phiếu luôn khớp với số món in ra.
        var subTotal = lines.Sum(l => l.Amount);
        return new PrintOrderDto(
            order.Id, order.Code, order.Status, tenantName, order.BranchName, order.BranchAddress, order.BranchPhone,
            order.TableName, order.ZoneName, order.CreatedByName, VietnamTime.Now, order.GuestCount,
            lines, subTotal, 0m, null, subTotal, null, null);
    }

    public async Task<InvoiceListDto> ListPaidAsync(int branchId, DateOnly day, CancellationToken ct = default)
    {
        // PaidAt được lưu theo giờ Việt Nam (A16 dùng VietnamTime.Now) nên so sánh thẳng với mốc [00:00 hôm nay, 00:00 hôm sau);
        // dùng khoảng nửa mở thay cho .Date để truy vấn dùng được chỉ mục và không bỏ sót đơn lúc 23:59:59.
        var from = day.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);

        // Tenant do Global Query Filter, chi nhánh do branchId lấy từ claim. Chỉ đơn Paid (đơn Hủy/Đang phục vụ không phải hóa đơn).
        var items = await _db.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Paid && o.PaidAt >= from && o.PaidAt < to)
            .OrderByDescending(o => o.PaidAt).ThenByDescending(o => o.Id)
            .Select(o => new InvoiceRowDto(
                o.Id, o.Code, o.Table.Name, o.Table.Zone.Name, o.PaidAt!.Value,
                o.PaymentMethod, o.Cashier != null ? o.Cashier.FullName : null, o.Discount, o.Total))
            .ToListAsync(ct);

        return new InvoiceListDto(day, items, items.Sum(i => i.Total));
    }

    // Bàn đích từ request: phải đang hoạt động và thuộc chi nhánh của nhân viên (tenant do Global Query Filter); ngược lại → 404.
    private async Task<DiningTable> FindActiveTableAsync(int branchId, int tableId, CancellationToken ct) =>
        await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId && t.IsActive && t.Zone.BranchId == branchId, ct)
        ?? throw new NotFoundException("Không tìm thấy bàn.");
}
