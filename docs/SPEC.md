# SPEC – Đặc tả hệ thống (Chương 2–3 báo cáo BTL, nhóm WNC.G12)

> Trích từ file báo cáo Word. Đây là đặc tả chính thức cho code. Khi báo cáo Word được sửa, cập nhật lại file này.
> Các hình (use case, tuần tự, ERD, mockup) không đưa vào đây; giao diện xem `docs/design/`.

---

# CHƯƠNG 2: KHẢO SÁT VÀ XÁC ĐỊNH YÊU CẦU

## 2.1. Các quy trình nghiệp vụ (Pxx)

**Bảng 2.1. Danh sách quy trình nghiệp vụ**

| Mã | Tên quy trình | Tác nhân chính | Module |
|---|---|---|---|
| P01 | Đăng ký sử dụng dịch vụ và kích hoạt tenant | Khách truy cập, Super Admin | Module 1 |
| P02 | Quản lý gói dịch vụ và khóa/mở tenant | Super Admin | Module 1 |
| P03 | Thiết lập quán (chi nhánh, khu vực/bàn, thực đơn, định lượng) | Chủ quán | Module 2 |
| P04 | Quản lý nhân viên và phân công chi nhánh | Chủ quán | Module 2 |
| P05 | Bán hàng tại bàn (gọi món – thanh toán – in hóa đơn) | Nhân viên/Thu ngân | Module 3 |
| P06 | Nhập kho nguyên liệu | Chủ quán | Module 2 |
| P07 | Trừ kho tự động khi thanh toán | Hệ thống | Tích hợp |
| P08 | Thống kê, báo cáo doanh thu | Chủ quán, Super Admin | Module 2, 1 |
| P09 | Khách xem thực đơn công khai qua mã QR | Khách của quán | Module 3 |

### P01. Đăng ký sử dụng dịch vụ và kích hoạt tenant
Tác nhân: Khách truy cập (chủ quán tiềm năng), Super Admin.
1. Khách vào landing page, xem giới thiệu và bảng gói dịch vụ, chọn "Đăng ký dùng thử".
2. Khách điền phiếu đăng ký (BM01): tên quán, đường dẫn riêng của quán, họ tên, email, số điện thoại, mật khẩu, gói dịch vụ.
3. Trong lúc nhập, hệ thống kiểm tra ngay email và đường dẫn quán đã tồn tại chưa, đường dẫn có trùng từ khóa dành riêng không.
4. Hệ thống tạo tenant ở trạng thái "Chờ kích hoạt", tạo tài khoản Chủ quán gắn với tenant đó và một chi nhánh mặc định.
5. Super Admin kích hoạt hoặc từ chối (ghi lý do).
6. Sau khi được kích hoạt, chủ quán đăng nhập và thiết lập quán (P03).

Kết quả: một tenant hoạt động, có một tài khoản Chủ quán và một chi nhánh mặc định. Biểu mẫu: BM01, BM02.

### P02. Quản lý gói dịch vụ và khóa/mở tenant
Tác nhân: Super Admin.
1. Tạo hoặc sửa gói dịch vụ (BM02): tên gói, số chi nhánh tối đa, số nhân viên tối đa, giá tham khảo, thời hạn.
2. Đổi gói cho tenant (mô phỏng). Không cho chuyển xuống gói có giới hạn thấp hơn số chi nhánh/nhân viên tenant đang có.
3. Đánh dấu tenant hết hạn gói. Khi khóa tenant (ghi lý do): mọi tài khoản thuộc tenant bị chặn đăng nhập, phiên đang mở bị đăng xuất, trang thực đơn công khai hiển thị thông báo tạm ngưng.
4. Mở khóa thì tenant hoạt động lại; dữ liệu giữ nguyên.

### P03. Thiết lập quán (Module 2)
Tác nhân: Chủ quán.
1. Khai báo chi nhánh (BM03): tên, đường dẫn chi nhánh, địa chỉ, số điện thoại, giờ mở cửa. Kiểm tra số chi nhánh không vượt giới hạn gói.
2. Với mỗi chi nhánh, tạo các khu vực (tầng 1, sân vườn…) và danh sách bàn trong từng khu vực.
3. Tạo danh mục món, sau đó khai báo từng món (BM04): tên, giá, ảnh, mô tả, trạng thái đang bán/ngừng bán.
4. Với mỗi món, khai báo công thức định lượng theo đơn vị cơ sở của nguyên liệu (ví dụ: 1 ly cà phê sữa = 25 g cà phê bột + 30 ml sữa đặc).
5. Hệ thống sinh mã QR dẫn tới trang thực đơn công khai của từng chi nhánh để in và đặt tại bàn.

Kết quả: quán sẵn sàng bán hàng. Biểu mẫu: BM03, BM04.

### P04. Quản lý nhân viên và phân công chi nhánh (Module 2)
Tác nhân: Chủ quán.
1. Nhập thông tin nhân viên (BM08): họ tên, số điện thoại, email đăng nhập, chi nhánh làm việc.
2. Kiểm tra số nhân viên không vượt giới hạn gói, tạo tài khoản vai trò Nhân viên, cấp mật khẩu khởi tạo; nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên.
3. Khi nhân viên chuyển chi nhánh hoặc nghỉ việc: cập nhật chi nhánh hoặc khóa tài khoản (không xóa, để giữ lịch sử hóa đơn). Hệ thống thu hồi phiên đăng nhập hiện tại của nhân viên đó.

Kết quả: nhân viên chỉ thao tác được trên chi nhánh được phân công. Biểu mẫu: BM08.

### P05. Bán hàng tại bàn (Module 3)
Tác nhân: Nhân viên/Thu ngân.
1. Mở màn hình bán hàng, thấy sơ đồ bàn của chi nhánh mình; trạng thái bàn: Trống, Đang phục vụ, Chờ thanh toán.
2. Chọn bàn trống, tạo đơn mới (nhập số khách nếu cần); bàn chuyển Đang phục vụ.
3. Chọn món (tìm kiếm, lọc theo danh mục), nhập số lượng và ghi chú (BM05). Gọi thêm nhiều lần trên cùng một đơn.
4. Chuyển bàn, gộp bàn hoặc hủy món (ghi lý do).
5. In phiếu tạm tính, bàn chuyển Chờ thanh toán. Gọi thêm thì bàn quay lại Đang phục vụ.
6. Xem tổng tiền, giảm giá (bắt buộc ghi lý do), chọn hình thức thanh toán (tiền mặt/chuyển khoản), nhập tiền khách đưa để tính tiền thừa.
7. Ghi nhận hóa đơn (BM06), đơn chuyển Đã thanh toán, kích hoạt P07, bàn về Trống.
8. In hóa đơn.

Ngoại lệ: khách rời đi khi chưa gọi món → hủy đơn, bàn về Trống. Hủy đơn đã có món phải ghi lý do. Gộp bàn thì bàn nguồn về Trống.

### P06. Nhập kho nguyên liệu (Module 2)
Tác nhân: Chủ quán.
1. Khai báo danh mục nguyên liệu: tên, đơn vị tính cơ sở (g, ml, cái…), mức tồn tối thiểu. Mọi định lượng và tồn kho tính theo đơn vị cơ sở.
2. Lập phiếu nhập kho (BM07) cho chi nhánh: nguyên liệu, đơn vị nhập (kg, hộp…), hệ số quy đổi sang đơn vị cơ sở, số lượng, đơn giá, nhà cung cấp, ngày nhập.
3. Hệ thống cộng vào tồn kho của chi nhánh: số lượng nhập × hệ số quy đổi.

### P07. Trừ kho tự động khi thanh toán (Tích hợp)
Tác nhân: Hệ thống (kích hoạt từ bước 7 của P05).
1. Với mỗi món chưa hủy trong hóa đơn, tra công thức định lượng.
2. Lượng tiêu hao = định lượng × số lượng món; trừ vào tồn kho của chi nhánh bán hàng. Nếu chi nhánh chưa có dòng tồn kho của nguyên liệu đó thì tạo mới với tồn bằng 0 rồi trừ.
3. Không chặn bán hàng khi tồn kho âm. Trang quản trị của chủ quán cảnh báo nguyên liệu dưới mức tối thiểu hoặc bị âm.

### P08. Thống kê, báo cáo doanh thu
Tác nhân: Chủ quán (trong tenant), Super Admin (toàn hệ thống).
1. Chủ quán chọn khoảng thời gian và chi nhánh (hoặc tất cả).
2. Tổng hợp từ hóa đơn đã thanh toán: doanh thu theo ngày, theo chi nhánh, theo món; số hóa đơn; so sánh với kỳ trước; món bán chạy (BM09); hiển thị bảng và biểu đồ. Xem danh sách hóa đơn chi tiết trong khoảng thời gian đã chọn.
3. Super Admin xem thống kê toàn hệ thống: số tenant theo trạng thái, theo gói; số tenant đăng ký mới theo tháng.

### P09. Khách xem thực đơn công khai qua mã QR (Module 3)
1. Quét QR → `/{duong-dan-quan}/thuc-don/{duong-dan-chi-nhanh}`, ví dụ `/ca-phe-may/thuc-don/cau-giay`.
2. Trang hiển thị tên quán, địa chỉ, giờ mở cửa, món đang bán theo danh mục, ảnh và giá.
3. Khách gọi món trực tiếp với nhân viên (không tự đặt món).

## 2.2. Các biểu mẫu (BMxx)

| Mã | Tên biểu mẫu | Quy trình |
|---|---|---|
| BM01 | Phiếu đăng ký sử dụng dịch vụ | P01 |
| BM02 | Bảng gói dịch vụ | P01, P02 |
| BM03 | Phiếu thông tin chi nhánh | P03 |
| BM04 | Phiếu khai báo món và công thức định lượng | P03 |
| BM05 | Phiếu gọi món | P05 |
| BM06 | Hóa đơn thanh toán | P05 |
| BM07 | Phiếu nhập kho | P06 |
| BM08 | Phiếu thông tin nhân viên | P04 |
| BM09 | Báo cáo doanh thu | P08 |

### BM01. Phiếu đăng ký sử dụng dịch vụ
| Trường | Bắt buộc | Ràng buộc |
|---|---|---|
| Tên quán | Có | 3–100 ký tự |
| Đường dẫn riêng của quán | Có | Chữ thường không dấu, chữ số, dấu gạch ngang; duy nhất trong hệ thống |
| Họ tên chủ quán | Có | 2–100 ký tự |
| Email | Có | Đúng định dạng; duy nhất trong hệ thống |
| Số điện thoại | Có | 10 chữ số |
| Mật khẩu / Nhập lại | Có | Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số; hai lần nhập trùng nhau |
| Gói dịch vụ | Có | Chọn từ BM02 |
| Đồng ý điều khoản | Có | Phải tích chọn |

### BM02. Bảng gói dịch vụ
| Gói | Chi nhánh tối đa | Nhân viên tối đa | Giá tham khảo/tháng | Ghi chú |
|---|---|---|---|---|
| Dùng thử | 1 | 3 | 0 đ | Thời hạn 30 ngày |
| Cơ bản | 1 | 10 | 199.000 đ | |
| Chuỗi | 5 | 50 | 499.000 đ | |

Giá chỉ mô phỏng, không thu phí thực tế.

### BM03. Phiếu thông tin chi nhánh
| Trường | Bắt buộc | Ràng buộc |
|---|---|---|
| Tên chi nhánh | Có | Không trùng trong cùng tenant |
| Địa chỉ | Có | |
| Số điện thoại | Không | 10 chữ số |
| Giờ mở cửa – đóng cửa | Có | Giờ đóng cửa phải sau giờ mở cửa |
| Trạng thái | Có | Đang hoạt động / Tạm ngưng |

### BM04. Phiếu khai báo món và công thức định lượng
a) Thông tin món

| Trường | Bắt buộc | Ràng buộc |
|---|---|---|
| Danh mục | Có | Chọn từ danh mục của tenant |
| Tên món | Có | Không trùng trong cùng tenant |
| Giá bán | Có | Số nguyên lớn hơn 0 |
| Ảnh món | Không | JPG/PNG/WEBP, tối đa 2 MB |
| Mô tả | Không | Tối đa 500 ký tự |
| Trạng thái | Có | Đang bán / Ngừng bán |

b) Công thức định lượng (ví dụ Cà phê sữa đá)

| STT | Nguyên liệu | Định lượng | Đơn vị |
|---|---|---|---|
| 1 | Cà phê bột | 25 | g |
| 2 | Sữa đặc | 30 | ml |
| 3 | Đá viên | 150 | g |

### BM05. Phiếu gọi món
Đầu phiếu: chi nhánh, bàn, khu vực, nhân viên, giờ vào, số khách. Chi tiết: STT, tên món, số lượng, ghi chú (ví dụ "Ít đá").

### BM06. Hóa đơn thanh toán
| Phần | Nội dung |
|---|---|
| Đầu hóa đơn | Tên quán, tên chi nhánh, địa chỉ, số điện thoại, số hóa đơn, ngày giờ, bàn, thu ngân |
| Chi tiết | STT, tên món, số lượng, đơn giá, thành tiền |
| Tổng kết | Tổng tiền hàng, giảm giá, tổng thanh toán, hình thức thanh toán, tiền khách đưa, tiền thừa |
| Chân hóa đơn | Lời cảm ơn, mã QR thực đơn (tùy chọn) |

### BM07. Phiếu nhập kho
Đầu phiếu: số phiếu, ngày nhập, chi nhánh, nhà cung cấp, người nhập.

| STT | Nguyên liệu | Đơn vị | Số lượng | Đơn giá (đ) | Thành tiền (đ) |
|---|---|---|---|---|---|
| 1 | Cà phê bột | kg | 5 | 180.000 | 900.000 |
| 2 | Sữa đặc | hộp | 24 | 25.000 | 600.000 |
| | | | | Tổng cộng | 1.500.000 |

### BM08. Phiếu thông tin nhân viên
| Trường | Bắt buộc | Ràng buộc |
|---|---|---|
| Họ tên | Có | |
| Email đăng nhập | Có | Duy nhất trong hệ thống |
| Số điện thoại | Có | 10 chữ số |
| Chi nhánh làm việc | Có | Chọn từ các chi nhánh của tenant |
| Ngày vào làm | Có | |
| Trạng thái | Có | Đang làm / Đã nghỉ (khóa tài khoản) |

### BM09. Báo cáo doanh thu
| Phần | Nội dung |
|---|---|
| Tham số | Từ ngày – đến ngày; chi nhánh (một hoặc tất cả) |
| Tổng quan | Tổng doanh thu, số hóa đơn, giá trị trung bình mỗi hóa đơn, so sánh với kỳ trước |
| Theo ngày | Biểu đồ đường doanh thu từng ngày |
| Theo chi nhánh | Bảng và biểu đồ cột doanh thu từng chi nhánh |
| Theo món | Top 10 món bán chạy (số lượng, doanh thu) |

## 2.3. Các yêu cầu (Rxx)

### 2.3.1. Yêu cầu chức năng (Bảng 2.3)

| Mã | Yêu cầu | Tác nhân | Quy trình | Module |
|---|---|---|---|---|
| R01 | Đăng ký dịch vụ; kiểm tra trùng email và đường dẫn quán khi nhập (AJAX) | Khách truy cập | P01 | 1 |
| R02 | Đăng nhập, đăng xuất; tự chuyển tới giao diện theo vai trò | Mọi người dùng | — | 1 |
| R03 | Đổi mật khẩu; bắt buộc đổi ở lần đăng nhập đầu tiên | Mọi người dùng | P04 | 1 |
| R04 | Phân quyền ba cấp; chặn truy cập chức năng ngoài quyền | Hệ thống | — | 1 |
| R05 | Danh sách tenant; tìm kiếm, lọc theo trạng thái/gói | Super Admin | P01, P02 | 1 |
| R06 | Kích hoạt, khóa, mở khóa tenant (AJAX) | Super Admin | P01, P02 | 1 |
| R07 | Thêm, sửa, ngừng cung cấp gói dịch vụ | Super Admin | P02 | 1 |
| R08 | Đổi gói dịch vụ cho tenant (mô phỏng) | Super Admin | P02 | 1 |
| R09 | Thống kê toàn hệ thống | Super Admin | P08 | 1 |
| R10 | Landing page giới thiệu dịch vụ và bảng gói | Khách truy cập | P01 | 1 |
| R11 | Quản lý chi nhánh; chặn tạo vượt giới hạn gói | Chủ quán | P03 | 2 |
| R12 | Quản lý khu vực và bàn của từng chi nhánh | Chủ quán | P03 | 2 |
| R13 | Quản lý danh mục món | Chủ quán | P03 | 2 |
| R14 | Thêm, sửa, xóa, ẩn món; tải ảnh món; thao tác bằng AJAX | Chủ quán | P03 | 2 |
| R15 | Khai báo công thức định lượng cho từng món | Chủ quán | P03 | 2 |
| R16 | Sinh mã QR thực đơn công khai cho từng chi nhánh | Chủ quán | P03 | 2 |
| R17 | Quản lý nhân viên, phân công chi nhánh; chặn tạo vượt giới hạn gói | Chủ quán | P04 | 2 |
| R18 | Quản lý danh mục nguyên liệu và mức tồn tối thiểu | Chủ quán | P06 | 2 |
| R19 | Lập phiếu nhập kho; xem tồn kho theo chi nhánh; cảnh báo nguyên liệu sắp hết | Chủ quán | P06, P07 | 2 |
| R20 | Báo cáo doanh thu theo ngày, chi nhánh, món; biểu đồ tải bằng AJAX | Chủ quán | P08 | 2 |
| R21 | Tìm kiếm, lọc, phân trang các danh sách quản trị (AJAX) | Chủ quán | — | 2 |
| R22 | Sơ đồ bàn theo khu vực; trạng thái bàn cập nhật bằng AJAX | Nhân viên | P05 | 3 |
| R23 | Tạo đơn, thêm/bớt món không tải lại trang (AJAX) | Nhân viên | P05 | 3 |
| R24 | Tìm kiếm món, lọc theo danh mục khi bán | Nhân viên | P05 | 3 |
| R25 | Chuyển bàn, gộp bàn, hủy món/hủy đơn (ghi lý do) | Nhân viên | P05 | 3 |
| R26 | Thanh toán: giảm giá, chọn hình thức, tính tiền thừa | Nhân viên | P05 | 3 |
| R27 | In hóa đơn theo mẫu BM06 | Nhân viên | P05 | 3 |
| R28 | Xem hóa đơn trong ca/ngày tại chi nhánh | Nhân viên | P05 | 3 |
| R29 | Trang thực đơn công khai của chi nhánh qua mã QR | Khách của quán | P09 | 3 |
| R30 | Tự động trừ kho theo định lượng khi đơn được thanh toán | Hệ thống | P07 | Tích hợp |

### 2.3.2. Yêu cầu phi chức năng (Bảng 2.4)

| Mã | Nhóm | Yêu cầu | Cách kiểm chứng |
|---|---|---|---|
| R31 | Responsive | Điện thoại (từ 360px), máy tính bảng (từ 768px), máy tính (từ 1280px); màn hình bán hàng tối ưu cho máy tính bảng | DevTools, thiết bị thật |
| R32 | SEO | Landing page và thực đơn công khai: title, meta description, heading đúng thứ bậc, URL thân thiện, alt ảnh, sitemap.xml, robots.txt; SEO ≥ 90 | Lighthouse |
| R33 | SEO | Trang quản trị và bán hàng không cho lập chỉ mục (noindex) | Thẻ meta, robots.txt |
| R34 | Dễ dùng | Gọi một món không quá 2 lần chạm; giao diện tiếng Việt theo style guide | Kiểm thử với người dùng |
| R35 | Hiệu năng | AJAX phản hồi dưới 1 giây; danh sách có phân trang | Tab Network |
| R36 | ATTT – Xác thực | Mật khẩu băm có salt; khóa tạm sau 5 lần đăng nhập sai | Kiểm tra CSDL; thử sai |
| R37 | ATTT – Lỗ hổng web | Chống SQL Injection (ORM tham số hóa), XSS (mã hóa đầu ra), CSRF (token cho form và AJAX) | Payload phổ biến |
| R38 | ATTT – Cô lập tenant | Mọi truy vấn nghiệp vụ tự lọc theo tenant; không xem/sửa dữ liệu tenant khác kể cả khi sửa ID trên URL hoặc request | Đăng nhập tenant A, thử ID của tenant B |
| R39 | ATTT – Truyền tải, đầu vào | Bắt buộc HTTPS; validate cả client và server; file tải lên kiểm tra định dạng, dung lượng, đổi tên khi lưu | Thủ công |
| R40 | Tương thích | Chrome, Edge, Firefox, Safari bản mới | Từng trình duyệt |
| R41 | Mở rộng | Thêm tenant không cần sửa code hay tạo CSDL mới | Đăng ký nhiều tenant thử |
| R42 | Toàn vẹn dữ liệu | Không xóa cứng hóa đơn, nhân viên, món đã phát sinh giao dịch | Kiểm tra CSDL sau khi xóa |

---

# CHƯƠNG 3: PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG

## 3.1. Phân tích hệ thống

### 3.1.1. Tác nhân (Bảng 3.1)

| Tác nhân | Phạm vi dữ liệu | Đăng nhập |
|---|---|---|
| Super Admin | Toàn hệ thống (tenant, gói, thống kê tổng hợp) | Có |
| Chủ quán (TenantAdmin) | Một tenant, tất cả chi nhánh của tenant | Có |
| Nhân viên / Thu ngân (Staff) | Một chi nhánh được phân công | Có |
| Khách truy cập | Trang công khai của nền tảng | Không |
| Khách của quán | Thực đơn công khai của một chi nhánh | Không |

### 3.1.2. Danh sách use case (Bảng 3.2)

| Mã | Tên use case | Tác nhân | Yêu cầu | Module |
|---|---|---|---|---|
| UC01 | Đăng ký dịch vụ | Khách truy cập | R01 | 1 |
| UC02 | Đăng nhập | SA, Chủ quán, NV | R02, R04 | 1 |
| UC03 | Đổi mật khẩu | SA, Chủ quán, NV | R03 | 1 |
| UC04 | Quản lý tenant | Super Admin | R05, R06, R08 | 1 |
| UC05 | Quản lý gói dịch vụ | Super Admin | R07 | 1 |
| UC06 | Thống kê hệ thống | Super Admin | R09 | 1 |
| UC07 | Xem landing page | Khách truy cập | R10 | 1 |
| UC08 | Quản lý chi nhánh (kèm sinh mã QR) | Chủ quán | R11, R16 | 2 |
| UC09 | Quản lý khu vực, bàn | Chủ quán | R12 | 2 |
| UC10 | Quản lý thực đơn (danh mục, món, định lượng) | Chủ quán | R13, R14, R15, R21 | 2 |
| UC11 | Quản lý nhân viên | Chủ quán | R17 | 2 |
| UC12 | Quản lý kho (nguyên liệu, nhập kho, tồn kho, cảnh báo) | Chủ quán | R18, R19 | 2 |
| UC13 | Báo cáo doanh thu (kèm danh sách hóa đơn) | Chủ quán | R20 | 2 |
| UC14 | Xem sơ đồ bàn | Nhân viên | R22 | 3 |
| UC15 | Lên đơn | Nhân viên | R23, R24, R25 | 3 |
| UC16 | Thanh toán (kèm trừ kho, in hóa đơn) | Nhân viên | R26, R27, R30 | 3, Tích hợp |
| UC17 | Xem hóa đơn trong ngày | Nhân viên | R28 | 3 |
| UC18 | Xem thực đơn công khai | Khách của quán | R29 | 3 |

### 3.1.4. Đặc tả use case

#### Bảng 3.4. UC10 – Quản lý thực đơn (luồng thêm món)

- **Tác nhân:** Chủ quán.
- **Mô tả:** Thêm, sửa, xóa món và khai báo định lượng; thao tác bằng AJAX, không tải lại trang.
- **Tiền điều kiện:** Đăng nhập vai trò Chủ quán; tenant đang hoạt động; đã có ít nhất một danh mục.
- **Luồng chính:**
  1. Mở màn hình Danh mục & món.
  2. Hệ thống tải danh sách món của tenant bằng AJAX, lọc theo danh mục, tìm kiếm, phân trang.
  3. Bấm "Thêm món".
  4. Hiển thị form trong hộp thoại (modal).
  5. Nhập thông tin theo BM04a, chọn ảnh, bấm "Lưu".
  6. Hệ thống nhận request AJAX, kiểm tra dữ liệu, ảnh, kiểm tra danh mục thuộc tenant hiện tại; lưu món, TenantId gán tự động.
  7. Trả kết quả; trình duyệt chèn món mới vào danh sách và đóng hộp thoại.
- **Luồng thay thế:**
  - 6a. Tên món đã tồn tại trong tenant: báo lỗi tại ô tên món.
  - 6b. Ảnh sai định dạng hoặc lớn hơn 2 MB: báo lỗi, không lưu.
  - 6c. Danh mục không thuộc tenant hiện tại (request bị sửa): trả 404.
  - Sửa món: như thêm món, form điền sẵn dữ liệu hiện tại.
  - Xóa món: hỏi xác nhận; nếu món đã xuất hiện trong hóa đơn thì chỉ xóa mềm (IsDeleted = 1).
  - Khai báo định lượng (extend): chọn nguyên liệu của tenant, nhập lượng dùng cho một phần món theo đơn vị cơ sở (BM04b).
- **Hậu điều kiện:** Món mới hiển thị trên màn hình bán hàng và trang thực đơn công khai (nếu Đang bán).
- **Yêu cầu:** R13, R14, R15, R21, R37, R38, R39, R42.

#### Bảng 3.6. UC16 – Thanh toán (Module 3, liên quan Việc 12)

- **Tiền điều kiện:** Đơn Đang phục vụ, có ít nhất một món chưa hủy, thuộc chi nhánh của nhân viên.
- **Luồng chính:**
  1. Bấm "Thanh toán".
  2. Hộp thoại: tổng tiền hàng, giảm giá và lý do, hình thức thanh toán, tiền khách đưa.
  3. JavaScript tính sẵn tiền thừa.
  4. Xác nhận; gửi request AJAX.
  5. Trong **một giao dịch**: tính lại tổng tiền từ CSDL, cập nhật đơn sang Đã thanh toán, **trừ kho theo định lượng** (include; tạo dòng tồn kho nếu chưa có), đưa bàn về Trống.
  6. Trả kết quả; đổi màu bàn, mở trang in hóa đơn BM06. Nguyên liệu dưới mức tối thiểu xuất hiện trong cảnh báo trang quản trị chủ quán.
- **Luồng thay thế:**
  - 3a. Tiền mặt mà tiền khách đưa nhỏ hơn số phải trả: không cho xác nhận.
  - 3b. Giảm giá âm, lớn hơn tổng tiền hàng, hoặc > 0 mà không có lý do: báo lỗi.
  - 5a. Đơn đã được thanh toán từ thiết bị khác (trạng thái đổi hoặc xung đột RowVersion): hủy giao dịch, trả 409, tải lại đơn.
  - 5b. Món chưa khai báo định lượng: bỏ qua trừ kho cho món đó, vẫn hoàn tất thanh toán.
- **Yêu cầu:** R26, R27, R30, R35, R42.

Ghi chú biểu đồ tuần tự: tổng tiền luôn tính lại phía server từ giá đã lưu trong chi tiết đơn; cập nhật đơn, trừ kho và đổi trạng thái bàn nằm trong cùng một giao dịch, một bước lỗi thì toàn bộ hoàn tác.

## 3.2. Thiết kế hệ thống

### 3.2.1. Kiến trúc tổng thể

Một ứng dụng ASP.NET Core MVC duy nhất phục vụ mọi tenant. Mỗi nhóm người dùng có một Area riêng. Bảng khu vực trong chi nhánh tên là **Zones** (tránh nhầm với Area của MVC).

- **Trình duyệt:** HTML do Razor sinh; JavaScript gọi AJAX bằng fetch; biểu đồ bằng Chart.js.
- **Pipeline:** HTTPS → Routing → Authentication → TenantResolution → Authorization → Rate Limiting; trước action, filter toàn cục AutoValidateAntiforgeryToken kiểm tra token cho request thay đổi dữ liệu.
- **Controller theo Area:** Public (landing, tài khoản, thực đơn công khai), System (Super Admin), **Manage (Chủ quán)**, Pos (Nhân viên). Controller chỉ nhận request, kiểm tra dữ liệu, gọi service.
- **Tầng Service:** nghiệp vụ (giới hạn gói, kiểm tra ID tham chiếu, tính tiền, trừ kho…), dùng chung cho request thường và AJAX.
- **Tầng dữ liệu:** AppDbContext (EF Core) kế thừa IdentityDbContext; tự lọc theo tenant.

**Bảng 3.7. Công nghệ:** ASP.NET Core MVC (.NET 8), Razor View; EF Core + SQL Server; ASP.NET Core Identity (cookie); Rate Limiting middleware; HTML5, CSS3 tự viết theo style guide Figma, JavaScript; Fetch API, JSON, Partial View; Chart.js; QRCoder.

### 3.2.2. Thiết kế cơ chế đa tenant

Chiến lược **shared database – shared schema**: mọi tenant dùng chung CSDL và bảng, mỗi dòng nghiệp vụ mang TenantId. Rủi ro lớn nhất là quên điều kiện `WHERE TenantId = …`, nên việc lọc diễn ra tự động ở tầng dữ liệu.

**a) Đánh dấu dữ liệu thuộc tenant**
- Mọi entity nghiệp vụ (chi nhánh, khu vực, bàn, món, nguyên liệu, đơn hàng, chi tiết đơn, tồn kho…) cài `ITenantEntity` có `TenantId`. Bảng con như OrderItems, Recipes cũng lưu TenantId để bộ lọc đồng nhất và chỉ mục luôn bắt đầu bằng TenantId.
- `AppUser` (AspNetUsers) có cột TenantId nhưng **không** cài `ITenantEntity` (lúc đăng nhập chưa có tenant). Service quản lý nhân viên của Area Manage tự lọc theo TenantId của chủ quán.

**b) Xác định tenant của request**
- `TenantContext` (Scoped) chứa TenantId, BranchId, vai trò.
- Đã đăng nhập: đọc claim `tenant_id`, `branch_id` ghi vào cookie lúc đăng nhập. Super Admin không có tenant_id.
- Trang thực đơn công khai: tra Tenants theo slug quán (cache ngắn hạn).
- Middleware kiểm tra trạng thái tenant; tenant bị khóa thì đăng xuất, trang công khai báo tạm ngưng.
- Khi khóa hoặc chuyển chi nhánh nhân viên: cập nhật SecurityStamp; cookie kiểm tra lại SecurityStamp theo chu kỳ ngắn (ValidationInterval vài phút), phiên cũ mang branch_id cũ bị thu hồi.

**c) Lọc và gán TenantId tự động (AppDbContext)**
- **Lọc khi đọc:** khi tạo model, duyệt mọi entity cài ITenantEntity và đăng ký Global Query Filter `TenantId == TenantId hiện tại`. Biểu thức lọc tham chiếu **thuộc tính của chính DbContext** (lấy từ TenantContext), không dùng biến cục bộ, vì EF Core cache model và chỉ đánh giá lại thành viên của DbContext ở mỗi truy vấn. Entity có xóa mềm gộp TenantId và IsDeleted trong **cùng một biểu thức** (EF Core 8/9 chỉ cho một bộ lọc mỗi entity).
- **Gán khi thêm:** trước khi lưu, gán TenantId cho bản ghi mới bằng tenant hiện tại; không xác định được tenant và bản ghi chưa có TenantId thì từ chối lưu.
- **Chặn đổi tenant khi sửa:** bản ghi cũ bị đổi TenantId thì từ chối lưu.
- Tenant A sửa ID trên URL thành ID của tenant B → truy vấn vẫn có điều kiện TenantId = A → không thấy → **404** (R38). TenantId null thì bộ lọc trả rỗng.
- Được phép bỏ qua bộ lọc hoặc gán TenantId tường minh chỉ ở: service Area System (IgnoreQueryFilters để thống kê); service đăng ký UC01; sinh sitemap.xml.

**d) Phạm vi chi nhánh, giới hạn gói, đường dẫn dành riêng**
- Nhân viên: service Area Pos luôn lọc thêm `BranchId == TenantContext.BranchId`; BranchId lấy từ claim, không nhận từ request.
- Giới hạn gói: trước khi thêm chi nhánh hoặc nhân viên, đếm số bản ghi hiện có so với MaxBranches, MaxStaff (R11, R17).
- Slug quán không được trùng: system, manage, pos, api, dang-ky, dang-nhap, tai-khoan, css, js, lib, images, uploads, sitemap.xml, robots.txt.

**e) Kiểm tra ID tham chiếu**
Global Query Filter chỉ bảo vệ khi **đọc**. Khi **ghi**, khóa ngoại trỏ tới bản ghi của tenant khác (ví dụ CategoryId của quán B khi quán A tạo món) vẫn INSERT được. Vì vậy mọi ID tham chiếu nhận từ request (danh mục, nguyên liệu, chi nhánh, khu vực, bàn, món…) được service tra lại qua DbSet đã lọc trước khi gán; không thấy thì 404. Với Area Pos, bàn và đơn còn phải thuộc chi nhánh của nhân viên.

### 3.2.3. Thiết kế cơ sở dữ liệu

16 bảng nghiệp vụ + các bảng Identity.

**Bảng 3.8. Danh sách bảng**

| STT | Bảng | Ý nghĩa | Biểu mẫu | Có TenantId |
|---|---|---|---|---|
| 1 | ServicePlans | Gói dịch vụ | BM02 | Không |
| 2 | Tenants | Quán/chuỗi | BM01 | Không (gốc) |
| 3 | AspNetUsers (AppUser) | Tài khoản | BM01, BM08 | Có (null với SA); không áp bộ lọc tự động |
| 4 | AspNetRoles, AspNetUserRoles | Vai trò (Identity) | — | Không |
| 5 | Branches | Chi nhánh | BM03 | Có |
| 6 | Zones | Khu vực trong chi nhánh | — | Có |
| 7 | DiningTables | Bàn | — | Có |
| 8 | Categories | Danh mục món | BM04 | Có |
| 9 | Products | Món | BM04 | Có |
| 10 | Ingredients | Nguyên liệu | BM04, BM07 | Có |
| 11 | Recipes | Định lượng cho một phần món | BM04 | Có |
| 12 | BranchStocks | Tồn kho theo chi nhánh | — | Có |
| 13 | StockReceipts | Phiếu nhập kho | BM07 | Có |
| 14 | StockReceiptItems | Chi tiết phiếu nhập | BM07 | Có |
| 15 | Orders | Đơn hàng / hóa đơn | BM05, BM06 | Có |
| 16 | OrderItems | Chi tiết đơn | BM05, BM06 | Có |

Quy ước: PK khóa chính, FK khóa ngoại, NN NOT NULL, UQ duy nhất. Mọi bảng có TenantId đều có cột `TenantId int NN FK → Tenants(Id)` (không lặp lại trong từng bảng dưới đây).

**Bảng 3.9. ServicePlans**

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| Id | int | PK, identity | |
| Name | nvarchar(50) | NN, UQ | Tên gói |
| MaxBranches | int | NN, > 0 | |
| MaxStaff | int | NN, > 0 | |
| PricePerMonth | decimal(18,0) | NN, ≥ 0 | Giá mô phỏng |
| TrialDays | int | NULL | Ngày dùng thử |
| IsActive | bit | NN, mặc định 1 | |

**Bảng 3.10. Tenants**

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| Id | int | PK, identity | |
| Name | nvarchar(100) | NN | Tên quán |
| Slug | varchar(60) | NN, UQ | Đường dẫn riêng |
| Phone | varchar(15) | NULL | |
| ServicePlanId | int | NN, FK → ServicePlans | |
| Status | tinyint | NN | 0 Chờ kích hoạt; 1 Hoạt động; 2 Bị khóa; 3 Từ chối |
| PlanExpiresAt | datetime2 | NULL | |
| LockReason | nvarchar(255) | NULL | |
| CreatedAt | datetime2 | NN | |

**Bảng 3.11. AspNetUsers – cột bổ sung của AppUser**

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| FullName | nvarchar(100) | NN | |
| TenantId | int | NULL, FK → Tenants | Null với Super Admin |
| BranchId | int | NULL, FK → Branches | Bắt buộc (ở service) với vai trò Nhân viên |
| StartDate | date | NULL | Ngày vào làm |
| IsActive | bit | NN | Đang làm / Đã nghỉ |
| MustChangePassword | bit | NN | |
| Email, NormalizedEmail, PasswordHash, SecurityStamp, LockoutEnd, AccessFailedCount… | Identity | UQ trên NormalizedEmail | Cấu hình lại chỉ mục NormalizedEmail thành UNIQUE, bật RequireUniqueEmail |

**Bảng 3.12. Branches**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| Id | int | PK, identity |
| Name | nvarchar(100) | NN; UQ(TenantId, Name) |
| Slug | varchar(60) | NN; UQ(TenantId, Slug) |
| Address | nvarchar(255) | NN |
| Phone | varchar(15) | NULL |
| OpenTime, CloseTime | time | NN; CloseTime > OpenTime |
| IsActive | bit | NN (Đang hoạt động / Tạm ngưng) |

**Bảng 3.13. Zones và DiningTables**

Zones: Id int PK; BranchId int NN FK → Branches; Name nvarchar(50) NN, UQ(BranchId, Name); SortOrder int NN.

DiningTables: Id int PK; ZoneId int NN FK → Zones; Name nvarchar(20) NN, UQ(ZoneId, Name); Seats int NULL; Status tinyint NN (0 Trống, 1 Đang phục vụ, 2 Chờ thanh toán); IsActive bit NN.

**Bảng 3.14. Categories và Products**

Categories: Id int PK; Name nvarchar(50) NN, UQ(TenantId, Name) với bản ghi chưa xóa; SortOrder int NN; IsDeleted bit NN.

Products:

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| Id | int | PK | |
| CategoryId | int | NN, FK → Categories | |
| Name | nvarchar(100) | NN; UQ(TenantId, Name) với món chưa xóa | |
| Price | decimal(18,0) | NN, > 0 | Giá hiện tại |
| ImagePath | varchar(255) | NULL | Ảnh đã đổi tên |
| Description | nvarchar(500) | NULL | |
| IsAvailable | bit | NN | Đang bán / Ngừng bán |
| IsDeleted | bit | NN | Xóa mềm |

**Bảng 3.15. Ingredients, Recipes, BranchStocks**

Ingredients: Id int PK; Name nvarchar(100) NN, UQ(TenantId, Name) với bản ghi chưa xóa; Unit nvarchar(20) NN (đơn vị cơ sở); MinStock decimal(18,3) NN ≥ 0; IsDeleted bit NN.

Recipes: ProductId int PK, FK → Products; IngredientId int PK, FK → Ingredients; Quantity decimal(18,3) NN > 0 (lượng cho 1 phần món, đơn vị cơ sở).

BranchStocks: BranchId int PK, FK → Branches; IngredientId int PK, FK → Ingredients; Quantity decimal(18,3) NN (có thể âm); UpdatedAt datetime2 NN; RowVersion rowversion.

Cảnh báo tồn kho không lưu bảng riêng: tính khi hiển thị, là các dòng BranchStocks có Quantity < MinStock.

**Bảng 3.16. StockReceipts và StockReceiptItems**

StockReceipts: Id int PK; BranchId int NN FK → Branches; Code varchar(20) NN, UQ(TenantId, Code); Supplier nvarchar(100) NULL; ReceivedDate date NN; CreatedById nvarchar(450) NN FK → AspNetUsers; TotalAmount decimal(18,0) NN.

StockReceiptItems: Id int PK; StockReceiptId int NN FK → StockReceipts (cascade); IngredientId int NN FK → Ingredients; PurchaseUnit nvarchar(20) NN; ConversionRate decimal(18,3) NN > 0; Quantity decimal(18,3) NN > 0 (theo đơn vị nhập; tồn cộng Quantity × ConversionRate); UnitPrice decimal(18,0) NN ≥ 0.

**Bảng 3.17. Orders và OrderItems**

Orders:

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| Id | bigint | PK | |
| BranchId | int | NN, FK → Branches | |
| TableId | int | NN, FK → DiningTables | Giữ nguyên sau thanh toán |
| Code | varchar(20) | NN; UQ(BranchId, Code) | Số hóa đơn |
| Status | tinyint | NN | 0 Đang phục vụ; 1 Đã thanh toán; 2 Đã hủy |
| GuestCount | int | NULL | |
| CreatedById | nvarchar(450) | NN, FK → AspNetUsers | |
| CreatedAt | datetime2 | NN | Giờ vào |
| SubTotal, Discount, Total | decimal(18,0) | NN, ≥ 0 | |
| DiscountReason | nvarchar(255) | NULL; bắt buộc khi Discount > 0 | |
| PaymentMethod | tinyint | NULL | 0 Tiền mặt; 1 Chuyển khoản |
| CashReceived | decimal(18,0) | NULL | |
| PaidAt | datetime2 | NULL | |
| CashierId | nvarchar(450) | NULL, FK → AspNetUsers | |
| CancelReason | nvarchar(255) | NULL | |
| RowVersion | rowversion | | Chống thanh toán trùng |

OrderItems: Id bigint PK; OrderId bigint NN FK → Orders; ProductId int NN FK → Products; ProductName nvarchar(100) NN (tên lúc bán); UnitPrice decimal(18,0) NN (giá lúc bán); Quantity int NN > 0; Note nvarchar(100) NULL; IsCancelled bit NN; CancelReason nvarchar(255) NULL.

OrderItems lưu tên và giá lúc bán nên đổi giá/tên món không làm thay đổi hóa đơn cũ và báo cáo.

**Bảng 3.18. Chỉ mục và ràng buộc**

| Đối tượng | Thiết kế | Mục đích |
|---|---|---|
| Chỉ mục theo tenant | Mọi bảng có TenantId có chỉ mục bắt đầu bằng TenantId | R35, R41 |
| Orders | (TenantId, BranchId, Status) | Đơn đang phục vụ |
| Orders | (TenantId, PaidAt) INCLUDE (BranchId, Total) | Báo cáo doanh thu |
| Categories, Products, Ingredients | UNIQUE (TenantId, Name) WHERE IsDeleted = 0 | Tên bản ghi đã xóa mềm được dùng lại |
| AspNetUsers | UNIQUE (NormalizedEmail) | Email duy nhất toàn hệ thống |
| Khóa ngoại | ON DELETE NO ACTION cho dữ liệu đã phát sinh giao dịch; CASCADE chỉ cho chi tiết phiếu nhập | R42 |
| CHECK | Price > 0, Quantity > 0, Discount ≥ 0, ConversionRate > 0, CloseTime > OpenTime… | |
| Đồng thời | RowVersion trên Orders, BranchStocks | Chống thanh toán trùng, ghi đè tồn kho |

### 3.2.4. Thiết kế phân quyền

Ba vai trò Identity: `SuperAdmin`, `TenantAdmin` (Chủ quán), `Staff` (Nhân viên). Kiểm tra ở ba lớp:
- Lớp chức năng: mỗi Area gắn `[Authorize(Roles = "...")]` ở controller gốc (R04).
- Lớp dữ liệu tenant: Global Query Filter và kiểm tra ID tham chiếu.
- Lớp dữ liệu chi nhánh: service Area Pos lọc theo BranchId của nhân viên.

Chủ quán không trực tiếp bán hàng; muốn đứng quầy thì tạo thêm tài khoản Nhân viên.

**Bảng 3.19. Ma trận phân quyền**

| Chức năng | Super Admin | Chủ quán | Nhân viên | Khách |
|---|---|---|---|---|
| Đăng ký, landing page | — | — | — | Có |
| Đăng nhập, đổi mật khẩu | Có | Có | Có | — |
| Quản lý tenant, gói, thống kê hệ thống | Có | — | — | — |
| Chi nhánh, khu vực, bàn | — | Tenant mình | — | — |
| Thực đơn, định lượng | — | Tenant mình | Chỉ xem khi bán | — |
| Nhân viên | — | Tenant mình | — | — |
| Kho | — | Tenant mình | — | — |
| Báo cáo doanh thu | — | Mọi chi nhánh của tenant | — | — |
| Sơ đồ bàn, lên đơn, thanh toán, in hóa đơn | — | — | Chi nhánh mình | — |
| Danh sách, chi tiết hóa đơn | — | Mọi chi nhánh, mọi thời điểm (trong báo cáo) | Chi nhánh mình, trong ngày | — |
| Thực đơn công khai | — | — | — | Có |

### 3.2.5. Thiết kế giao diện

**Bảng 3.20. Danh sách màn hình**

| Mã | Màn hình | URL | Vai trò | Phụ trách |
|---|---|---|---|---|
| MH01 | Landing page, bảng gói | / | Khách | Đạt |
| MH02 | Đăng ký | /dang-ky | Khách | Đạt |
| MH03 | Đăng nhập | /dang-nhap | Mọi người dùng | Đạt |
| MH04 | Thực đơn công khai | /{slug-quan}/thuc-don/{slug-chi-nhanh} | Khách của quán | Trung |
| MH05 | Tổng quan hệ thống | /system | Super Admin | Đạt |
| MH06 | Quản lý tenant | /system/tenants | Super Admin | Đạt |
| MH07 | Quản lý gói dịch vụ | /system/plans | Super Admin | Đạt |
| **MH08** | Tổng quan quán (doanh thu hôm nay, cảnh báo tồn kho) | /manage | Chủ quán | **Hiếu** |
| **MH09** | Chi nhánh, khu vực, bàn, mã QR | /manage/branches | Chủ quán | **Hiếu** |
| **MH10** | Danh mục và món, định lượng | /manage/products | Chủ quán | **Hiếu** |
| **MH11** | Nhân viên | /manage/staff | Chủ quán | **Hiếu** |
| **MH12** | Nguyên liệu, tồn kho, phiếu nhập | /manage/inventory | Chủ quán | **Hiếu** |
| **MH13** | Báo cáo doanh thu, danh sách hóa đơn | /manage/reports | Chủ quán | **Hiếu** |
| MH14 | Sơ đồ bàn | /pos | Nhân viên | Trung |
| MH15 | Đơn hàng tại bàn, hộp thoại thanh toán | /pos/orders/{id} | Nhân viên | Trung |
| MH16 | In phiếu tạm tính, in hóa đơn | /pos/orders/{id}/print | Nhân viên | Trung |
| MH17 | Hóa đơn trong ngày | /pos/invoices | Nhân viên | Trung |
| MH18 | Đổi mật khẩu | /tai-khoan/doi-mat-khau | Người dùng đã đăng nhập | Đạt |

**Mô tả màn hình Module 2 (theo thiết kế Figma, ảnh trong `docs/design/`):**
- **MH08 Tổng quan:** ba thẻ số liệu từ hóa đơn đã thanh toán trong ngày, so với hôm qua (R20); khối "Nguyên liệu cần nhập" liệt kê nguyên liệu âm kho hoặc dưới mức (R19); bên phải là thông tin gói dịch vụ và thao tác nhanh; chân menu bên hiển thị số chi nhánh, nhân viên đã dùng so với giới hạn gói.
- **MH09 Chi nhánh:** danh sách chi nhánh theo BM03, nhãn "3/5 chi nhánh của gói" (R11); mỗi chi nhánh có nút Mã QR (R16); phần dưới là khu vực và bàn của chi nhánh đang chọn: thêm khu vực, thêm bàn, ẩn bàn (R12).
- **MH10 Thực đơn:** danh sách món tải bằng AJAX (A06), lọc danh mục, trạng thái, tìm kiếm, phân trang (R21); món chưa định lượng được tô nổi, cảnh báo "bán không trừ kho" (R15); nút Sửa, Định lượng, Xóa gọi A07, A08 (R14). Hộp thoại Thêm món báo lỗi tại từng trường (6a, 6b), nút Lưu khóa tới khi hợp lệ.
- **MH11 Nhân viên:** danh sách theo BM08, lọc chi nhánh và trạng thái; nhân viên mới hiển thị "Chưa đổi mật khẩu"; nút Chuyển chi nhánh và Khóa tài khoản (P04 bước 3); nhãn "18/50 nhân viên của gói" (R17).
- **MH12 Kho:** tồn kho theo đơn vị cơ sở tại chi nhánh đang chọn; tình trạng Âm kho / Dưới mức / Đủ tính khi hiển thị; nút Nhập hàng mở phiếu nhập BM07, quy đổi đơn vị (P06, R19).
- **MH13 Báo cáo:** tham số và các phần theo BM09: khoảng thời gian, chi nhánh, bốn chỉ số so với kỳ trước, biểu đồ đường theo ngày (Chart.js, A09), doanh thu theo chi nhánh, hóa đơn trong kỳ, top 10 món (R20).
- **768px:** menu bên thu thành nút menu trên thanh trên cùng, danh mục thành dãy chip, bảng món giữ 4 cột.
- **360px:** bảng chuyển thành thẻ, danh mục vuốt ngang, nút chính trải hết chiều ngang.

**a) Responsive**
- Mobile-first, mốc 360px, 768px, 1280px; CSS Flexbox và Grid.
- Vùng chạm tối thiểu 44×44px (R34).
- Bảng dữ liệu trang quản trị chuyển thành thẻ trên điện thoại; menu bên thành menu thu gọn.

**b) SEO**
- Chỉ landing page và trang thực đơn công khai được lập chỉ mục.
- robots.txt chặn /system, /manage, /pos, /api; layout trang quản trị và bán hàng có `<meta name="robots" content="noindex, nofollow">` (R32, R33).

### 3.2.6. Thiết kế xử lý AJAX

- GET để đọc, POST để thay đổi dữ liệu và luôn gửi anti-forgery token trong header.
- Kết quả: JSON `{ success, message, data }` hoặc Partial View.
- Mã trạng thái: 200 thành công; 400 dữ liệu không hợp lệ; 401 chưa đăng nhập/hết phiên; 403 không đủ quyền; 404 không tìm thấy (kể cả bản ghi thuộc tenant khác); 409 xung đột cập nhật đồng thời; 429 quá tần suất.
- Cấu hình OnRedirectToLogin và OnRedirectToAccessDenied: request AJAX (đường dẫn /api hoặc header X-Requested-With) trả 401/403 thay vì 302; hàm JS dùng chung thông báo và chuyển sang trang đăng nhập.

**Bảng 3.21. Endpoint AJAX**

| Mã | Phương thức, đường dẫn | Mục đích | Kết quả | Yêu cầu |
|---|---|---|---|---|
| A01 | GET /api/account/check-email | Kiểm tra trùng email (giới hạn tần suất) | JSON | R01, R36 |
| A02 | GET /api/account/check-slug | Kiểm tra slug quán, gợi ý slug khác (giới hạn tần suất) | JSON | R01, R36 |
| A03 | GET /system/tenants/list | Tìm, lọc, phân trang tenant | Partial View | R05 |
| A04 | POST /system/tenants/{id}/status | Kích hoạt, từ chối, khóa, mở khóa | JSON | R06 |
| A05 | POST /system/tenants/{id}/plan | Đổi gói | JSON | R08 |
| **A06** | GET /manage/products/list | Lọc, tìm kiếm, phân trang món | Partial View | R14, R21 |
| **A07** | POST /manage/products/create, /{id}/update, /{id}/delete | Thêm, sửa, xóa món | JSON | R14 |
| **A08** | POST /manage/products/{id}/recipe | Lưu định lượng | JSON | R15 |
| **A09** | GET /manage/reports/revenue | Dữ liệu biểu đồ theo ngày/chi nhánh/món, so sánh kỳ trước | JSON (Chart.js) | R20 |
| A10 | GET /pos/tables/status | Trạng thái bàn, gọi lại mỗi 10 giây | JSON | R22 |
| A11 | GET /pos/products/search | Tìm, lọc món khi bán | JSON | R24 |
| A12 | POST /pos/orders/open | Mở đơn cho bàn trống | JSON | R23 |
| A13 | POST /pos/orders/{id}/items | Thêm món, đổi số lượng, ghi chú | JSON | R23 |
| A14 | POST /pos/orders/{id}/items/{itemId}/cancel | Hủy món | JSON | R25 |
| A15 | POST /pos/orders/{id}/move | Chuyển bàn, gộp bàn | JSON | R25 |
| A16 | POST /pos/orders/{id}/pay | Thanh toán, trừ kho | JSON | R26, R30 |

Chống CSRF cho AJAX: layout đặt anti-forgery token trong thẻ meta; mọi POST AJAX đi qua một hàm JS dùng chung, gửi token trong header `RequestVerificationToken`. Server cấu hình Antiforgery đọc header này và đăng ký `AutoValidateAntiforgeryToken` toàn cục.

Chèn dữ liệu nhận từ AJAX: dùng `textContent` (hoặc tạo phần tử DOM) thay cho `innerHTML` để tránh XSS.

### 3.2.7. Thiết kế an toàn thông tin (Bảng 3.22)

| Nguy cơ | Giải pháp | Yêu cầu |
|---|---|---|
| Lộ hoặc dò mật khẩu | Identity băm PBKDF2 có salt; mật khẩu ≥ 8 ký tự gồm chữ hoa, thường, số; PasswordSignInAsync với lockoutOnFailure = true, khóa 15 phút sau 5 lần sai; bắt buộc đổi mật khẩu khởi tạo | R36 |
| Dò email, slug qua API công khai | Rate Limiting theo IP cho A01, A02 và trang đăng nhập; vượt ngưỡng trả 429 | R36 |
| SQL Injection | Mọi truy vấn qua EF Core LINQ; không nối chuỗi SQL; SQL thô dùng FromSqlInterpolated | R37 |
| XSS | Razor tự mã hóa; không Html.Raw với dữ liệu người dùng; JS dùng textContent; header Content-Security-Policy | R37 |
| CSRF | AutoValidateAntiforgeryToken toàn cục; AJAX gửi token qua header; cookie SameSite=Lax | R37 |
| Truy cập chéo tenant (sửa ID trên URL) | Global Query Filter, gán TenantId tự động, chặn đổi TenantId; trả 404 thay vì 403 | R38 |
| Khóa ngoại trỏ sang tenant khác | Service tra lại mọi ID tham chiếu qua DbSet đã lọc (3.2.2e); Pos kiểm tra thêm chi nhánh | R38 |
| Leo thang quyền | [Authorize(Roles)] theo Area; vai trò, TenantId, BranchId lấy từ claim; Chủ quán chỉ tạo được tài khoản Staff | R04, R38 |
| Tenant bị khóa dùng phiên cũ | Middleware kiểm tra trạng thái tenant mỗi request (cache ngắn), bị khóa thì đăng xuất | R06, R38 |
| Nhân viên bị khóa/chuyển chi nhánh dùng phiên cũ | Cập nhật SecurityStamp; cookie kiểm tra lại SecurityStamp theo chu kỳ ngắn | R04, R38 |
| Nghe lén, đánh cắp cookie | UseHttpsRedirection, HSTS; cookie HttpOnly, Secure; hết hạn khi không hoạt động | R39 |
| Tải lên file độc hại | Chỉ JPG/PNG/WEBP, kiểm tra phần mở rộng và chữ ký file, ≤ 2 MB; đổi tên GUID; lưu theo thư mục tenant, không cho thực thi | R39 |
| Sửa giá, tổng tiền từ client | Server lấy giá từ CSDL và tự tính tổng | R26 |
| Lạm dụng giảm giá | Giảm giá ≤ tổng tiền hàng; bắt buộc lý do; lưu thu ngân để đối soát | R26 |
| Thanh toán trùng | Giao dịch CSDL + RowVersion trên Orders và BranchStocks; xung đột trả 409 | R42 |
