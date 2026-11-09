<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->

Bước 1. Nối sơ đồ bàn với A12 (R22, R23)

Bàn Trống: bấm mở hộp thoại nhập số khách (1–99, có thể bỏ trống), gọi A12, rồi chuyển sang /pos/orders/{id}.
Bàn Đang phục vụ hoặc Chờ thanh toán: bấm là vào thẳng /pos/orders/{orderId}. TableDto đã có OrderId.
Xử lý 409 (“Bàn đã có khách”): báo lỗi và tải lại sơ đồ.
Chuyển thẻ bàn từ div sang button để bấm và dùng bàn phím được.
Trạng thái: Đã làm.

Bước 2. Khung trang đơn hàng MH15

GET /pos/orders/{id} trả View. Lọc theo BranchId từ claim. Sai chi nhánh hoặc sai tenant thì trả 404.
Tạo OrderDetailDto gồm đầu phiếu theo BM05 (chi nhánh, bàn, khu vực, nhân viên, giờ vào, số khách), danh sách món, tổng tiền và RowVersion dạng base64 (cần cho bước 8).
Bố cục hai cột trên tablet: món bên trái, đơn bên phải. Trên điện thoại chuyển thành tab.
Trạng thái: Đã làm.

Bước 3. A11 tìm và lọc món (R24)

GET /pos/products/search?q=&categoryId=: chỉ lấy món IsAvailable và chưa xóa. categoryId phải tra qua DbSet đã lọc.
Phía giao diện: ô tìm kiếm có debounce, thanh lọc danh mục, trạng thái đang tải, rỗng và lỗi.
Trạng thái: Đã làm.

Bước 4. A13 thêm món, đổi số lượng, ghi chú (R23)

ProductId tra lại qua DbSet đã lọc. Món sai tenant hoặc đã xóa trả 404; món đã ngừng bán trả 400 (BusinessRuleException).
Lưu ProductName và UnitPrice tại thời điểm bán vào OrderItem.
Tính lại SubTotal và Total phía server, không tin số từ trình duyệt. Thẻ bàn ở MH14 đang hiển thị SubTotal.
Nếu bàn đang Chờ thanh toán mà gọi thêm thì đưa bàn về Đang phục vụ (P05 bước 5).
Dùng RowVersion của đơn để tránh hai máy sửa cùng lúc (409).
Chỉ cho sửa khi đơn còn Serving.
Trạng thái: Đã làm.

Bước 5. A14 hủy món (R25)

Bắt buộc có lý do. Đặt IsCancelled = true và không xóa dòng (R42). Tính lại tổng tiền.
Món đã hủy vẫn hiển thị gạch ngang nhưng không tính tiền và không bị trừ kho.
Trạng thái: Đã làm.

Bước 6. A15 chuyển bàn và gộp bàn (R25)

Chuyển: bàn đích phải Trống, cùng chi nhánh và đang hoạt động. Đổi Order.TableId, bàn cũ về Trống, bàn mới nhận trạng thái của đơn.
Gộp: bàn đích phải có đơn Serving. Chuyển các OrderItem sang đơn đích, tính lại tổng, đánh dấu đơn nguồn đã hủy (lý do “Gộp vào đơn …”), bàn nguồn về Trống. Làm trong một transaction.
Trạng thái: Đã làm.

Bước 7. Tạm tính và hủy đơn (P05 bước 5 và ngoại lệ)

Phần này SPEC chưa có endpoint (đã bổ sung A29, A30).
Tạm tính: đổi bàn sang Chờ thanh toán, mở trang in phiếu tạm tính (MH16).
Hủy đơn: nếu đơn chưa có món thì hủy luôn, nếu đã có món thì bắt buộc ghi lý do. Đơn chuyển Cancelled, bàn về Trống.
Trạng thái: Đã làm (A29 POST /pos/orders/{id}/provisional, A30 POST /pos/orders/{id}/cancel; đã ghi ở docs/EndpointBoSung.md).

Bước 8. A16 thanh toán và trừ kho (R26, R30, UC16)

Đây là bước khó nhất. Làm theo Viec12_HuongDanTrung.md.
Validate: giảm giá không âm, không lớn hơn tổng tiền, lớn hơn 0 thì bắt buộc có lý do. Tiền mặt thì tiền khách đưa phải đủ.
Trong một transaction: gắn RowVersion gốc từ client, tính lại tổng từ CSDL, đặt Paid, PaidAt = VietnamTime.Now, CashierId, gọi DeductForOrderAsync, đưa bàn về Trống, commit.
Bắt DbUpdateConcurrencyException và trả 409 (đơn đã thanh toán ở máy khác).
Giao diện: hộp thoại thanh toán, JS tính sẵn tiền thừa, thành công thì mở trang in.
Đơn không có món chưa hủy thì không cho thanh toán.
Trạng thái: Đã làm.

Bước 9. MH16 in phiếu tạm tính và hóa đơn BM06 (R27)

GET /pos/orders/{id}/print. Đầu hóa đơn lấy tên quán, chi nhánh, địa chỉ, SĐT (Branch.Address và Phone đã có).
Có CSS @media print khổ 80mm, tự gọi window.print().
Có thể tham khảo _OrderDetail.cshtml của Manage (MH13) để tránh viết lại.
Trạng thái: Đã làm.

Bước 10. MH17 hóa đơn trong ngày (R28)

GET /pos/invoices: danh sách đơn Đã thanh toán trong ngày của chi nhánh, tính theo giờ Việt Nam. Bấm vào để xem lại hoặc in lại.
Thêm link “Hóa đơn” vào _PosLayout.
Trạng thái: Đã làm (R28 theo ngày; chưa lọc theo ca vì SPEC chưa có khái niệm ca).

Bước 11. Hoàn thiện

Test theo CLAUDE.md mục 8:
đơn hoặc bàn của tenant khác trả 404;
đơn hoặc bàn của chi nhánh khác trong cùng quán trả 404;
khóa ngoại sai trả 404 và không tạo bản ghi nào;
thanh toán hai lần trả 409 và chỉ trừ kho một lần.
Kiểm tra giao diện ở 360px, 768px, 1280px (R31, vùng chạm ≥ 44px).
Đăng ký DI cho IOrderService trong Program.cs (đã có), chạy dotnet build và dotnet test.
Cập nhật EndpointBoSung.md với các endpoint mới.
Trạng thái: Đang làm: dotnet build đã qua; còn dotnet test và kiểm tra giao diện 360px, 768px, 1280px.
