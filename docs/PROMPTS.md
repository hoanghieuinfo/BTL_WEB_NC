<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->

# Bộ prompt cho Claude Code – Module 2 (Hoàng Minh Hiếu)

Đặt file này tại `docs/PROMPTS.md`. Mỗi bước mở **một phiên mới**, bật Plan mode (Shift+Tab), dán prompt,
duyệt kế hoạch rồi mới cho Claude Code sửa file. `CLAUDE.md` ở gốc repo được đọc tự động, không cần dán lại.

## Chuẩn bị (tự làm, trước Bước 1a)

1. Đặt `CLAUDE.md` ở thư mục gốc `BTL_WNC` (cùng cấp `.gitignore`).
2. Tạo `docs/SPEC.md`: copy Chương 2 và Chương 3 từ file Word (giữ nguyên các bảng).
3. Export PNG các frame Module 2 từ Figma vào `docs/design/` (chọn frame → Export → PNG 1x), đặt tên:

| Frame Figma                                                                         | Tên file                                                                                |
| ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| MH08 – Tổng quan quán                                                               | `MH08-tong-quan.png`                                                                    |
| MH08 – Chưa có hóa đơn                                                              | `MH08-rong.png`                                                                         |
| MH08 – Tổng quan quán (Mobile 360px)                                                | `MH08-mobile-360.png`                                                                   |
| MH09 – Quản lý chi nhánh & Bàn                                                      | `MH09-list.png`                                                                         |
| MH09 – Hộp thoại Thêm bàn                                                           | `MH09-them-ban.png`                                                                     |
| MH09 – Mã QR                                                                        | `MH09-qr.png`                                                                           |
| MH09 – Thêm chi nhánh – lỗi                                                         | `MH09-them-chi-nhanh-loi.png`                                                           |
| MH09 – Đủ giới hạn gói                                                              | `MH09-du-gioi-han.png`                                                                  |
| MH09 – Chi nhánh & Bàn (Mobile 360px)                                               | `MH09-mobile-360.png`                                                                   |
| MH10 – Quản lý thực đơn & Định lượng                                                | `MH10-list.png`                                                                         |
| MH10 – Đang tải                                                                     | `MH10-dang-tai.png`                                                                     |
| MH10 – Hộp thoại Thêm danh mục                                                      | `MH10-them-danh-muc.png`                                                                |
| MH10 – Thêm món / lỗi / đang lưu                                                    | `MH10-them-mon.png`, `MH10-them-mon-loi.png`, `MH10-them-mon-dang-luu.png`              |
| MH10 – Định lượng                                                                   | `MH10-dinh-luong.png`                                                                   |
| MH10 – Xác nhận xóa                                                                 | `MH10-xac-nhan-xoa.png`                                                                 |
| MH10 – Thực đơn (Tablet 768px) / (Mobile 360px)                                     | `MH10-tablet-768.png`, `MH10-mobile-360.png`                                            |
| MH11 – Quản lý nhân viên                                                            | `MH11-list.png`                                                                         |
| MH11 – Thêm nhân viên – lỗi / Mật khẩu khởi tạo / Chuyển chi nhánh / Khóa tài khoản | `MH11-them-loi.png`, `MH11-mat-khau.png`, `MH11-chuyen-chi-nhanh.png`, `MH11-khoa.png`  |
| MH11 – Quản lý nhân viên (Mobile 360px)                                             | `MH11-mobile-360.png`                                                                   |
| MH12 – Nguyên liệu / Tồn kho / Lập phiếu nhập kho / Phiếu nhập                      | `MH12-nguyen-lieu.png`, `MH12-ton-kho.png`, `MH12-lap-phieu.png`, `MH12-phieu-nhap.png` |
| MH12 – Tồn kho (Mobile 360px)                                                       | `MH12-mobile-360.png`                                                                   |
| MH13 – Báo cáo / Đang tải và rỗng / Chi tiết hóa đơn                                | `MH13-bao-cao.png`, `MH13-dang-tai-rong.png`, `MH13-chi-tiet-hd.png`                    |
| MH13 – Báo cáo (Mobile 360px)                                                       | `MH13-mobile-360.png`                                                                   |

4. Kiểm tra .NET 8 SDK, `dotnet-ef`, SQL Server LocalDB. Commit lại trạng thái hiện tại.
5. Nhắn Trung để thống nhất chữ ký `IInventoryDeductionService` (CLAUDE.md mục 9).

---

## Bước 1a – Entity, DbContext, migration

```
Đọc CLAUDE.md và docs/SPEC.md (Bảng 3.8–3.18, mục 3.2.2). Project BTL_WEB_NC (template MVC) đã có sẵn: kiểm tra TargetFramework là net8.0, tạo BTL_WNC.sln ở thư mục gốc, thêm project test BTL_WEB_NC.Tests (xUnit) và add cả hai vào solution. Giữ HomeController của template.
1. Toàn bộ entity theo Bảng 3.8–3.17 và AppUser kế thừa IdentityUser, enum cho các cột Status/PaymentMethod.
2. ITenantEntity, ISoftDelete, TenantContext [TẠM].
3. AppDbContext kế thừa IdentityDbContext<AppUser>: Global Query Filter, tự gán/chặn đổi TenantId trong SaveChanges/SaveChangesAsync, cấu hình đầy đủ khóa, UNIQUE có điều kiện, CHECK, RowVersion, ON DELETE theo Bảng 3.18, chỉ mục bắt đầu bằng TenantId.
4. Migration đầu tiên.
5. Test: Global Query Filter lọc đúng tenant; thêm bản ghi tự gán TenantId; đổi TenantId bị từ chối; không có tenant thì không lưu được.
```

## Bước 1b – Khung tạm: đăng nhập, layout, seed, ajax.js

```
Tiếp tục khung tạm theo CLAUDE.md mục 4 và 6.
1. Program.cs: Identity 3 vai trò (SuperAdmin, TenantAdmin, Staff), chính sách mật khẩu và lockout theo Bảng 3.22, RequireUniqueEmail, cookie HttpOnly/Secure/SameSite=Lax, SecurityStamp ValidationInterval 5 phút, OnRedirectToLogin/OnRedirectToAccessDenied trả 401/403 cho request AJAX, Antiforgery đọc header RequestVerificationToken, AutoValidateAntiforgeryToken toàn cục, HTTPS/HSTS.
2. TenantResolutionMiddleware [TẠM]: đọc claim tenant_id, branch_id; tenant bị khóa thì đăng xuất.
3. AccountController [TẠM]: đăng nhập/đăng xuất tối giản, ghi claim tenant_id khi đăng nhập.
4. SeedData: 2 tenant (gói Chuỗi và gói Cơ bản), mỗi tenant 1 TenantAdmin, 2 chi nhánh có khu vực và bàn, 5 danh mục, ~15 món (1 món chưa có định lượng), ~10 nguyên liệu có định lượng, tồn kho ban đầu (1 nguyên liệu dưới mức, 1 âm), và ~30 ngày hóa đơn đã thanh toán rải trên 2 chi nhánh, có món hủy, có giảm giá, có 1 đơn đang phục vụ. In tài khoản thử ra console khi seed.
5. _ManageLayout.cshtml (sidebar, topbar, mức dùng gói) theo docs/design/MH10 – Quản lý thực đơn & Định lượng.png; site.css với biến màu; ajax.js (getJson, postJson, postForm cho upload, xử lý 400/401/403/404/409/429, hiển thị lỗi theo data.errors).
6. Trang /manage tạm hiển thị "Xin chào" để thử đăng nhập.
```

## Bước 2 – MH10 Thực đơn (UC10, chức năng trình bày ở Chương 4)

```
Làm MH10 /manage/products theo UC10 (Bảng 3.4), R13, R14, R15, R21, BM04 và ảnh docs/design/MH10*.png.
- Danh mục (cột trái): thêm/sửa/xóa mềm, đếm số món, chọn danh mục để lọc. Không xóa danh mục còn món chưa xóa.
- A06 GET /manage/products/list: lọc danh mục, trạng thái, tìm theo tên, lọc "món chưa có định lượng", phân trang 6/trang, trả Partial View; có trạng thái đang tải và rỗng.
- A07 POST /manage/products/create, /{id}/update, /{id}/delete: hộp thoại modal, validate theo BM04a ở cả client và server, CategoryId phải thuộc tenant (6c → 404), tên trùng trong tenant báo lỗi tại ô (6a), ảnh qua ImageUploadService (6b). Xóa: hỏi xác nhận; món đã có trong OrderItems thì chỉ xóa mềm.
- Bật/tắt Đang bán bằng AJAX ngay trên dòng.
- A08 POST /manage/products/{id}/recipe: hộp thoại định lượng, chọn nguyên liệu của tenant, số lượng theo đơn vị cơ sở > 0, không trùng nguyên liệu.
- Khối "Cảnh báo trừ kho" và dòng món chưa định lượng tô nổi như thiết kế.
- Responsive theo ảnh MH10 bản Tablet 768px và Mobile 360px.
- Test: cô lập tenant cho A06–A08, upload file giả ảnh bị từ chối, xóa món đã bán là xóa mềm.
```

## Bước 3 – MH09 Chi nhánh, khu vực, bàn, mã QR

```
Làm MH09 /manage/branches theo P03, R11, R12, R16, BM03 và ảnh docs/design/MH09*.png.
- CRUD chi nhánh: tên và slug duy nhất trong tenant, slug chỉ chữ thường không dấu/số/gạch ngang (gợi ý slug tự sinh từ tên), CloseTime > OpenTime, tạm ngưng thay vì xóa. Chặn vượt MaxBranches, hiển thị "x/y chi nhánh của gói" và trạng thái đủ giới hạn.
- Khu vực (Zones) và bàn (DiningTables) của chi nhánh đang chọn, thao tác bằng AJAX; ẩn bàn (IsActive = 0) thay vì xóa; không ẩn bàn đang có đơn.
- Mã QR bằng QRCoder trỏ tới https://{host}/{slug-quan}/thuc-don/{slug-chi-nhanh}, xem trong hộp thoại và tải PNG.
- Test: vượt giới hạn gói bị chặn; ZoneId/BranchId của tenant khác trả 404.
```

## Bước 4 – MH11 Nhân viên

```
Làm MH11 /manage/staff theo P04, R03, R17, BM08 và ảnh docs/design/MH11*.png.
- AppUser không có Global Query Filter: service tự lọc theo TenantId của chủ quán; BranchId phải thuộc tenant.
- Thêm nhân viên: email duy nhất toàn hệ thống, vai trò luôn là Staff, sinh mật khẩu khởi tạo ngẫu nhiên đạt chính sách, hiện một lần trong hộp thoại, MustChangePassword = true. Chặn vượt MaxStaff.
- Chuyển chi nhánh, khóa/mở tài khoản: cập nhật SecurityStamp; không xóa cứng.
- Tìm kiếm, lọc chi nhánh và trạng thái, phân trang bằng AJAX; nhãn "Chưa đổi mật khẩu" và "x/y nhân viên của gói".
- Việc bắt buộc đổi mật khẩu khi đăng nhập thuộc Module 1: chỉ ghi chú lại, không làm.
- Test: không thấy/sửa được nhân viên tenant khác; không tạo được vai trò khác Staff; vượt MaxStaff bị chặn.
```

## Bước 5 – MH12 Kho và Việc 12 (trừ kho)

```
Làm MH12 /manage/inventory theo P06, P07, R18, R19, R30, BM07 và ảnh docs/design/MH12*.png.
- Nguyên liệu: tên duy nhất trong tenant, đơn vị cơ sở, MinStock ≥ 0, xóa mềm; không xóa nguyên liệu đang dùng trong định lượng.
- Phiếu nhập: chọn chi nhánh, nhà cung cấp, ngày; nhiều dòng gồm nguyên liệu, đơn vị nhập, hệ số quy đổi, số lượng, đơn giá; JS tính thành tiền. Server: một transaction, sinh Code duy nhất trong tenant, cộng tồn = Quantity × ConversionRate, tạo dòng BranchStocks nếu chưa có, TotalAmount tính lại ở server.
- Tồn kho theo chi nhánh: tình trạng Âm kho / Dưới mức / Đủ tính khi hiển thị; danh sách phiếu nhập và xem chi tiết.
- Việc 12: IInventoryDeductionService.DeductForOrderAsync(long orderId, CancellationToken ct) theo CLAUDE.md mục 9: không tự mở transaction; bỏ món hủy và món chưa định lượng; tạo dòng tồn nếu chưa có; cho phép âm; để DbUpdateConcurrencyException nổi lên.
- Viết docs/Viec12_HuongDanTrung.md: đoạn code mẫu Trung gọi service trong A16 (mở transaction, cập nhật đơn, trừ kho, đổi trạng thái bàn, commit; bắt lỗi xung đột trả 409).
- Test: các trường hợp trừ kho ở CLAUDE.md mục 8; nhập kho cộng đúng sau quy đổi.
```

## Bước 6 – MH13 Báo cáo và MH08 Tổng quan

```
Làm MH13 /manage/reports theo P08, R20, BM09 và ảnh docs/design/MH13*.png.
- A09 GET /manage/reports/revenue?from=&to=&branchId=: chỉ tính đơn Đã thanh toán, lọc theo PaidAt; tổng doanh thu, số hóa đơn, giá trị TB, tổng giảm giá và số hóa đơn có giảm giá; so với kỳ trước cùng độ dài (% tăng/giảm); theo ngày (đủ mọi ngày, ngày không bán = 0); theo chi nhánh; top 10 món theo OrderItems.ProductName (bỏ món hủy). Giới hạn khoảng tối đa 366 ngày, from ≤ to.
- Chart.js: đường theo ngày, cột theo chi nhánh; trạng thái đang tải và rỗng.
- Danh sách hóa đơn trong kỳ, phân trang AJAX, xem chi tiết trong modal theo BM06.
Sau đó làm MH08 /manage theo các ảnh MH08*.png: 3 thẻ số liệu hôm nay so với hôm qua, danh sách nguyên liệu âm kho/dưới mức, thông tin gói và ngày hết hạn, mức dùng chi nhánh/nhân viên, thao tác nhanh; trạng thái chưa có hóa đơn.
- Test: số liệu báo cáo khớp với dữ liệu seed tính tay cho 1 ngày; không lẫn số liệu tenant khác.
```
## Bước 7 – Rà soát trước Milestone 2

```
Rà soát toàn bộ Area Manage theo CLAUDE.md mục 6 và 8. CHƯA sửa; lập bảng (file, dòng, vấn đề, mức độ, cách sửa) cho:
- Action POST thiếu anti-forgery; chỗ nhận TenantId/BranchId từ request; khóa ngoại chưa tra lại qua DbSet đã lọc; chỗ dùng IgnoreQueryFilters.
- Html.Raw hoặc innerHTML với dữ liệu người dùng.
- JavaScript inline (<script> có nội dung trong .cshtml, onclick=, onchange=…) sẽ bị chặn khi Module 1 bật header CSP.
- Responsive ở 360/768/1280px so với ảnh thiết kế; vùng chạm < 44px.
- Truy vấn có thể chậm (thiếu AsNoTracking, N+1, thiếu phân trang).
- Xóa cứng dữ liệu đã phát sinh giao dịch.
- Giới hạn gói (chi nhánh và nhân viên) có thể bị vượt khi hai request đồng thời: đề xuất cách khóa (ví dụ UPDLOCK trên dòng Tenants trong transaction) dùng chung cho cả hai chỗ.
- File do Claude tạo/sửa thiếu comment [AI] ở đầu file theo CLAUDE.md mục 7.
Sau khi tôi duyệt bảng thì mới sửa.

Ngoài bảng trên, lập file docs/EndpointBoSung.md liệt kê mọi endpoint của Area Manage chưa có trong Bảng 3.21 (phương thức, đường dẫn, mục đích, kết quả JSON/Partial View, yêu cầu Rxx), đánh mã nối tiếp từ A17, để tôi bổ sung vào báo cáo.
```
## Bước 8 – Chương 4 và nhật ký AI

```
Dựa trên mã nguồn MH10 trong repo và docs/SPEC.md, viết docs/Chuong4_Hieu.md cho chức năng "Quản lý thực đơn" của Hoàng Minh Hiếu, đúng khung giảng viên:
1. Giao diện: chỗ đặt ảnh (Hình 4.x) ở 1280/768/360px, kèm mô tả.
2. Mã HTML, CSS, Client Script: trích đoạn tiêu biểu (modal thêm món, media query, ajax.js, chèn dữ liệu bằng textContent) kèm giải thích.
3. Mã Server: controller, service thêm món, đoạn Global Query Filter và kiểm tra CategoryId.
4. Đánh giá: Responsive; SEO (trang quản trị dùng noindex theo R33 và lý do; liên hệ trang thực đơn công khai do món Đang bán hiển thị ở đó); AJAX (A06–A08, mã trạng thái); ATTT (CSRF, XSS, upload, cô lập tenant) kèm bảng kết quả kiểm thử thật lấy từ test đã viết.
Văn phong giống Chương 1–3, đánh số bảng/hình theo Chương 4. Chỉ trích đoạn code ngắn, không dán cả file.
Sau đó soạn docs/PhuLucZ_Z7.md: mục Z.7 theo đúng mẫu bảng Z.1–Z.3 (để trống "Phần nhóm giữ lại/chỉnh sửa/loại bỏ" và "Minh chứng" cho tôi điền) và dòng Z.7 cho Bảng Z.1.
```
