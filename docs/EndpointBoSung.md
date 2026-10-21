<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
# Endpoint bổ sung của Area Manage (chưa có trong Bảng 3.21)

Bảng dưới liệt kê mọi endpoint AJAX của Area Manage chưa có trong Bảng 3.21. Mã đánh tiếp từ A17.
Như kiểu dòng A07 của Bảng 3.21, các endpoint cùng màn hình và cùng mục đích được gộp vào một dòng;
các đường dẫn bắt đầu bằng `/{id}/…` nối tiếp vào đường dẫn đầu tiên của dòng.

Điều kiện chung cho mọi endpoint dưới đây:

- **Quyền:** chỉ vai trò `TenantAdmin`. Request AJAX chưa đăng nhập trả 401, sai vai trò trả 403.
- **Cách ly dữ liệu:** dữ liệu đọc qua DbSet đã lọc tenant. ID trên URL hoặc khóa ngoại trong body thuộc quán khác trả **404**.
- **Bảo vệ POST:** POST phải gửi anti-forgery token trong header `RequestVerificationToken`.
- **Định dạng JSON:** `{ success, message, data }`. Lỗi validate trả 400 với `data.errors`; xung đột RowVersion trả 409.

| Mã | Phương thức, đường dẫn | Mục đích | Kết quả | Yêu cầu |
|---|---|---|---|---|
| A17 | GET /manage/branches/list, /{id}/zones | MH09: bảng chi nhánh (kèm mức dùng gói); khu vực và bàn của chi nhánh đang chọn | Partial View | R11, R12 |
| A18 | POST /manage/branches/create, /{id}/update, /{id}/zones/create; /manage/zones/{id}/rename, /{id}/move, /{id}/tables/create; /manage/tables/{id}/update, /{id}/visibility | MH09: thêm, sửa chi nhánh (thêm thì kiểm tra MaxBranches); thêm, đổi tên, sắp xếp khu vực; thêm, sửa, ẩn/hiện bàn | JSON | R11, R12 |
| A19 | GET /manage/branches/{id}/qr, /{id}/qr.png | MH09: thông tin và ảnh mã QR thực đơn công khai của chi nhánh (xem, tải về) | JSON, ảnh PNG | R16 |
| A20 | POST /manage/categories/create, /{id}/update, /{id}/delete | MH10: thêm, sửa, xóa mềm danh mục món | JSON | R13 |
| A21 | POST /manage/products/{id}/availability | MH10: bật/tắt Đang bán ngay trên dòng món | JSON | R14 |
| A22 | GET /manage/products/{id}/recipe | MH10: dữ liệu hộp thoại định lượng (nguyên liệu của quán, định lượng hiện có) | JSON | R15 |
| A23 | GET /manage/staff/list | MH11: tìm, lọc chi nhánh/trạng thái, phân trang nhân viên | Partial View | R17 |
| A24 | POST /manage/staff/create, /{id}/update, /{id}/reset-password, /{id}/transfer, /{id}/lock, /{id}/unlock | MH11: thêm nhân viên vai trò Staff (kiểm tra MaxStaff, trả mật khẩu khởi tạo một lần, no-store); sửa; đặt lại mật khẩu; chuyển chi nhánh; khóa, mở khóa (mở khóa kiểm tra MaxStaff). Đặt lại, chuyển, khóa, mở khóa đều đổi SecurityStamp | JSON | R03, R17 |
| A25 | GET /manage/inventory/stock, /stock/{branchId}/{ingredientId}, /ingredients/list, /receipts/list, /receipts/{id} | MH12: tồn kho theo chi nhánh và chi tiết tồn; danh sách nguyên liệu; danh sách phiếu nhập (kèm tổng chi trong kỳ) và chi tiết phiếu theo BM07 | Partial View | R18, R19 |
| A26 | POST /manage/inventory/ingredients/create, /{id}/update, /{id}/delete | MH12: thêm, sửa, xóa mềm nguyên liệu | JSON | R18 |
| A27 | POST /manage/inventory/receipts/create | MH12: lưu phiếu nhập trong một transaction (sinh số phiếu, cộng tồn theo hệ số quy đổi, tính lại tổng tiền) | JSON | R19 |
| A28 | GET /manage/reports/orders, /orders/{id} | MH13: hóa đơn Đã thanh toán trong kỳ (phân trang); chi tiết hóa đơn theo BM06 | Partial View | R20 |

## Trang chưa có trong Bảng 3.20 (để bổ sung Bảng 3.20)

Bảng 3.20 chỉ ghi `/manage/inventory` cho MH12. Ba trang con dưới đây cùng thuộc MH12, trả View đầy đủ:

| Phương thức | Đường dẫn | Mục đích |
|---|---|---|
| GET | `/manage/inventory/ingredients` | Tab Nguyên liệu |
| GET | `/manage/inventory/receipts` | Tab Phiếu nhập |
| GET | `/manage/inventory/receipts/new?branchId&ingredientId` | Trang Lập phiếu nhập (nút "Nhập hàng" điền sẵn chi nhánh và nguyên liệu) |

Ghi chú về MH08: đổi chi nhánh ở trang Tổng quan dùng form GET tải lại chính trang `/manage?branchId=`, nên không có
endpoint AJAX riêng (không có `/manage/summary`). Có thể ghi tham số `?branchId=` vào dòng MH08 của Bảng 3.20.
