<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
<!-- Nháp mục Z.7 cho Phụ lục Z. Các ô "Phần nhóm giữ lại / chỉnh sửa / loại bỏ" và "Minh chứng" để trống cho Hiếu điền. -->

## Dòng bổ sung cho Bảng Z.1

| Mục | Ngày | Người dùng | Công cụ | Nội dung hỗ trợ | Vị trí sử dụng |
|---|---|---|---|---|---|
| Z.7 | 21/10/2026 | Hoàng Minh Hiếu | Claude Code; Claude | Lập trình Module 2 – Quản trị quán (Bước 1a–7 trong docs/PROMPTS.md), viết kiểm thử, soạn tài liệu kỹ thuật và nháp Chương 4 | Mã nguồn Area Manage, Infrastructure (khung tạm), test, docs/; Chương 4 mục của Hiếu |
| Z.7 | 01/11/2026 | Trung | Claude Code; Claude | Lập trình Module 3 – Bán hàng (Bước 1–11 trong docs/KeHoach_Module3_Trung.md), viết kiểm thử, cập nhật tài liệu kỹ thuật | Mã nguồn Area Pos, test, docs/ |

## Z.7. Lập trình Module 2 – Quản trị quán

| Thông tin | Nội dung |
|---|---|
| Ngày sử dụng | 21/10/2026 |
| Người sử dụng | Hoàng Minh Hiếu |
| Công cụ, phiên bản | Claude (Anthropic): Claude Code (extension VS Code), mô hình Claude Opus 5.5, dùng để viết mã, chạy build/test và soạn tài liệu trong repo; kết hợp Claude trên giao diện claude.ai để soạn CLAUDE.md, docs/PROMPTS.md và docs/SPEC.md |
| Mục đích | Hỗ trợ lập trình Module 2 (màn hình MH08–MH13, Area Manage, vai trò Chủ quán) và service trừ kho (Việc 12), bám đặc tả Chương 2–3 (docs/SPEC.md) và quy tắc của nhóm trong CLAUDE.md. |
| Nội dung yêu cầu | 1. Bước 1a: entity theo Bảng 3.8–3.18, AppDbContext đa tenant (Global Query Filter, kiểm tra TenantId khi lưu), migration, test cô lập tenant.<br>2. Bước 1b: Identity, middleware xác định tenant, đăng nhập tạm, dữ liệu mẫu (seed), layout trang quản trị, hàm AJAX dùng chung.<br>3. Bước 2 – MH10: thực đơn, danh mục, định lượng, tải ảnh món bằng AJAX (A06–A08).<br>4. Bước 3 – MH09: chi nhánh, khu vực, bàn, mã QR.<br>5. Bước 4 – MH11: nhân viên, mật khẩu khởi tạo, chuyển chi nhánh, khóa tài khoản; bổ sung sửa thông tin và đặt lại mật khẩu.<br>6. Bước 5 – MH12 và Việc 12: nguyên liệu, phiếu nhập, tồn kho theo chi nhánh; service trừ kho và tài liệu hướng dẫn cho Module 3.<br>7. Bước 6 – MH13 và MH08: báo cáo doanh thu (A09, Chart.js), tổng quan quán.<br>8. Bước 7: rà soát Area Manage theo CLAUDE.md (bảng vấn đề, sửa sau khi duyệt), lập danh sách endpoint bổ sung.<br>9. Bước 8: soạn nháp Chương 4 (chức năng Quản lý thực đơn) và mục Z.7 này. |
| Kết quả AI trả về | Với mỗi bước: kế hoạch để duyệt trước khi sửa (với bước lớn); mã nguồn C#/Razor/CSS/JavaScript; test xUnit; báo cáo cuối bước bằng tiếng Việt gồm quyết định thiết kế, file đã sửa, lệnh chạy thử và các bước tự kiểm tra trên trình duyệt. Kết quả chính:<br>– Area Manage đủ 6 màn hình MH08–MH13.<br>– Service trừ kho `IInventoryDeductionService` và tài liệu docs/Viec12_HuongDanTrung.md.<br>– 167 ca kiểm thử tự động đều đạt.<br>– Tài liệu docs/EndpointBoSung.md (A17–A28), docs/Chuong4_Hieu.md (nháp).<br>– Bảng rà soát 9 vấn đề ở Bước 7 và bản sửa sau khi duyệt. |
| Phần nhóm giữ lại |  |
| Phần nhóm chỉnh sửa |  |
| Phần nhóm loại bỏ |  |
| Vị trí sử dụng | Mã nguồn: `BTL_WEB_NC/Areas/Manage/`, `BTL_WEB_NC/Infrastructure/` (khung tạm thay cho Module 1), `BTL_WEB_NC/Data/`, `BTL_WEB_NC/wwwroot/js/`, `BTL_WEB_NC/wwwroot/css/`; kiểm thử: `BTL_WEB_NC.Tests/`; tài liệu: `docs/`; báo cáo: Chương 4, mục của Hoàng Minh Hiếu. |
| Minh chứng |  |
| Tuân thủ quy tắc | AUR.01: Hiếu duyệt kế hoạch trước mỗi bước lớn; sau mỗi bước Claude giải thích bằng tiếng Việt các quyết định quan trọng (lọc tenant ở đâu, vì sao trả 404…) để Hiếu nắm và trả lời được khi bảo vệ. AUR.03: mọi file Claude tạo hoặc sửa đáng kể có comment `[AI] Claude hỗ trợ – Phụ lục Z.7` ở đầu file; commit có tiền tố `[AI]`. Ngoại lệ: commit d1edac7 (Bước 1a) có dùng Claude nhưng thiếu tiền tố `[AI]` do sơ suất; các file của commit này vẫn có comment `[AI]`. AUR.04: [Hiếu bổ sung]. AUR.99: footnote tại [Hiếu bổ sung]. |

## Z.7 (tiếp). Lập trình Module 3 – Bán hàng (Area Pos)

| Thông tin | Nội dung |
|---|---|
| Ngày sử dụng | 01/11/2026 (các bước 1–11 trong docs/KeHoach_Module3_Trung.md) |
| Người sử dụng | Trung (Module 3) – [bổ sung họ tên đầy đủ] |
| Công cụ, phiên bản | Claude (Anthropic), mô hình Claude Sonnet 5.5 qua Claude Code |
| Mục đích | Hỗ trợ lập trình Module 3: MH14 Sơ đồ bàn, MH15 Đơn hàng và thanh toán, MH16 In phiếu/hóa đơn, MH17 Hóa đơn trong ngày (R22–R28, R30), bám docs/SPEC.md và CLAUDE.md. |
| Nội dung yêu cầu | Bước 1–11 trong docs/KeHoach_Module3_Trung.md: nối sơ đồ bàn với A12; trang đơn hàng; A11 tìm món; A13 thêm món; A14 hủy món; A15 chuyển và gộp bàn; A29 tạm tính, A30 hủy đơn; A16 thanh toán và trừ kho; MH16 in phiếu (khổ 80mm); MH17 hóa đơn trong ngày; hoàn thiện, kiểm thử, cập nhật tài liệu. |
| Kết quả AI trả về | Mã C#/Razor/CSS/JS trong `Areas/Pos/`, `wwwroot/js/pos/`, `wwwroot/css/pos/`; test xUnit trong `BTL_WEB_NC.Tests/Pos/` (có test cô lập tenant/chi nhánh, thanh toán hai lần, hóa đơn trong ngày); cập nhật docs/EndpointBoSung.md (A15 tách `/transfer`, `/merge`; A29, A30; trang print và invoices). `dotnet build` thành công (còn 1 cảnh báo CS0105 trùng using ở Data/SeedData.cs có từ trước). Kết quả `dotnet test` và kiểm tra giao diện 360/768/1280px: [Trung bổ sung sau khi chạy]. |
| Phần nhóm giữ lại |  |
| Phần nhóm chỉnh sửa |  |
| Phần nhóm loại bỏ |  |
| Vị trí sử dụng | `BTL_WEB_NC/Areas/Pos/`, `BTL_WEB_NC/wwwroot/js/pos/`, `BTL_WEB_NC/wwwroot/css/pos/`, `BTL_WEB_NC.Tests/Pos/`, `docs/`. |
| Minh chứng |  |
| Tuân thủ quy tắc | AUR.01: Trung duyệt từng bước và nhận giải thích các quyết định (trả 404 thay vì 403, tính tiền ở server, RowVersion chống thanh toán trùng, lọc ngày bằng khoảng nửa mở). AUR.03: file Claude tạo hoặc sửa có comment `[AI] Claude hỗ trợ – Phụ lục Z.7`. AUR.04, AUR.99: [bổ sung]. |
