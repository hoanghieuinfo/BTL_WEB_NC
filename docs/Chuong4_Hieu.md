<!-- [AI] Claude hỗ trợ – Phụ lục Z.7 -->
<!-- Ghi chú khi ghép báo cáo: số mục "4.x" và số Hình/Bảng đang đánh từ 4.1 trong phần của Hiếu;
     nhóm đánh lại một lượt khi ghép phần của Đạt và Trung. Các khung [Chèn ảnh …] thay bằng ảnh chụp ứng dụng thật. -->

## 4.x. Chức năng Quản lý thực đơn (Hoàng Minh Hiếu)

Chức năng Quản lý thực đơn là màn hình MH10 (`/manage/products`) của Module 2, dành cho chủ quán (TenantAdmin). Màn hình thực hiện use case UC10 (Bảng 3.4) và các yêu cầu sau:

- R13: quản lý danh mục món.
- R14: thêm, sửa, xóa, ẩn món, tải ảnh món, thao tác bằng AJAX.
- R15: khai báo công thức định lượng.
- R21: tìm kiếm, lọc, phân trang.

Dữ liệu nhập theo biểu mẫu BM04: thông tin món (BM04a) và công thức định lượng (BM04b). Món ở trạng thái "Đang bán" được hiển thị trên trang thực đơn công khai của chi nhánh (P09). Định lượng là đầu vào cho việc trừ kho tự động khi thanh toán (P07).

### 4.x.1. Giao diện

Giao diện được dựng bằng Razor View và CSS tự viết theo style guide Figma (mục 3.2.5), mobile-first với ba mốc 360px, 768px và 1280px (R31). Mọi màn hình đều có trạng thái đang tải, danh sách rỗng và lỗi như các frame thiết kế.

[Chèn ảnh chụp MH10 ở độ rộng 1280px]

**Hình 4.1. Màn hình Quản lý thực đơn trên máy tính (1280px)**

Ở 1280px trang chia hai cột:

- **Cột trái:** danh sách danh mục, mỗi dòng có số món và menu sửa/xóa danh mục.
- **Cột phải:**
  - khối "Cảnh báo trừ kho", liệt kê số món chưa khai báo định lượng (bán các món này sẽ không trừ kho, R15);
  - thanh lọc gồm ô tìm kiếm, trạng thái Đang bán/Ngừng bán, công tắc "Chưa định lượng";
  - bảng món: ảnh, tên, danh mục, giá, trạng thái, các nút Sửa, Định lượng, Xóa;
  - phân trang 6 món mỗi trang.

[Chèn ảnh chụp MH10 ở độ rộng 768px]

**Hình 4.2. Màn hình Quản lý thực đơn trên máy tính bảng (768px)**

Ở 768px:

- Menu bên thu thành nút menu trên thanh trên cùng.
- Danh mục chuyển thành dãy chip vuốt ngang phía trên danh sách.
- Bảng món vẫn giữ dạng bảng.

[Chèn ảnh chụp MH10 ở độ rộng 360px]

**Hình 4.3. Màn hình Quản lý thực đơn trên điện thoại (360px)**

Ở 360px:

- Bảng món chuyển thành thẻ: tên, giá, trạng thái và các nút thao tác trên cùng một thẻ.
- Các nút chính trải hết chiều ngang.
- Mọi vùng chạm tối thiểu 44×44px.

[Chèn ảnh chụp hộp thoại Thêm món khi nhập sai]

**Hình 4.4. Hộp thoại Thêm món báo lỗi tại từng trường**

Hộp thoại Thêm món theo BM04a gồm: danh mục, tên món, giá bán, ảnh món, mô tả, trạng thái. Lỗi phát hiện ở trình duyệt hoặc do server trả về đều hiện ngay dưới ô tương ứng, ví dụ:

- tên trùng trong quán (luồng 6a);
- ảnh sai định dạng hoặc quá 2 MB (luồng 6b).

[Chèn ảnh chụp hộp thoại Định lượng]

**Hình 4.5. Hộp thoại khai báo định lượng món**

Hộp thoại Định lượng theo BM04b: mỗi dòng gồm nguyên liệu của quán, lượng cho một phần món và đơn vị cơ sở. Có thể thêm hoặc xóa dòng; một nguyên liệu không được lặp lại trong cùng món.

### 4.x.2. Mã HTML, CSS và Client Script

**a) Hộp thoại Thêm món (HTML)**

Hộp thoại dùng thẻ `<dialog>` chuẩn HTML5, form có `novalidate` để JavaScript tự kiểm tra và hiện lỗi theo kiểu thống nhất. Mỗi ô có một phần tử `data-error-for` để nhận lỗi theo tên trường:

```html
<dialog class="modal modal-lg" id="product-modal" aria-labelledby="product-modal-title">
  <form id="product-form" class="modal-form" novalidate>
    <h2 class="modal-title" id="product-modal-title">Thêm món mới</h2>
    <select class="select" id="product-category" name="categoryId" required data-category-select>
      <option value="">Chọn danh mục</option>
      @foreach (var c in Model.Categories) { <option value="@c.Id">@c.Name</option> }
    </select>
    <span class="field-error" data-error-for="categoryId"></span>
    <input class="input" id="product-name" name="name" maxlength="100" required autocomplete="off" />
    <span class="field-error" data-error-for="name"></span>
    <input type="file" class="visually-hidden" name="image"
           accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp" />
    <span class="field-error field-error-box" data-error-for="image"></span>
    <button type="submit" class="btn btn-primary" data-submit>Lưu món</button>
  </form>
</dialog>
```

Tên danh mục (`@c.Name`) được Razor mã hóa HTML khi xuất ra. Vì vậy dữ liệu do người dùng nhập không thể chèn thẻ `<script>` vào trang.

**b) Responsive bằng media query (CSS)**

CSS viết mobile-first: mặc định cho điện thoại, mỗi mốc `min-width` chỉ ghi phần thay đổi. Ở điện thoại, bảng món hiển thị dạng thẻ bằng CSS Grid. Từ 768px trở lên, bảng trở lại dạng `table`:

```css
.product-table, .product-table tbody { display: block; }
.product-table thead { display: none; }
.product-row { display: grid; grid-template-columns: 1fr auto; gap: 8px 12px; }

@media (min-width: 768px) {
  .product-table { display: table; }
  .product-table thead { display: table-header-group; }
  .product-row { display: table-row; padding: 0; }
  .form-grid { grid-template-columns: 1fr 1fr; }
}

@media (min-width: 1280px) {
  .menu-layout { display: grid; grid-template-columns: 228px 1fr; align-items: start; gap: 20px; }
}
```

**c) Hàm AJAX dùng chung (ajax.js)**

Mọi lời gọi AJAX của Area Manage đi qua `wwwroot/js/ajax.js`:

- Với POST, hàm luôn gửi anti-forgery token (lấy từ thẻ meta của layout) trong header `RequestVerificationToken`.
- Hàm chuẩn hóa xử lý mã trạng thái (mục 3.2.6):
  - 401 thì báo hết phiên rồi chuyển sang trang đăng nhập;
  - 400 thì trả `data.errors` để hiện lỗi tại từng ô;
  - các mã khác hiện thông báo.

```javascript
async function send(url, init, options) {
  init.credentials = "same-origin";
  init.headers = Object.assign({ "X-Requested-With": "XMLHttpRequest" }, init.headers || {});
  if (init.method === "POST") init.headers["RequestVerificationToken"] = token();

  var response = await fetch(url, init);
  var body = await readBody(response);
  if (response.ok) return body;
  return fail(response.status, body, options); // 400 → data.errors; 401 → đăng nhập lại; 403/404/409/429 → toast
}
```

**d) Chèn dữ liệu bằng textContent**

Danh sách món (A06) là Partial View do server render, nên các chuỗi trong đó đã được Razor mã hóa. Mọi dữ liệu khác mà JavaScript tự đưa vào trang (ví dụ cập nhật lại ô chọn danh mục sau khi thêm danh mục) đều tạo phần tử DOM và gán `textContent`, không ghép chuỗi HTML:

```javascript
next.categories.forEach(function (c) {
  var option = document.createElement("option");
  option.value = c.id;
  option.textContent = c.name;   // không dùng innerHTML với dữ liệu người dùng
  select.appendChild(option);
});
```

Khi lưu món, form được gửi bằng `FormData` (multipart, vì có ảnh). Lỗi 400 từ server được hiện đúng ô qua `Ajax.showErrors`:

```javascript
var data = new FormData();
data.append("categoryId", f.categoryId.value);
data.append("name", f.name.value.trim());
data.append("price", f.price.value.replace(/\D/g, ""));
if (imageInput.files[0]) data.append("image", imageInput.files[0]);
try {
  var result = await Ajax.postForm(editingId ? URLS.update(editingId) : URLS.create, data);
} catch (err) {
  if (err.status === 400 && err.data && err.data.errors) Ajax.showErrors(productForm, err.data.errors);
}
```

### 4.x.3. Mã Server

**a) Controller**

Controller chỉ nhận request, kiểm tra ModelState, gọi service và trả kết quả. Nghiệp vụ nằm trong `ProductService`. Lớp cha `ManageControllerBase` mang `[Area("Manage")]` và `[Authorize(Roles = "TenantAdmin")]`. Bộ lọc `ManageExceptionFilter` đổi lỗi nghiệp vụ thành 404/400:

```csharp
[HttpPost("create")]
[RequestSizeLimit(MaxRequestBytes)]
public async Task<IActionResult> Create([FromForm] ProductFormModel model, CancellationToken ct)
{
    if (!ModelState.IsValid)
        return BadRequest(ApiResult.ValidationFailed(ModelState));   // 400, data.errors theo tên trường

    var id = await _products.CreateAsync(model, ct);
    return Ok(ApiResult.Ok(new { id, summary = await _categories.GetSummaryAsync(ct) }, "Đã thêm món."));
}
```

**b) Service thêm món và kiểm tra CategoryId**

`ProductFormModel` không có trường TenantId; TenantId do AppDbContext tự gán khi lưu. CategoryId nhận từ request được tra lại qua DbSet đã lọc tenant trước khi gán (mục 3.2.2e). Danh mục của quán khác coi như không tồn tại, trả 404 (luồng 6c):

```csharp
public async Task<int> CreateAsync(ProductFormModel model, CancellationToken ct = default)
{
    await EnsureCategoryAsync(model.CategoryId!.Value, ct);   // 6c: danh mục quán khác → 404
    var name = model.Name.Trim();
    await EnsureUniqueNameAsync(name, exceptId: null, ct);     // 6a: trùng tên trong quán → lỗi tại ô tên

    var imagePath = await SaveImageAsync(model.Image, ct);     // 6b: ảnh không hợp lệ → lỗi tại ô ảnh
    var product = new Product { CategoryId = model.CategoryId.Value, Name = name, Price = model.Price!.Value,
                                IsAvailable = model.IsAvailable, ImagePath = imagePath /* … */ };   // không gán TenantId
    _db.Products.Add(product);
    await SaveAsync(name, exceptId: null, ct);
    return product.Id;
}

private async Task EnsureCategoryAsync(int categoryId, CancellationToken ct)
{
    if (!await _db.Categories.AnyAsync(c => c.Id == categoryId, ct))   // _db.Categories đã lọc theo tenant
        throw new NotFoundException("Không tìm thấy danh mục.");
}
```

**c) Global Query Filter trong AppDbContext**

Khi dựng model, AppDbContext đăng ký bộ lọc cho mọi entity cài `ITenantEntity`. Bộ lọc tham chiếu thuộc tính `CurrentTenantId` của chính DbContext, nên được tính lại ở mỗi truy vấn. Entity có xóa mềm (Products, Categories, Ingredients) gộp điều kiện TenantId và `!IsDeleted` trong cùng một biểu thức:

```csharp
if (typeof(ISoftDelete).IsAssignableFrom(typeof(T)))
    modelBuilder.Entity<T>().HasQueryFilter(
        e => e.TenantId == CurrentTenantId && !((ISoftDelete)e).IsDeleted);
else
    modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
```

Ở chiều ghi, trước mỗi lần lưu `ApplyTenantRules()` chạy ba kiểm tra:

- gán TenantId hiện tại cho bản ghi mới chưa có tenant;
- từ chối khi không xác định được tenant;
- từ chối khi bản ghi cũ bị đổi TenantId hoặc thuộc tenant khác.

**d) Kiểm tra file ảnh tải lên**

`ImageUploadService` chỉ nhận JPG/PNG/WEBP tối đa 2 MB. Dịch vụ kiểm tra **cả** phần mở rộng **và** chữ ký đầu file (magic bytes), rồi lưu với tên GUID trong thư mục riêng của tenant (`wwwroot/uploads/{tenantId}/`):

```csharp
if (file.Length > MaxBytes) return new(null, $"Ảnh nặng {mb} MB, vượt giới hạn 2 MB. Vui lòng chọn ảnh nhỏ hơn.");
var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
    return new(null, "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");
if (read < header.Length || !SignatureMatches(extension, header))   // FF D8 FF, 89 50 4E 47…, RIFF…WEBP
    return new(null, "Nội dung file không phải ảnh hợp lệ.");
var fileName = $"{Guid.NewGuid():N}{(extension == ".jpeg" ? ".jpg" : extension)}";
```

### 4.x.4. Đánh giá

**a) Responsive**

**Bảng 4.1. Bố cục MH10 theo kích thước màn hình**

| Độ rộng | Danh mục | Danh sách món | Hộp thoại |
|---|---|---|---|
| 360px | Dãy chip vuốt ngang | Thẻ (CSS Grid), nút trải hết chiều ngang | Một cột |
| 768px | Dãy chip nhiều hàng | Bảng 4 cột | Danh mục và tên món trên một hàng |
| 1280px | Cột trái cố định 228px | Bảng đầy đủ bên phải | Như 768px |

Chỉ dùng Flexbox và Grid, không dùng thư viện CSS. Vùng chạm nhỏ (nút `btn-sm`, nút phân trang) được nới tới 44×44px bằng phần tử giả `::after`. Bố cục được đối chiếu với ba frame thiết kế MH10 (Mobile 360px, Tablet 768px và màn hình chính).

**b) SEO**

- **Trang quản trị không được lập chỉ mục (R33).** `_ManageLayout` luôn có thẻ `<meta name="robots" content="noindex, nofollow">`. Lý do: trang quản trị chỉ dành cho chủ quán đã đăng nhập và chứa dữ liệu nội bộ (định lượng, tồn kho, nhân viên, doanh thu), không có giá trị tìm kiếm. Cho máy tìm kiếm thu thập chỉ lộ đường dẫn nội bộ và lãng phí ngân sách thu thập (crawl budget) của trang công khai.
- **Liên hệ với trang thực đơn công khai.** Dữ liệu nhập ở MH10 chính là nội dung SEO của trang thực đơn công khai `/{slug-quan}/thuc-don/{slug-chi-nhanh}` (P09, R32, Module 3): chỉ món "Đang bán" của quán được hiển thị ở đó. Vì vậy MH10 giới hạn:
  - tên món tối đa 100 ký tự và không trùng trong quán;
  - mô tả tối đa 500 ký tự;
  - ảnh nén gọn (≤ 2 MB, JPG/PNG/WEBP).

  Các ràng buộc này giúp trang công khai có tiêu đề món rõ ràng, có thể dùng tên món làm thuộc tính alt của ảnh, và tải nhanh.
- **Hạn chế còn lại.** `robots.txt` chặn `/manage` (R33) thuộc phần khung chung của Module 1 và chưa có trong repo tại thời điểm viết. Hiện tại trang quản trị được bảo vệ bằng thẻ meta noindex và yêu cầu đăng nhập.

**c) AJAX**

**Bảng 4.2. Endpoint AJAX của chức năng Quản lý thực đơn**

| Mã | Phương thức, đường dẫn | Mục đích | Kết quả |
|---|---|---|---|
| A06 | GET /manage/products/list | Lọc danh mục, trạng thái, chưa định lượng, tìm kiếm, phân trang 6 món/trang | Partial View |
| A07 | POST /manage/products/create, /{id}/update, /{id}/delete | Thêm, sửa (multipart, có ảnh), xóa món (đã bán thì xóa mềm) | JSON |
| A08 | POST /manage/products/{id}/recipe | Lưu định lượng (thay toàn bộ danh sách dòng) | JSON |
| A20 | POST /manage/categories/create, /{id}/update, /{id}/delete | Thêm, sửa, xóa mềm danh mục | JSON |
| A21 | POST /manage/products/{id}/availability | Bật/tắt Đang bán ngay trên dòng món | JSON |
| A22 | GET /manage/products/{id}/recipe | Dữ liệu cho hộp thoại định lượng | JSON |

A20–A22 là endpoint bổ sung (xem tài liệu EndpointBoSung). Các mã trạng thái được dùng:

- **200:** thành công, `{ success: true, message, data }`.
- **400:** dữ liệu không hợp lệ, kèm `data.errors` theo tên trường (ví dụ `name`, `image`, `items[0].quantity`).
- **401:** hết phiên.
- **403:** không đủ quyền.
- **404:** không tìm thấy, kể cả bản ghi của tenant khác.

Ở trình duyệt, hàm dùng chung xử lý mỗi mã một cách thống nhất (mục 4.x.2c).

**d) An toàn thông tin**

- **CSRF:**
  - Filter `AutoValidateAntiforgeryToken` được bật toàn cục.
  - Token đặt trong thẻ meta của layout và được `ajax.js` gửi trong header của mọi POST.
- **XSS:**
  - Razor mã hóa mọi dữ liệu xuất ra, không dùng `Html.Raw`.
  - JavaScript chỉ chèn Partial View do server render; dữ liệu khác gán bằng `textContent`.
- **SQL Injection:** chỉ dùng LINQ của EF Core, truy vấn được tham số hóa.
- **Upload:** kiểm tra phần mở rộng, magic bytes, dung lượng; đổi tên bằng GUID; lưu theo thư mục tenant.
- **Cô lập tenant (R38):**
  - Global Query Filter khi đọc;
  - kiểm tra TenantId khi ghi;
  - tra lại CategoryId và IngredientId trước khi gán.

Bảng 4.3 tổng hợp kết quả kiểm thử thật:

- **Kiểm thử tự động** (xUnit + EF Core SQLite in-memory): toàn bộ 167 ca của dự án đều đạt khi viết tài liệu này. Bảng chỉ trích các ca liên quan MH10.
- **Thử thủ công:** gọi trực tiếp ứng dụng đang chạy (SQL Server) bằng curl ngày 21/10/2026.

**Bảng 4.3. Kết quả kiểm thử an toàn thông tin của chức năng Quản lý thực đơn**

| STT | Nhóm | Ca kiểm thử | Cách kiểm thử | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|---|
| 1 | CSRF | POST /manage/products/create không có token | Thủ công (curl) | 400, không tạo món | Đạt (400) |
| 2 | CSRF | POST /manage/products/create với token sai | Thủ công (curl) | 400, không tạo món | Đạt (400) |
| 3 | XSS | Thêm món tên `<script>alert(1)</script>` rồi tải A06 | Thủ công (curl) | Tên hiển thị dạng `&lt;script&gt;…`, không có thẻ script thô | Đạt |
| 4 | Upload | File `.jpg` nhưng nội dung không phải ảnh | `ImageUploadServiceTests.FileJpgNhungNoiDungKhongPhaiAnh_BiTuChoi` | Bị từ chối | Đạt |
| 5 | Upload | File PNG thật nhưng đặt đuôi `.jpg` | `ImageUploadServiceTests.PngThatNhungDatDuoiJpg_BiTuChoi` | Bị từ chối | Đạt |
| 6 | Upload | Đuôi `.gif`; file quá 2 MB | `ImageUploadServiceTests.DuoiGif_BiTuChoi`, `QuaHaiMB_BiTuChoi` | Bị từ chối | Đạt |
| 7 | Upload | Ảnh JPG/PNG/WEBP hợp lệ | `ImageUploadServiceTests.AnhHopLe_LuuTenGuidTrongThuMucTenant` (4 ca) | Lưu tên GUID trong `uploads/{tenantId}/` | Đạt |
| 8 | Upload | Thêm món kèm ảnh không hợp lệ | `ProductServiceTests.A07_Them_AnhKhongHopLe_LoiTaiOAnh_KhongTaoMon` | Lỗi tại ô ảnh, không tạo món | Đạt |
| 9 | Cô lập tenant | Danh sách A06 của quán A | `ProductServiceTests.A06_ChiTraMonCuaTenantHienTai` | Không có món quán B | Đạt |
| 10 | Cô lập tenant | Lọc A06 theo danh mục của quán B | `ProductServiceTests.A06_LocDanhMucCuaTenantKhac_KhongTraGi` | Danh sách rỗng | Đạt |
| 11 | Cô lập tenant | Thêm món với CategoryId của quán B | `ProductServiceTests.A07_Them_VoiCategoryIdCuaTenantKhac_Tra404_KhongTaoMon` | 404, không tạo món | Đạt |
| 12 | Cô lập tenant | Sửa món sang danh mục của quán B | `ProductServiceTests.A07_Sua_ChuyenSangDanhMucCuaTenantKhac_Tra404` | 404 | Đạt |
| 13 | Cô lập tenant | Sửa, xóa, bật/tắt món bằng ID của quán B | `ProductServiceTests.A07_SuaXoaBatTat_IdCuaTenantKhac_Tra404_DuLieuBKhongDoi` | 404, dữ liệu quán B không đổi | Đạt |
| 14 | Cô lập tenant | Lưu định lượng với nguyên liệu của quán B | `RecipeTests.Luu_NguyenLieuCuaTenantKhac_Tra404_DinhLuongCuGiuNguyen` | 404, định lượng cũ giữ nguyên | Đạt |
| 15 | Cô lập tenant | Sửa, xóa danh mục của quán B | `CategoryServiceTests.SuaXoa_DanhMucCuaTenantKhac_Tra404` | 404 | Đạt |
| 16 | Cô lập tenant | Đọc dữ liệu bằng ID của quán B; khi không có tenant | `TenantFilterTests.TenantA_TraIdCuaB_KhongThay`, `KhongCoTenant_KhongThayGi` | Không thấy bản ghi nào | Đạt |
| 17 | Cô lập tenant | Ghi bản ghi mang TenantId khác, đổi TenantId khi sửa | `TenantSaveRulesTests` (6 ca) | SaveChanges bị từ chối | Đạt |
| 18 | AJAX | Request AJAX chưa đăng nhập; không đủ quyền | `AjaxInfrastructureTests.ChuaDangNhap_Ajax401_RequestThuong302`, `KhongDuQuyen_Ajax403` | 401/403 thay vì chuyển trang 302 | Đạt |
| 19 | SEO | Trang /manage/products có thẻ noindex | Thủ công (curl) | Có `<meta name="robots" content="noindex, nofollow">` | Đạt |

Ngoài các ca an toàn thông tin, các ca nghiệp vụ của UC10 cũng đạt:

- trùng tên trong quán → lỗi tại ô tên (6a); trùng tên với món của quán khác → được phép;
- món đã bán chỉ xóa mềm (R42); món chưa bán xóa hẳn cùng định lượng;
- phân trang 6 món mỗi trang;
- định lượng không dương hoặc trùng nguyên liệu → lỗi tại dòng.
