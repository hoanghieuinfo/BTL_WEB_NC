<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->

# Bộ prompt sửa lỗi Module 1 (Nguyễn Thành Đạt)

Kết quả rà soát code Module 1 ngày 05/11/2026, đối chiếu với `docs/SPEC.md` và `CLAUDE.md`.
Cách dùng giống `docs/PROMPTS.md`: mỗi bước mở **một phiên mới**, bật Plan mode (Shift+Tab), dán prompt,
duyệt kế hoạch rồi mới cho Claude Code sửa file. Làm theo thứ tự D1 → D5. D4 phải làm sau D3,
vì CSP chỉ bật được khi view không còn script và style inline.

## Đã sửa trong phiên trước (không làm lại)

- Route A04, A05 và `plans/{id}/toggle` thiếu tiền tố `system/` nên trả 404.
- `_SystemLayout` không tìm thấy partial `_Icon` nên mọi trang `/system` trả 500.
- `Tenants/Index.cshtml` gọi `Ajax.get` (không tồn tại); đã đổi thành `Ajax.getHtml`.
- `.section-title` 24px của landing page đè lên tiêu đề mục ở `/manage`.
- Đổi tên nền tảng "Cà Phê Mây" thành **CafeSaaS** (trùng tên quán mẫu).
- `_TenantList` dưới 768px chuyển bảng thành thẻ (`wwwroot/css/system/tenants.css`).

## Tiến độ

| Bước | Trạng thái | Ghi chú |
|---|---|---|
| D1 | Xong (05/11/2026) | Lỗi 1–3. Thêm `Infrastructure/Auth/` (middleware đổi mật khẩu, rate limit, `PostLoginRedirect`). |
| D2 | Xong (05/11/2026) | Lỗi 4–8. Thêm endpoint A29 (`docs/EndpointBoSung.md`). Hết hạn gói tính theo `PlanExpiresAt`, không cần migration. |
| D3 | Xong (05/11/2026) | Lỗi 9–11. CSS ở `css/system/`, JS ở `js/system/`; dùng chung `js/manage/layout.js`, `ui.js` với Area Manage (thay cho `js/system/layout.js`). Đổi mật khẩu, đăng ký thành công chuyển sang `_AuthLayout`. Sửa kèm: `PlansController` trả lỗi validate dạng `data.errors`; `toast-region` thiếu id nên toast hiện bằng `alert()`. |
| D4 | Xong (05/11/2026) | Lỗi 12–15. CSP thêm `blob:` vào `img-src` (ảnh xem trước MH10). Đã thử bằng Edge headless ở 360px và 768px: 16 trang (Module 1 và MH08–MH13) không có vi phạm CSP, lỗi console hay lỗi JS. Sửa kèm: header landing tràn 61px ở 360px. Còn lại (Module 2): nút ⋯ trên thẻ nhân viên MH11 tràn 8px ở 360px. |
| D5 | Xong (05/11/2026) | Lỗi 16. Test ở `BTL_WEB_NC.Tests/SystemArea` (không đặt tên `System` vì namespace `BTL_WEB_NC.Tests.System` che namespace `System` của .NET). Thêm 36 test: đăng ký (kể cả rollback khi tạo tài khoản lỗi), lọc/phân trang/đổi gói, gói dịch vụ, phân quyền Area System. Tổng 242 test qua. |

## Danh sách lỗi còn lại

| # | Mức | Chỗ | Lỗi | Bước |
|---|---|---|---|---|
| 1 | Cao | `AccountController.Login` | Có `returnUrl` thì chuyển hướng **trước** khi xét `MustChangePassword` → bỏ qua được R03. Ngoài lần đăng nhập, không có gì chặn người dùng đi thẳng tới `/manage` khi chưa đổi mật khẩu. | D1 |
| 2 | Cao | A01, A02, `POST /dang-nhap` | Chưa giới hạn tần suất (Bảng 3.21 ghi "giới hạn tần suất", mã 429). Ai cũng dò được email đã đăng ký. | D1 |
| 3 | Trung bình | `AccountController.Register` | `catch (Exception ex)` đưa `ex.Message` ra giao diện → lộ chi tiết lỗi nội bộ (Bảng 3.22). | D1 |
| 4 | Cao | `SystemTenantService` | Số nhân viên đếm **mọi** tài khoản đang hoạt động của tenant, tính cả chủ quán. CLAUDE.md quy định chỉ đếm vai trò Staff. Sai ở danh sách (cột "Quy mô") và ở kiểm tra hạ gói (`ChangePlanAsync`) → chặn nhầm khi hạ gói. | D2 |
| 5 | Trung bình | `SystemTenantService` | Lấy "chủ quán" bằng `FirstOrDefault(u => u.TenantId == t.Id)` → có thể ra một nhân viên. Phải lấy tài khoản vai trò TenantAdmin. | D2 |
| 6 | Trung bình | `UpdateStatusAsync` | Từ chối không bắt buộc và không lưu lý do (P01 bước 5: "từ chối (ghi lý do)"). Không kiểm tra chuyển trạng thái hợp lệ: gửi tay `status: 0` hoặc giá trị ngoài enum vẫn lưu được. `LockReason` không giới hạn độ dài. | D2 |
| 7 | Đã chốt | P02 bước 3 | Chưa có chức năng "Đánh dấu tenant hết hạn gói". `PlanExpiresAt` tính từ ngày đăng ký, không phải ngày kích hoạt. | D2 |
| 8 | Thấp | `TenantRegistrationService` | Chủ quán mới được gán `BranchId` = chi nhánh mặc định, trong khi seed để `null` cho TenantAdmin. Hai cách không thống nhất. | D2 |
| 9 | Trung bình | View Area System | Khoảng 110 thuộc tính `style="..."` và 4 khối `<script>` inline (Dashboard, Plans, Tenants, layout). Module 2 đã bỏ JS inline ở Bước 7; còn lại thì không bật được CSP. | D3 |
| 10 | Trung bình | `System/Dashboard/Index.cshtml` | `Html.Raw(JsonSerializer.Serialize(...))` chứa tên gói do người dùng nhập (trái quy tắc "không Html.Raw với dữ liệu người dùng"). Chart.js tải từ CDN, không ghi phiên bản, trong khi Module 2 dùng `~/lib/chartjs/chart.umd.min.js`. `minmax(360px, 1fr)` gây tràn ngang ở 360px. | D3 |
| 11 | Trung bình | MH05, MH07 | Chưa chuyển bảng thành thẻ dưới 768px. Thiếu trạng thái đang tải, rỗng, lỗi như Module 2. | D3 |
| 12 | Cao | `Program.cs` | Chưa có header CSP (CLAUDE.md mục 9). | D4 |
| 13 | Trung bình | `wwwroot/robots.txt` | `Sitemap: https://app.caphemay.vn/...` ghi cứng tên miền cũ. Có dòng `Disallow: /tai-khoan/` nhưng thiếu các trang không cần lập chỉ mục như `/dang-nhap`. | D4 |
| 14 | Thấp | `SitemapController` | Có `/dang-ky`, trong khi SPEC mục 3.2.5 chỉ cho lập chỉ mục landing page và thực đơn công khai. | D4 |
| 15 | Đã chốt | `global.json` | Đạt đổi SDK `8.0.426 / latestFeature` thành `9.0.318 / latestMajor`. CLAUDE.md ghi .NET 8 SDK. | D4 |
| 16 | Trung bình | `BTL_WEB_NC.Tests` | Module 1 chưa có test nào (đăng ký, đổi trạng thái tenant, hạ gói, giới hạn tần suất). | D5 |

---

## Bước D1 – Đăng nhập, đăng ký, đổi mật khẩu (lỗi 1–3)

```
Đọc CLAUDE.md, docs/SPEC.md (R01–R03, R36, Bảng 3.21 dòng A01–A02, Bảng 3.22) và docs/PROMPTS_Module1_SuaLoi.md lỗi 1–3. Phạm vi: Controllers/AccountController.cs, Program.cs, Infrastructure (thêm mới nếu cần). Không sửa Area Manage.
1. R03: khi MustChangePassword = true, mọi request đã đăng nhập (trừ /tai-khoan/doi-mat-khau, /dang-xuat, file tĩnh) đều chuyển tới trang đổi mật khẩu; request AJAX trả 403 JSON { success:false, message } theo ApiResult. Làm bằng middleware hoặc filter, đặt sau UseAuthentication. Trong Login, xét MustChangePassword TRƯỚC returnUrl. Lấy cờ từ claim để không truy vấn CSDL mỗi request; sau khi đổi mật khẩu, RefreshSignInAsync để cập nhật claim.
2. Rate limiting bằng Microsoft.AspNetCore.RateLimiting có sẵn trong .NET 8 (không thêm thư viện): policy theo IP cho A01, A02 (ví dụ 20 request/phút) và POST /dang-nhap (ví dụ 10 request/phút). Vượt thì trả 429; với AJAX trả JSON theo ApiResult để ajax.js hiện thông báo 429 có sẵn.
3. Register: bỏ catch (Exception ex) đưa ex.Message ra giao diện. Ghi log lỗi bằng ILogger, hiện thông báo chung "Không thể đăng ký lúc này. Vui lòng thử lại sau."
4. Test xUnit: Login có returnUrl mà MustChangePassword = true thì vẫn vào trang đổi mật khẩu; middleware chặn /manage khi chưa đổi mật khẩu; vượt giới hạn A01 trả 429.
Giải thích ngắn bằng tiếng Việt vì sao xét MustChangePassword trước returnUrl và vì sao giới hạn theo IP.
```

## Bước D2 – Nghiệp vụ tenant và gói (lỗi 4–8)

Trước khi dán prompt, Hiếu và Đạt chốt hai điểm SPEC chưa rõ (lỗi 7):

- **Hết hạn gói (P02 bước 3).**
  - (a) Thêm trạng thái `Expired` vào enum, kèm migration. Super Admin đánh dấu bằng tay.
  - (b) Không thêm trạng thái; coi tenant là hết hạn khi `PlanExpiresAt < hôm nay`. Danh sách hiện nhãn "Hết hạn" và có bộ lọc riêng.
  - (c) Như (b), và thêm nút "Gia hạn" để đặt lại `PlanExpiresAt`.
- **Ngày bắt đầu dùng thử.** Tính `PlanExpiresAt` từ ngày **đăng ký** (như hiện tại) hay từ ngày **kích hoạt**?

**Đã chốt (05/11/2026):** hết hạn gói theo phương án **(c)**, tức là không thêm trạng thái mà tính theo
`PlanExpiresAt` và có nút "Gia hạn". `PlanExpiresAt` tính từ ngày **đăng ký**, giữ như code hiện tại.
Prompt dưới đây đã điền sẵn hai quyết định này.

```
Đọc CLAUDE.md, docs/SPEC.md (P01, P02, R05–R08, mục 3.2.2), docs/EndpointBoSung.md và docs/PROMPTS_Module1_SuaLoi.md lỗi 4–8. Phạm vi: Areas/System/Services, Areas/System/Models, Areas/System/Controllers, Areas/System/Views, Services/TenantRegistrationService.cs.
Đã chốt: KHÔNG thêm trạng thái Expired vào enum, không cần migration. Tenant được coi là hết hạn khi PlanExpiresAt có giá trị và PlanExpiresAt.Date < VietnamTime.Now.Date. PlanExpiresAt vẫn tính từ ngày đăng ký (giữ nguyên TenantRegistrationService). Hết hạn chỉ là đánh dấu (mô phỏng), không chặn đăng nhập.
1. Đếm nhân viên đúng quy tắc CLAUDE.md mục 6: chỉ tài khoản vai trò Staff và IsActive = true. Áp dụng cho cả danh sách tenant và ChangePlanAsync. Lọc theo vai trò bằng join với UserRoles/Roles trong một truy vấn, không gọi IsInRoleAsync trong vòng lặp.
2. "Chủ quán" là tài khoản vai trò TenantAdmin của tenant đó.
3. UpdateStatusAsync:
   - Kiểm tra Enum.IsDefined; chỉ cho các chuyển trạng thái: Chờ → Hoạt động/Từ chối; Hoạt động → Khóa; Khóa → Hoạt động. Trái quy tắc thì ném BusinessRuleException (400).
   - Từ chối và Khóa đều bắt buộc lý do, lưu vào LockReason. Giới hạn độ dài lý do bằng [StringLength] khớp độ dài cột trong cấu hình entity.
   - Cập nhật modal và JS ở Tenants/Index.cshtml và Dashboard/Index.cshtml: nút Từ chối mở hộp thoại nhập lý do (dùng lại hộp thoại Khóa).
4. Hết hạn gói (P02 bước 3):
   - TenantSummaryDto thêm IsExpired (tính trong service). _TenantList hiện nhãn "Hết hạn" cạnh trạng thái; ngày hết hạn tô màu --danger.
   - Bộ lọc A03 thêm tham số expired=true (chỉ tenant đã hết hạn), lọc bằng truy vấn EF, không lọc sau khi đã tải về bộ nhớ. Thêm lựa chọn "Hết hạn gói" vào ô lọc ở Tenants/Index.
   - MH05: thêm thẻ số liệu "Hết hạn gói".
   - Nút "Gia hạn" (tenant Hoạt động hoặc Bị khóa) mở hộp thoại chọn ngày hết hạn mới. Endpoint mới POST /system/tenants/{id}/expiry nhận { planExpiresAt }; ngày phải sau hôm nay, nếu không thì 400 với data.errors.planExpiresAt; tenant không tồn tại thì 404. Ghi endpoint này vào docs/EndpointBoSung.md (mã A29, theo mẫu các dòng A17–A28).
5. TenantRegistrationService: tài khoản chủ quán để BranchId = null như SeedData. Rà Area Manage xem có chỗ nào cần BranchId của TenantAdmin không; nếu có thì báo lại, không tự sửa Area Manage.
6. Test xUnit: hạ gói khi tenant có 1 chủ quán + MaxStaff nhân viên thì được phép; vượt thì bị chặn; Từ chối không có lý do thì 400; chuyển trạng thái sai thì 400; khóa tenant thì SecurityStamp mọi tài khoản đổi; lọc expired=true chỉ ra tenant có PlanExpiresAt trước hôm nay; gia hạn về ngày quá khứ thì bị từ chối, ngày hợp lệ thì lưu đúng.
```

## Bước D3 – Giao diện Area System (lỗi 9–11)

```
Đọc CLAUDE.md mục 6 (Giao diện, AJAX, An toàn thông tin) và docs/PROMPTS_Module1_SuaLoi.md lỗi 9–11. Phạm vi: Areas/System/Views, wwwroot/css/system, wwwroot/js/system, Views/Account/ChangePassword.cshtml, Views/Account/RegisterSuccess.cshtml.
Dùng Areas/Manage/Views làm mẫu (cách đặt CSS/JS, bảng → thẻ, trạng thái tải/rỗng/lỗi).
1. Chuyển toàn bộ style="..." trong các view trên thành class trong wwwroot/css/system/*.css (mỗi màn hình một file, dùng biến màu trong :root). Đã có sẵn css/system/tenants.css cho _TenantList; mở rộng file này cho Tenants/Index.
2. Chuyển mọi khối <script> inline ra wwwroot/js/system/{dashboard,plans,tenants,layout}.js. Không dùng onclick="..." inline. Thông báo lỗi chèn bằng textContent, không ghép HTML.
3. Dashboard: bỏ Html.Raw. Đưa dữ liệu biểu đồ bằng thuộc tính data-* (Razor tự mã hóa) hoặc <script type="application/json"> qua Json.Serialize, rồi JSON.parse trong file JS. Chart.js dùng ~/lib/chartjs/chart.umd.min.js giống MH13, bỏ CDN. Sửa lưới biểu đồ để không tràn ngang ở 360px (mobile-first một cột, từ 768px mới hai cột).
4. Dưới 768px: bảng ở MH05 (quán chờ duyệt) và MH07 (gói dịch vụ) chuyển thành thẻ như _TenantList. Vùng chạm ≥ 44px.
5. Thêm trạng thái đang tải và lỗi cho danh sách tenant; trạng thái rỗng cho bảng quán chờ duyệt ở MH05.
6. Xác nhận lại sau khi sửa: grep 'style="' và '<script>' trong Areas/System/Views không còn kết quả (trừ <script src=...>).
Liệt kê các bước kiểm tra trên trình duyệt ở 360px, 768px, 1280px.
```

## Bước D4 – CSP, SEO, cấu hình (lỗi 12–15)

Trước khi dán, Hiếu và Đạt chốt `global.json` (lỗi 15):

- (a) Trả về `8.0.426` / `latestFeature` như CLAUDE.md. Máy nào thiếu SDK 8 thì cài thêm.
- (b) Giữ `rollForward: latestMajor` để chạy được cả trên máy chỉ có SDK 9/10. Project vẫn build `net8.0`.

**Đã chốt (05/11/2026):** phương án **(b)**, để máy nào cũng chạy được. Lưu ý: bản của Đạt ghi
`"version": "9.0.318"` nên máy chỉ có SDK 8 sẽ **không** build được. Phải hạ phiên bản tối thiểu
về `8.0.100`, giữ `latestMajor`.

```
Đọc CLAUDE.md (mục 2, mục 9), docs/SPEC.md (R32, R33, mục 3.2.5, Bảng 3.22) và docs/PROMPTS_Module1_SuaLoi.md lỗi 12–15. Bước D3 đã xong (không còn script/style inline ở Area System).
1. CSP: middleware thêm header Content-Security-Policy cho mọi response: default-src 'self'; script-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'. Thêm X-Content-Type-Options: nosniff, Referrer-Policy: strict-origin-when-cross-origin. Trước khi bật, grep toàn repo (Views, Areas/Manage, Areas/System) tìm script/style/onclick inline và liệt kê chỗ còn vi phạm; nếu có ở Area Manage thì báo lại, không tự sửa.
2. robots.txt: không ghi cứng tên miền. Phục vụ robots.txt từ action (như sitemap) để dòng Sitemap dùng Request.Scheme/Host; Disallow /system/, /manage/, /pos/, /api/, /tai-khoan/, /dang-nhap.
3. sitemap.xml: chỉ landing page và thực đơn công khai (SPEC mục 3.2.5); bỏ /dang-ky. Trang /dang-ky, /dang-nhap thêm <meta name="robots" content="noindex">.
4. global.json: "version": "8.0.100", "rollForward": "latestMajor" (đã chốt: máy có SDK 8, 9 hoặc 10 đều build được; TargetFramework giữ net8.0). Sửa dòng ".NET 8 SDK" ở CLAUDE.md mục 2 thành ".NET SDK 8 trở lên (project build net8.0)".
5. Kiểm tra: dotnet run, mở DevTools Console ở /, /dang-ky, /dang-nhap, /system, /system/tenants, /system/plans, /manage và các trang MH08–MH13: không có lỗi "Refused to … Content Security Policy".
```

## Bước D5 – Test Module 1 (lỗi 16)

```
Đọc CLAUDE.md mục 8 và cấu trúc BTL_WEB_NC.Tests (xUnit + EF Core SQLite in-memory, làm mẫu theo BTL_WEB_NC.Tests/Manage). Tạo thư mục BTL_WEB_NC.Tests/System và BTL_WEB_NC.Tests/Account, viết test cho phần chưa có test:
1. TenantRegistrationService: tạo đúng 1 tenant Chờ kích hoạt + 1 chi nhánh mặc định + 1 TenantAdmin trong một transaction; slug dành riêng/trùng, email trùng thì không tạo bản ghi nào; gói ngừng cung cấp thì bị từ chối.
2. SystemTenantService: lọc theo từ khóa/trạng thái/gói, phân trang; UpdateStatusAsync và ChangePlanAsync (những trường hợp chưa có ở D2).
3. SystemPlanService: trùng tên gói (không phân biệt hoa thường) bị từ chối; ToggleActiveAsync.
4. Phân quyền: TenantAdmin gọi /system/* bị 403; Staff gọi /system/* bị 403.
Chạy dotnet test, báo số test qua/không qua.
```
