<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
# Việc 12 – Gọi service trừ kho trong A16 (gửi Trung, Module 3)

Hiếu (Module 2) đã làm xong service trừ kho. Bạn gọi nó **bên trong transaction** của action thanh toán
A16 `POST /pos/orders/{id}/pay`. Tài liệu này mô tả service hứa gì, bạn cần làm gì và có đoạn code mẫu.

## 1. Chữ ký

```csharp
// BTL_WEB_NC/Areas/Manage/Services/InventoryDeductionService.cs
namespace BTL_WEB_NC.Areas.Manage.Services;

public interface IInventoryDeductionService
{
    Task DeductForOrderAsync(long orderId, CancellationToken ct = default);
}
```

Service đã được đăng ký DI (scoped) trong `Program.cs`:

```csharp
builder.Services.AddScoped<IInventoryDeductionService, InventoryDeductionService>();
```

Bạn chỉ cần nhận `IInventoryDeductionService` qua constructor của controller hoặc service POS.

## 2. Service làm gì

| Việc | Cách làm |
|---|---|
| Tìm đơn | Qua `_db.Orders`, đã lọc theo tenant của nhân viên. Đơn của quán khác: ném `NotFoundException`. |
| Món được tính | Chỉ món `IsCancelled = false`. Các dòng cùng món (gọi thêm nhiều lần) được cộng lại. |
| Món chưa định lượng | Bỏ qua, không lỗi (R15: "bán không trừ kho"). |
| Lượng trừ | Cộng theo từng nguyên liệu: `Recipe.Quantity × số lượng món` (đơn vị cơ sở). Trừ vào chi nhánh `order.BranchId`. |
| Chưa có dòng tồn | Tạo dòng `BranchStocks` với tồn 0, rồi trừ. |
| Tồn âm | Cho phép (P07 bước 3). Không chặn bán hàng. |
| `UpdatedAt` | Ghi `VietnamTime.Now`. |
| Transaction | **Không** tự mở và **không** commit. Service có gọi `SaveChangesAsync`, nhưng trên cùng `AppDbContext` nên nằm trong transaction của bạn. |
| Xung đột | Nếu dòng tồn vừa bị request khác sửa (RowVersion), service ném `DbUpdateConcurrencyException`. Service không tự bắt lỗi này. |

**Service không đánh dấu đơn "đã trừ kho".** Mỗi đơn chỉ được gọi **một lần**. Bạn đã kiểm tra trạng thái
`Serving` và RowVersion của `Orders` trước khi gọi, nên hai lần bấm "Thanh toán" sẽ không trừ kho hai lần.

## 3. Đoạn code mẫu cho A16

```csharp
// [AI] Claude hỗ trợ – Phụ lục Z.7
[HttpPost("pos/orders/{id:long}/pay")]
public async Task<IActionResult> Pay(long id, [FromBody] PayModel model, CancellationToken ct)
{
    if (!ModelState.IsValid) return BadRequest(ApiResult.ValidationFailed(ModelState));

    await using var transaction = await _db.Database.BeginTransactionAsync(ct);
    try
    {
        // 1. Tải đơn qua DbSet đã lọc tenant; chỉ đơn của chi nhánh nhân viên (BranchId lấy từ claim).
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == _tenant.BranchId, ct);
        if (order is null) return NotFound(ApiResult.Fail("Không tìm thấy đơn hàng."));
        if (order.Status != OrderStatus.Serving) return Conflict(ApiResult.Fail("Đơn đã được thanh toán hoặc đã hủy."));

        // Bắt buộc: RowVersion trình duyệt gửi lên làm giá trị gốc để chống thanh toán trùng.
        _db.Entry(order).Property(o => o.RowVersion).OriginalValue = Convert.FromBase64String(model.RowVersion);

        // 2. Cập nhật đơn (BM06). PaidAt PHẢI dùng VietnamTime.Now, nếu không báo cáo MH08/MH13 lệch ngày.
        order.Status = OrderStatus.Paid;
        order.PaidAt = VietnamTime.Now;
        order.CashierId = _userManager.GetUserId(User);
        // ... SubTotal, Discount, Total, PaymentMethod, CashReceived theo nghiệp vụ của bạn

        // 3. Trừ kho (Việc 12). Hàm này có gọi SaveChanges nên đơn đã sửa ở bước 2 cũng được lưu,
        //    vẫn bên trong transaction.
        await _inventory.DeductForOrderAsync(order.Id, ct);

        // 4. Bàn về Trống.
        var table = await _db.DiningTables.FirstAsync(t => t.Id == order.TableId, ct);
        table.Status = TableStatus.Empty;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(ApiResult.Ok(new { order.Id, order.Code }, "Đã thanh toán."));
    }
    catch (DbUpdateConcurrencyException)
    {
        // Đơn vừa được thanh toán ở máy khác, hoặc dòng tồn vừa bị request khác sửa.
        await transaction.RollbackAsync(ct);
        return Conflict(ApiResult.Fail("Dữ liệu vừa thay đổi. Tải lại đơn rồi thử lại."));
    }
}
```

Ghi chú:

- Nếu có lỗi trước `CommitAsync`, `await using` sẽ tự rollback: cả đơn, tồn kho và trạng thái bàn đều trở về như cũ.
- `NotFoundException` chỉ xảy ra khi đơn không thuộc tenant hiện tại. Bước 1 đã chặn trường hợp này, nên thường không gặp.
- Khi bị 409, `ajax.js` tự hiện toast màu cam. Trang POS chỉ cần tải lại đơn.

## 4. Cần thống nhất với Hiếu

1. Chữ ký `DeductForOrderAsync(long orderId, CancellationToken ct = default)` ở trên.
2. Service gọi `SaveChangesAsync` bên trong. Mọi thay đổi đơn bạn làm trước khi gọi cũng được lưu ở lúc đó.
   Nếu bạn muốn service chỉ đánh dấu thay đổi (không SaveChanges), báo Hiếu để đổi; khi đó xung đột sẽ lộ ra ở lần
   `SaveChangesAsync` của bạn.
3. Mọi thời điểm ghi theo `VietnamTime.Now` (CLAUDE.md mục 9).
4. Test trừ kho của Module 2 nằm ở `BTL_WEB_NC.Tests/Manage/InventoryDeductionServiceTests.cs`. Khi gộp repo, thêm
   một test tích hợp A16: thanh toán thành công thì tồn giảm; thanh toán lần hai thì 409 và tồn không đổi.
