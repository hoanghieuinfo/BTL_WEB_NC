<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
# Endpoint bổ sung của Area Manage (chưa có trong Bảng 3.21)

Các mã đánh nối tiếp từ A17 để Hiếu đưa vào báo cáo. Bước 7 (rà soát) sẽ bổ sung các endpoint phụ còn lại
của MH09–MH12 vào cùng bảng này.

Mọi endpoint dưới đây:

- yêu cầu đăng nhập, vai trò `TenantAdmin`;
- đọc qua DbSet đã lọc tenant, nên ID của quán khác trả 404;
- trả lỗi theo định dạng `{ success, message, data }` (mục 3.2.6).

| Mã | Phương thức | Đường dẫn | Mục đích | Kết quả | Yêu cầu |
|---|---|---|---|---|---|
| A17 | GET | `/manage/reports/orders?from&to&branchId&page` | Danh sách hóa đơn Đã thanh toán trong kỳ của MH13, mới nhất trước, 10 hóa đơn/trang | Partial View `_OrderList`. Lỗi khoảng ngày trả 400 với `data.errors`; chi nhánh quán khác trả 404 | R20 |
| A18 | GET | `/manage/reports/orders/{id}` | Chi tiết một hóa đơn theo BM06 trong hộp thoại của MH13 | Partial View `_OrderDetail`. Hóa đơn của quán khác trả 404 | R20 |
| A19 | GET | `/manage?branchId=` | MH08 Tổng quan theo một chi nhánh: số liệu hôm nay so với hôm qua, nguyên liệu cần nhập | View đầy đủ (tải lại trang). `branchId` lạ hoặc của quán khác được bỏ qua và trang về "Tất cả chi nhánh" | R19, R20 |

Ghi chú về A19: kế hoạch ban đầu dự kiến endpoint riêng `/manage/summary` trả Partial View. Khi làm, đổi chi nhánh
được chuyển thành form GET tải lại chính trang `/manage`, nên không có `/manage/summary`.
