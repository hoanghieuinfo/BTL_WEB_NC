<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
<!-- Nháp mục Z.7 cho Phụ lục Z. Các ô "Phần nhóm giữ lại / chỉnh sửa / loại bỏ" và "Minh chứng" để trống cho Hiếu điền. -->

## Dòng bổ sung cho Bảng Z.1

| Mục | Ngày | Người dùng | Công cụ | Nội dung hỗ trợ | Vị trí sử dụng |
|---|---|---|---|---|---|
| Z.7 | 21/10/2026 | Hoàng Minh Hiếu | Claude Code; Claude | Lập trình Module 2 – Quản trị quán (Bước 1a–7 trong docs/PROMPTS.md), viết kiểm thử, soạn tài liệu kỹ thuật và nháp Chương 4 | Mã nguồn Area Manage, Infrastructure (khung tạm), test, docs/; Chương 4 mục của Hiếu |

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
