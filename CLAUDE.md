# CLAUDE.md – BTL_WEB_NC (BTL Lập trình Web nâng cao, nhóm WNC.G12)

Đề tài: **Quản lý (nhiều) quán cafe theo mô hình SaaS**. Người dùng Claude Code trong repo này là
**Hoàng Minh Hiếu**, phụ trách **Module 2 – Quản trị quán** (Area `Manage`, vai trò `TenantAdmin`).

## 1. Tài liệu nguồn (đọc trước khi làm)

- `docs/SPEC.md`: Chương 2–3 của báo cáo, là **đặc tả chính thức**. Khi code và đặc tả mâu thuẫn, theo đặc tả;
  nếu đặc tả thiếu hoặc mâu thuẫn với chính nó, **dừng lại và hỏi**, không tự đoán.
  - Quy trình P01–P09, biểu mẫu BM01–BM09, yêu cầu R01–R42
  - Đặc tả UC10 (Bảng 3.4), UC16 (Bảng 3.6)
  - Cơ chế đa tenant: mục 3.2.2
  - Bảng CSDL: Bảng 3.8–3.18
  - Ma trận phân quyền: Bảng 3.19
  - Màn hình và URL: Bảng 3.20
  - Endpoint AJAX: Bảng 3.21
  - An toàn thông tin: Bảng 3.22
- `docs/design/*.png`: ảnh export từ Figma (file WNC). **Không gọi Figma MCP** (tài khoản Starter, hạn mức thấp);
    chỉ xem ảnh trong thư mục này. Tên ảnh giữ nguyên tên frame Figma, bắt đầu bằng mã màn hình,
  ví dụ `MH10 – Thêm món – lỗi.png`, `MH10 – Thực đơn (Mobile 360px).png`. Tìm ảnh của một màn hình bằng mẫu `MH10*.png`.

## 2. Môi trường

- Hệ điều hành: Windows, VS Code, terminal PowerShell.
- SQL Server LocalDB, connection string trong `BTL_WEB_NC/appsettings.json`:
  `Server=(localdb)\\MSSQLLocalDB;Database=CafeSaaS;Trusted_Connection=True;TrustServerCertificate=True`
- .NET 8 SDK, công cụ `dotnet-ef` (`dotnet tool install --global dotnet-ef`).
- Không ghi mật khẩu thật vào `appsettings.json`.

Lệnh thường dùng (chạy ở thư mục gốc `BTL_WNC`):

```powershell
dotnet build
dotnet ef migrations add <Ten> --project BTL_WEB_NC
dotnet ef database update --project BTL_WEB_NC
dotnet run --project BTL_WEB_NC
dotnet test
```

## 3. Công nghệ

ASP.NET Core MVC .NET 8, Razor View, EF Core 8 + SQL Server, ASP.NET Core Identity (cookie),
CSS tự viết (**không** Bootstrap/Tailwind), JavaScript thuần với `fetch`, Chart.js, QRCoder.
Test: xUnit + EF Core SQLite in-memory.

## 4. Cấu trúc thư mục

```
BTL_WNC/                          ← thư mục gốc, mở trong VS Code
  CLAUDE.md
  BTL_WNC.sln
  BTL_WEB_NC/                     ← project MVC (namespace gốc: BTL_WEB_NC)
    Data/                 AppDbContext, cấu hình entity, SeedData, Migrations
    Models/Entities/      16 bảng + AppUser (đúng tên Bảng 3.8)
    Infrastructure/
      Tenancy/            ITenantEntity, ISoftDelete, TenantContext, TenantResolutionMiddleware   [TẠM – Module 1]
      Ajax/               ApiResult, cấu hình 401/403 cho AJAX                                    [TẠM – Module 1]
      Files/              ImageUploadService
    Areas/Manage/         ← PHẦN CỦA HIẾU
      Controllers/ Services/ Models/ Views/
    Controllers/AccountController.cs  đăng nhập tối giản                                         [TẠM – Module 1]
    Controllers/HomeController.cs     có sẵn từ template, giữ nguyên
    wwwroot/css/site.css  biến màu, layout chung
    wwwroot/js/ajax.js    hàm AJAX dùng chung
    wwwroot/js/manage/    JS riêng từng màn hình Module 2
  BTL_WEB_NC.Tests/               ← project test xUnit
  docs/                   SPEC.md, PROMPTS.md, design/, Chuong4_Hieu.md
```

File/thư mục đánh dấu **[TẠM]** là khung tạm thay cho Việc 8 của Đạt (chưa có repo chung). Mỗi file tạm có
comment `TẠM: thay bằng bản của Module 1 khi gộp repo`. Khi gộp, chỉ mang `Areas/Manage`, `wwwroot/js/manage` và các test sang.

## 5. Phạm vi

- **Được làm:** Area `Manage` (MH08–MH13), service trừ kho `IInventoryDeductionService` (Việc 12), khung tạm ở mục 4.
- **Không làm:** landing page, đăng ký, trang Super Admin (Module 1 – Đạt); màn hình POS, thanh toán, thực đơn công khai
  (Module 3 – Trung). Nếu một việc của Module 2 cần thứ thuộc module khác, chỉ tạo interface hoặc bản tạm tối thiểu và báo lại.
- Ngoài phạm vi đề tài (mục 1.3.6): cổng thanh toán, thu phí thật, app di động, kế toán/thuế.

## 6. Quy tắc bắt buộc

### Đa tenant (R38, mục 3.2.2)
- Mọi entity nghiệp vụ cài `ITenantEntity`. `AppUser` có `TenantId` nhưng **không** cài `ITenantEntity`;
  service quản lý nhân viên tự lọc theo `TenantId` của chủ quán.
- `AppDbContext` đăng ký Global Query Filter cho mọi `ITenantEntity`, tham chiếu **thuộc tính của DbContext**
  (lấy từ `TenantContext`), không dùng biến cục bộ. Entity có xóa mềm gộp `TenantId` và `!IsDeleted` trong **một** biểu thức.
- `SaveChanges`: tự gán `TenantId` cho bản ghi thêm mới; từ chối khi không xác định được tenant; từ chối khi bản ghi cũ bị đổi `TenantId`.
- Mọi ID tham chiếu nhận từ request (CategoryId, IngredientId, BranchId, ZoneId…) phải tra lại qua DbSet đã lọc
  trước khi gán; không tìm thấy → **404** (không phải 403).
- `TenantId`, `BranchId`, vai trò chỉ lấy từ claim phía server, **không bao giờ** nhận từ request.
- Không gọi `IgnoreQueryFilters()` trong Area `Manage`.

### Giới hạn gói (R11, R17)
Trước khi thêm chi nhánh/nhân viên, đếm bản ghi hiện có so với `MaxBranches`/`MaxStaff` của gói.

### AJAX (mục 3.2.6, Bảng 3.21)
- GET để đọc, POST để ghi. Endpoint đúng Bảng 3.21: A06–A09.
- Phản hồi: JSON `{ success, message, data }` (lỗi validate: `data.errors` dạng `{ tenTruong: [thông báo] }`) hoặc Partial View.
- Mã trạng thái: 200, 400, 401, 403, 404, 409 (xung đột RowVersion), 429.
- Mọi POST AJAX đi qua `ajax.js`, gửi token trong header `RequestVerificationToken`.
  Server bật filter `AutoValidateAntiforgeryToken` toàn cục.
- JS chèn dữ liệu bằng `textContent` hoặc tạo phần tử DOM; **không** dùng `innerHTML` với dữ liệu người dùng.
  Ngoại lệ: chèn Partial View do server render (đã được Razor mã hóa).

### Kiến trúc
- Controller mỏng: nhận request, validate ModelState, gọi service, trả kết quả. Nghiệp vụ nằm trong `Areas/Manage/Services`.
- Controller gốc của Area: `[Area("Manage")]`, `[Authorize(Roles = "TenantAdmin")]`.
- Tên bảng/cột đúng Bảng 3.9–3.17. Bảng khu vực tên `Zones` (không dùng `Areas`).
- Thao tác nhiều bước (nhập kho, trừ kho) chạy trong một transaction.
- Không xóa cứng dữ liệu đã phát sinh giao dịch (R42): dùng `IsDeleted` hoặc `IsActive`.
- Tiền: `decimal(18,0)`; số lượng/định lượng: `decimal(18,3)`.

### An toàn thông tin (Bảng 3.22)
- Không nối chuỗi SQL; nếu bắt buộc dùng SQL thô thì `FromSqlInterpolated`.
- Không dùng `Html.Raw` với dữ liệu người dùng.
- Upload ảnh: chỉ JPG/PNG/WEBP, kiểm tra **cả phần mở rộng và chữ ký file (magic bytes)**, tối đa 2 MB,
  đổi tên bằng GUID, lưu `wwwroot/uploads/{tenantId}/`.
- Chủ quán chỉ tạo được tài khoản vai trò `Staff`. Khóa hoặc chuyển chi nhánh nhân viên phải gọi `UpdateSecurityStampAsync`.

### Giao diện (R31, R33, R34)
- Bám ảnh trong `docs/design/`. Màu đặt làm biến CSS trong `:root`:
  ```css
  --sidebar:#2B1D16; --bg:#F6F7F5; --card:#FFFFFF; --border:#E4E2DE;
  --text:#201B18; --muted:#A09A97; --primary:#2F6B4F; --primary-soft:#E6F0EA;
  --warn-bg:#FBF0DC; --danger:#C0392B;
  ```
- Sidebar trái 248px, topbar có breadcrumb `Tên quán / Trang`; dưới sidebar có mức dùng chi nhánh/nhân viên so với gói.
- Mobile-first, mốc `min-width: 768px` và `min-width: 1280px`; dưới 768px bảng chuyển thành thẻ, sidebar thành menu thu gọn.
- Vùng chạm tối thiểu 44×44px. Toàn bộ chữ tiếng Việt.
- `_ManageLayout.cshtml` có `<meta name="robots" content="noindex, nofollow">`.
- Mỗi màn hình có trạng thái đang tải, danh sách rỗng và lỗi như các frame Figma tương ứng.

## 7. Quy tắc sử dụng AI của nhóm (mục 1.4)

- **AUR.03 – ghi nguồn gốc.** Mỗi file Claude tạo hoặc sửa đáng kể có comment ở đầu file, đúng cú pháp của loại file:
  - `.cs`, `.js`: `// [AI] Claude hỗ trợ – Phụ lục Z.7`
  - `.cshtml`: `@* [AI] Claude hỗ trợ – Phụ lục Z.7 *@`
  - `.css`: `/* [AI] Claude hỗ trợ – Phụ lục Z.7 */`
  - `.md`: `<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->`
- Commit message có tiền tố `[AI]`, ví dụ `[AI] MH10: thêm/sửa/xóa món bằng AJAX`.
- **AUR.01.** Hiếu phải hiểu mọi đoạn code. Sau mỗi bước, giải thích ngắn bằng tiếng Việt các quyết định quan trọng
  (vì sao lọc ở đâu, vì sao trả 404…) để Hiếu trả lời được khi bảo vệ.
- Không xóa hoặc sửa comment `[AI]` đã có.

## 8. Định nghĩa "xong" cho mỗi bước

Một bước chỉ được báo là xong khi:
1. `dotnet build` không lỗi, không cảnh báo nullable mới.
2. `dotnet test` qua hết.
3. Có migration nếu đổi model, và `dotnet ef database update` chạy được.
4. Đã đối chiếu từng yêu cầu Rxx của bước đó, ghi rõ yêu cầu nào **chưa** làm (nếu có).
5. Cuối câu trả lời liệt kê: file đã tạo/sửa, lệnh để chạy thử, các bước Hiếu tự kiểm tra trên trình duyệt
   (kể cả ở 360px và 768px).

Test bắt buộc:
- **Cô lập tenant:** đăng nhập tenant A, đọc/sửa/xóa ID của tenant B → 404; gửi khóa ngoại (CategoryId, IngredientId…)
  của tenant B → 404 và không có bản ghi nào được tạo.
- **Trừ kho (Việc 12):** đúng lượng theo định lượng × số lượng; bỏ qua món hủy và món chưa định lượng;
  tạo dòng tồn nếu chưa có; cho phép tồn âm; xung đột RowVersion ném lỗi để trả 409.
- **Giới hạn gói:** vượt `MaxBranches`/`MaxStaff` bị chặn.
- **Upload:** file `.jpg` nhưng nội dung không phải ảnh bị từ chối.

## 9. Giao tiếp với module khác

- **Với Trung (Module 3):** Việc 12 cung cấp
  `Task DeductForOrderAsync(long orderId, CancellationToken ct = default)` trong `IInventoryDeductionService`.
  Service **không tự mở transaction**; Trung gọi nó bên trong transaction của action A16 (`POST /pos/orders/{id}/pay`).
  Phải thống nhất chữ ký này với Trung trước khi code.
- **Với Đạt (Module 1):** Area `Manage` chỉ dùng `TenantContext` (TenantId, BranchId, Role) và vai trò `TenantAdmin`.
  Không phụ thuộc chi tiết nào khác của Module 1.

## 10. Cách làm việc

- Mỗi phiên làm một bước trong `docs/PROMPTS.md`. Với bước lớn, trình bày kế hoạch trước, chờ Hiếu đồng ý rồi mới sửa file.
- Không tự thêm thư viện ngoài danh sách ở mục 3; cần thêm thì hỏi trước.
- Không sửa `docs/SPEC.md` và ảnh trong `docs/design/`.
- Gặp điểm đặc tả chưa rõ: dừng lại, nêu 2–3 phương án, hỏi Hiếu.
