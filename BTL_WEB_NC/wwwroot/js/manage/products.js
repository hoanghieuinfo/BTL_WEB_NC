// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH10 – Thực đơn & định lượng. Mọi thao tác qua ajax.js (POST tự gửi anti-forgery token).
// Quy tắc XSS: chỉ chèn innerHTML với Partial View A06 do server render (Razor đã mã hóa);
// mọi dữ liệu khác điền bằng textContent / value / tạo phần tử DOM.
(function () {
  "use strict";

  var URLS = {
    list: "/manage/products/list",
    create: "/manage/products/create",
    update: function (id) { return "/manage/products/" + id + "/update"; },
    remove: function (id) { return "/manage/products/" + id + "/delete"; },
    availability: function (id) { return "/manage/products/" + id + "/availability"; },
    recipe: function (id) { return "/manage/products/" + id + "/recipe"; },
    categoryCreate: "/manage/categories/create",
    categoryUpdate: function (id) { return "/manage/categories/" + id + "/update"; },
    categoryDelete: function (id) { return "/manage/categories/" + id + "/delete"; }
  };
  var MAX_IMAGE_BYTES = 2 * 1024 * 1024;
  var IMAGE_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];

  var state = { categoryId: "", status: "", q: "", noRecipe: false, page: 1 };
  var listEl = document.querySelector("[data-product-list]");
  var filterForm = document.querySelector("[data-filter-form]");
  var categoryList = document.querySelector("[data-category-list]");
  var requestSeq = 0;

  function $(selector, root) { return (root || document).querySelector(selector); }
  function $all(selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); }
  function formatMoney(digits) { return digits ? Number(digits).toLocaleString("vi-VN") : ""; }
  function formatSize(bytes) { return (bytes / 1024 / 1024).toLocaleString("vi-VN", { maximumFractionDigits: 1 }) + " MB"; }

  // ================================================================ Danh sách (A06)

  function readStateFromUrl() {
    var params = new URLSearchParams(location.search);
    state.categoryId = params.get("categoryId") || "";
    state.status = params.get("status") || "";
    state.q = params.get("q") || "";
    state.noRecipe = params.get("noRecipe") === "true";
    state.page = parseInt(params.get("page"), 10) || 1;

    filterForm.elements.q.value = state.q;
    filterForm.elements.status.value = state.status;
    filterForm.elements.noRecipe.checked = state.noRecipe;
    markActiveCategory();
  }

  function queryParams() {
    return {
      categoryId: state.categoryId,
      status: state.status,
      q: state.q.trim(),
      noRecipe: state.noRecipe ? "true" : "",
      page: state.page > 1 ? state.page : ""
    };
  }

  /** Giữ bộ lọc trên URL để F5 hoặc gửi link vẫn đúng màn hình đang xem. */
  function writeStateToUrl() {
    var params = new URLSearchParams();
    var q = queryParams();
    Object.keys(q).forEach(function (k) { if (q[k]) params.set(k, q[k]); });
    var text = params.toString();
    history.replaceState(null, "", location.pathname + (text ? "?" + text : ""));
  }

  function showLoading() {
    listEl.setAttribute("aria-busy", "true");
    listEl.replaceChildren(document.getElementById("product-loading-template").content.cloneNode(true));
  }

  function showListError(message) {
    var box = document.createElement("div");
    box.className = "state state-error";
    var title = document.createElement("p");
    title.className = "state-title";
    title.textContent = "Không tải được danh sách món";
    var detail = document.createElement("p");
    detail.textContent = message;
    var retry = document.createElement("button");
    retry.type = "button";
    retry.className = "btn";
    retry.dataset.action = "retry";
    retry.textContent = "Thử lại";
    box.append(title, detail, retry);
    listEl.replaceChildren(box);
    listEl.setAttribute("aria-busy", "false");
  }

  async function loadList(options) {
    options = options || {};
    var seq = ++requestSeq;
    writeStateToUrl();
    showLoading();
    try {
      var html = await Ajax.getHtml(URLS.list, queryParams(), { silent: true });
      if (seq !== requestSeq) return; // đã có yêu cầu mới hơn (gõ tìm kiếm liên tục)
      listEl.innerHTML = html; // Partial View do server render – Razor đã mã hóa dữ liệu
      listEl.setAttribute("aria-busy", "false");

      var meta = $("[data-list-meta]", listEl);
      if (meta && meta.dataset.page) state.page = parseInt(meta.dataset.page, 10); // server có thể kẹp lại số trang
      writeStateToUrl();

      if (options.highlightId) {
        var row = $('tr[data-id="' + options.highlightId + '"]', listEl);
        if (row) {
          row.classList.add("is-flash");
          row.scrollIntoView({ block: "nearest", behavior: "smooth" });
        }
      }
    } catch (err) {
      if (seq !== requestSeq || err.status === 401) return;
      showListError(err.message);
    }
  }

  function setFilter(changes) {
    Object.assign(state, changes, { page: changes.page || 1 });
    loadList();
  }

  // Enter trong ô tìm kiếm không gửi form (thay cho onsubmit inline – CSP của Module 1 chặn JS inline).
  filterForm.addEventListener("submit", function (event) { event.preventDefault(); });

  var searchTimer = null;
  filterForm.elements.q.addEventListener("input", function () {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(function () { setFilter({ q: filterForm.elements.q.value }); }, 300);
  });
  filterForm.elements.status.addEventListener("change", function () { setFilter({ status: filterForm.elements.status.value }); });
  filterForm.elements.noRecipe.addEventListener("change", function () { setFilter({ noRecipe: filterForm.elements.noRecipe.checked }); });

  function markActiveCategory() {
    $all(".category-item", categoryList).forEach(function (btn) {
      var active = (btn.dataset.categoryId || "") === state.categoryId;
      btn.classList.toggle("is-active", active);
      btn.setAttribute("aria-pressed", active ? "true" : "false");
    });
  }

  categoryList.addEventListener("click", function (event) {
    var item = event.target.closest(".category-item");
    if (item) {
      state.categoryId = item.dataset.categoryId || "";
      markActiveCategory();
      setFilter({});
      return;
    }
    var action = event.target.closest("[data-action]");
    if (!action) return;
    var row = action.closest("[data-category-row]");
    action.closest("details").open = false;
    if (action.dataset.action === "edit-category") openCategoryForm(row);
    if (action.dataset.action === "delete-category") deleteCategory(row);
  });

  function clearFilters() {
    filterForm.elements.q.value = "";
    filterForm.elements.status.value = "";
    filterForm.elements.noRecipe.checked = false;
    state.categoryId = "";
    markActiveCategory();
    setFilter({ q: "", status: "", noRecipe: false });
  }

  // Nút có data-action ở mọi nơi trên trang (đầu trang, trạng thái rỗng, khối cảnh báo, từng dòng món).
  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action], [data-page]");
    if (!target || target.disabled) return;

    if (target.dataset.page && listEl.contains(target)) {
      setFilter({ page: parseInt(target.dataset.page, 10) });
      return;
    }

    var row = target.closest("tr[data-id]");
    switch (target.dataset.action) {
      case "add-product": openProductForm(null); break;
      case "add-category": openCategoryForm(null); break;
      case "show-missing":
        filterForm.elements.noRecipe.checked = true;
        setFilter({ noRecipe: true });
        break;
      case "clear-filters": clearFilters(); break;
      case "retry": loadList(); break;
      case "edit": openProductForm(row); break;
      case "recipe": openRecipe(row); break;
      case "delete": deleteProduct(row); break;
      case "add-recipe-line": addRecipeLine(null); break;
      case "remove-recipe-line": removeRecipeLine(target.closest("[data-recipe-line]")); break;
    }
  });

  // ================================================================ Bộ đếm (data.summary)

  var summary = readInitialSummary();

  function readInitialSummary() {
    return {
      categories: $all("[data-category-row]", categoryList).map(function (li) {
        return {
          id: parseInt(li.dataset.categoryRow, 10),
          name: $('[data-field="name"]', li).textContent,
          sortOrder: parseInt(li.dataset.sort, 10) || 0,
          productCount: parseInt($('[data-field="count"]', li).textContent, 10) || 0
        };
      })
    };
  }

  function applySummary(next) {
    summary = next;
    $all('[data-summary="total"]').forEach(function (el) { el.textContent = next.totalProducts; });
    $all('[data-summary="categoryCount"]').forEach(function (el) { el.textContent = next.categoryCount; });
    $all('[data-summary="missing"]').forEach(function (el) { el.textContent = next.missingRecipeCount; });
    $("[data-recipe-warning]").hidden = next.missingRecipeCount === 0;

    // Dựng lại danh sách danh mục từ <template> (giữ dòng "Tất cả" đầu tiên).
    var template = document.getElementById("category-row-template");
    $all("[data-category-row]", categoryList).forEach(function (li) { li.remove(); });
    next.categories.forEach(function (c) {
      var li = template.content.firstElementChild.cloneNode(true);
      li.dataset.categoryRow = c.id;
      li.dataset.sort = c.sortOrder;
      $(".category-item", li).dataset.categoryId = c.id;
      $('[data-field="name"]', li).textContent = c.name;
      $('[data-field="count"]', li).textContent = c.productCount;
      $('[data-field="menu-label"]', li).setAttribute("aria-label", "Tùy chọn danh mục " + c.name);
      categoryList.appendChild(li);
    });
    var empty = $("[data-category-empty]");
    if (empty) empty.hidden = next.categories.length > 0;

    if (state.categoryId && !next.categories.some(function (c) { return String(c.id) === state.categoryId; })) {
      state.categoryId = ""; // danh mục đang lọc vừa bị xóa
    }
    markActiveCategory();

    // Ô chọn danh mục trong form món.
    var select = $("[data-category-select]");
    var current = select.value;
    $all("option", select).slice(1).forEach(function (o) { o.remove(); });
    next.categories.forEach(function (c) {
      var option = document.createElement("option");
      option.value = c.id;
      option.textContent = c.name;
      select.appendChild(option);
    });
    select.value = current;

    $all('.page-head [data-action="add-product"]').forEach(function (btn) {
      btn.disabled = next.categories.length === 0;
    });
  }

  // ================================================================ Hộp thoại dùng chung (wwwroot/js/manage/ui.js)

  var setBusy = ManageUi.setBusy;
  var confirmAction = ManageUi.confirmAction;

  // ================================================================ Thêm / sửa món (A07)

  var productModal = document.getElementById("product-modal");
  var productForm = document.getElementById("product-form");
  var productSubmit = $("[data-submit]", productForm);
  var imageInput = $("[data-image-input]", productForm);
  var editingId = null;
  var touched = {};
  var previewUrl = null;

  function openProductForm(row) {
    editingId = row ? row.dataset.id : null;
    touched = {};
    productForm.reset();
    Ajax.clearErrors(productForm);
    productModal.classList.remove("has-errors");

    $("#product-modal-title").textContent = row ? "Sửa món" : "Thêm món mới";
    $("[data-modal-icon]", productModal).textContent = row ? "✎" : "+";
    $("[data-modal-subtitle]", productModal).textContent = row
      ? "Cập nhật thông tin món. Hóa đơn cũ giữ nguyên tên và giá lúc bán."
      : "Khai báo thông tin món bán. Định lượng nguyên liệu khai báo sau ở nút \"Định lượng\".";
    productSubmit.textContent = "Lưu món";

    var f = productForm.elements;
    if (row) {
      f.categoryId.value = row.dataset.categoryId;
      f.name.value = row.dataset.name;
      f.price.value = formatMoney(row.dataset.price);
      f.description.value = row.dataset.description || "";
      f.isAvailable.checked = row.dataset.available === "true";
    } else {
      f.categoryId.value = state.categoryId || "";
      f.isAvailable.checked = true;
    }
    showImage(row && row.dataset.image ? { src: row.dataset.image, name: "Ảnh hiện tại", size: "" } : null);
    updateDescriptionCount();
    validateProduct();
    productModal.showModal();
    (row ? f.name : f.categoryId).focus();
  }

  function showImage(image) {
    if (previewUrl) { URL.revokeObjectURL(previewUrl); previewUrl = null; }
    var preview = $("[data-image-preview]", productForm);
    preview.hidden = !image;
    $("[data-image-placeholder]", productForm).hidden = !!image;
    if (image) preview.src = image.src; else preview.removeAttribute("src");
    $("[data-image-name]", productForm).textContent = image ? image.name : "Chưa chọn ảnh";
    $("[data-image-size]", productForm).textContent = image ? image.size : "";
    $("[data-image-button-text]", productForm).textContent = image ? "Đổi ảnh" : "Chọn ảnh";
  }

  /** Kiểm tra phía client theo BM04a (server kiểm tra lại toàn bộ). Trả về { tenTruong: thông báo }. */
  function productErrors() {
    var f = productForm.elements;
    var errors = {};
    if (!f.categoryId.value) errors.categoryId = "Vui lòng chọn danh mục.";
    var name = f.name.value.trim();
    if (!name) errors.name = "Vui lòng nhập tên món.";
    else if (name.length > 100) errors.name = "Tên món tối đa 100 ký tự.";
    var digits = f.price.value.replace(/\D/g, "");
    if (!digits || Number(digits) <= 0) errors.price = "Giá bán phải là số nguyên lớn hơn 0.";
    else if (Number(digits) > 100000000) errors.price = "Giá bán không quá 100.000.000 đ.";
    if (f.description.value.length > 500) errors.description = "Mô tả tối đa 500 ký tự.";

    var file = imageInput.files[0];
    if (file) {
      var ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
      if (IMAGE_EXTENSIONS.indexOf(ext) < 0) errors.image = "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.";
      else if (file.size > MAX_IMAGE_BYTES) errors.image = "Ảnh nặng " + formatSize(file.size) + ", vượt giới hạn 2 MB. Vui lòng chọn ảnh nhỏ hơn.";
    }
    return errors;
  }

  /** Hiện lỗi của các ô đã chạm; khóa nút Lưu tới khi hợp lệ (mô tả MH10). */
  function validateProduct(showAll) {
    var errors = productErrors();
    var visible = {};
    Object.keys(errors).forEach(function (key) { if (showAll || touched[key]) visible[key] = errors[key]; });
    Ajax.showErrors(productForm, visible, { focus: false });

    var count = Object.keys(visible).length;
    productModal.classList.toggle("has-errors", count > 0);
    $("[data-form-status]", productForm).textContent = count > 0 ? "Phát hiện " + count + " mục cần điều chỉnh." : "";
    productSubmit.disabled = Object.keys(errors).length > 0;
    return errors;
  }

  productForm.addEventListener("input", function (event) {
    var field = event.target.name;
    if (!field) return;
    if (field === "price") {
      var digits = event.target.value.replace(/\D/g, "").slice(0, 9);
      event.target.value = formatMoney(digits);
    }
    if (field === "description") updateDescriptionCount();
    touched[field] = true;
    validateProduct();
  });
  productForm.addEventListener("focusout", function (event) {
    if (event.target.name) { touched[event.target.name] = true; validateProduct(); }
  });

  imageInput.addEventListener("change", function () {
    var file = imageInput.files[0];
    touched.image = true;
    if (file) {
      var errors = productErrors();
      if (!errors.image) {
        var url = URL.createObjectURL(file);
        showImage({ src: url, name: file.name, size: formatSize(file.size) }); // thu hồi URL xem trước cũ
        previewUrl = url;
      } else {
        showImage(null);
        $("[data-image-name]", productForm).textContent = file.name;
        $("[data-image-size]", productForm).textContent = formatSize(file.size) + " • Không hợp lệ";
      }
    }
    validateProduct();
  });

  function updateDescriptionCount() {
    $("[data-description-count]", productForm).textContent = productForm.elements.description.value.length;
  }

  productForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    if (Object.keys(validateProduct(true)).length > 0) return;

    var f = productForm.elements;
    var data = new FormData();
    data.append("categoryId", f.categoryId.value);
    data.append("name", f.name.value.trim());
    data.append("price", f.price.value.replace(/\D/g, ""));
    data.append("description", f.description.value);
    data.append("isAvailable", f.isAvailable.checked ? "true" : "false");
    if (imageInput.files[0]) data.append("image", imageInput.files[0]);

    setBusy(productModal, true);
    Ajax.setLoading(productSubmit, true, "Đang lưu...");
    $("[data-modal-subtitle]", productModal).textContent = "Đang lưu món...";
    $("[data-form-status]", productForm).textContent = "Đang ghi nhận vào cơ sở dữ liệu...";
    try {
      var result = await Ajax.postForm(editingId ? URLS.update(editingId) : URLS.create, data);
      delete productModal.dataset.busy;
      productModal.close();
      Ajax.toast(result.message, "success");
      applySummary(result.data.summary);
      loadList({ highlightId: result.data.id });
    } catch (err) {
      $("[data-form-status]", productForm).textContent = "Chưa thể lưu dữ liệu.";
      if (err.status === 400 && err.data && err.data.errors) {
        Ajax.showErrors(productForm, err.data.errors);
        productModal.classList.add("has-errors");
      }
    } finally {
      setBusy(productModal, false);
      Ajax.setLoading(productSubmit, false);
      if (productModal.open) {
        $("[data-modal-subtitle]", productModal).textContent = productModal.classList.contains("has-errors")
          ? "Vui lòng điều chỉnh các mục được đánh dấu."
          : "";
      }
    }
  });

  // ================================================================ Bật/tắt Đang bán

  listEl.addEventListener("change", async function (event) {
    var toggle = event.target.closest('[data-action="toggle-available"]');
    if (!toggle) return;
    var row = toggle.closest("tr[data-id]");
    var isAvailable = toggle.checked;
    var label = $("[data-available-text]", row);
    toggle.disabled = true;
    try {
      var result = await Ajax.postJson(URLS.availability(row.dataset.id), { isAvailable: isAvailable });
      row.dataset.available = isAvailable ? "true" : "false";
      label.textContent = isAvailable ? "Đang bán" : "Ngừng bán";
      Ajax.toast(result.message, "success");
      if (state.status) loadList(); // đang lọc theo trạng thái → món có thể không còn thuộc bộ lọc
    } catch (err) {
      toggle.checked = !isAvailable; // trả về như cũ
    } finally {
      toggle.disabled = false;
    }
  });

  // ================================================================ Xóa món

  function deleteProduct(row) {
    var name = row.dataset.name;
    var orders = parseInt(row.dataset.orderCount, 10) || 0;
    confirmAction({
      title: "Xóa món " + name + "?",
      message: orders > 0
        ? "Món này đã có trong " + orders.toLocaleString("vi-VN") + " hóa đơn. Món sẽ được ẩn khỏi thực đơn và màn hình thu ngân; hóa đơn cũ và báo cáo vẫn giữ nguyên."
        : "Món chưa có trong hóa đơn nào nên sẽ bị xóa hẳn cùng định lượng. Thao tác này không thể hoàn tác.",
      okText: "Xóa món",
      busyText: "Đang xóa...",
      onConfirm: async function () {
        var result = await Ajax.postJson(URLS.remove(row.dataset.id), {});
        Ajax.toast(result.message, "success");
        applySummary(result.data.summary);
        loadList();
      }
    });
  }

  // ================================================================ Danh mục

  var categoryModal = document.getElementById("category-modal");
  var categoryForm = document.getElementById("category-form");
  var editingCategoryId = null;

  function openCategoryForm(row) {
    editingCategoryId = row ? row.dataset.categoryRow : null;
    categoryForm.reset();
    Ajax.clearErrors(categoryForm);
    $("#category-modal-title").textContent = row ? "Sửa danh mục" : "Thêm danh mục";
    var f = categoryForm.elements;
    if (row) {
      f.name.value = $('[data-field="name"]', row).textContent;
      f.sortOrder.value = row.dataset.sort;
    } else {
      var maxSort = summary.categories.reduce(function (m, c) { return Math.max(m, c.sortOrder); }, 0);
      f.sortOrder.value = maxSort + 1;
    }
    categoryModal.showModal();
    f.name.focus();
  }

  categoryForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var f = categoryForm.elements;
    var name = f.name.value.trim();
    var sortOrder = parseInt(f.sortOrder.value, 10);
    var errors = {};
    if (!name) errors.name = "Vui lòng nhập tên danh mục.";
    if (isNaN(sortOrder) || sortOrder < 0 || sortOrder > 999) errors.sortOrder = "Thứ tự hiển thị từ 0 đến 999.";
    if (Object.keys(errors).length) { Ajax.showErrors(categoryForm, errors); return; }

    var submit = $("[data-submit]", categoryForm);
    setBusy(categoryModal, true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var body = { name: name, sortOrder: sortOrder };
      var result = await Ajax.postJson(editingCategoryId ? URLS.categoryUpdate(editingCategoryId) : URLS.categoryCreate, body);
      delete categoryModal.dataset.busy;
      categoryModal.close();
      Ajax.toast(result.message, "success");
      applySummary(result.data.summary);
      loadList(); // tên danh mục có thể đổi → danh sách món cập nhật thứ tự
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(categoryForm, err.data && err.data.errors ? err.data.errors : { "": err.message });
    } finally {
      setBusy(categoryModal, false);
      Ajax.setLoading(submit, false);
    }
  });

  function deleteCategory(row) {
    var name = $('[data-field="name"]', row).textContent;
    var count = parseInt($('[data-field="count"]', row).textContent, 10) || 0;
    if (count > 0) {
      Ajax.toast("Danh mục \"" + name + "\" còn " + count + " món. Hãy chuyển các món sang danh mục khác hoặc xóa món trước.", "warn");
      return;
    }
    confirmAction({
      title: "Xóa danh mục " + name + "?",
      message: "Danh mục không còn món nào. Sau khi xóa, danh mục sẽ không hiện trên thực đơn nữa.",
      okText: "Xóa danh mục",
      busyText: "Đang xóa...",
      onConfirm: async function () {
        var result = await Ajax.postJson(URLS.categoryDelete(row.dataset.categoryRow), {});
        Ajax.toast(result.message, "success");
        applySummary(result.data.summary);
        loadList();
      }
    });
  }

  // ================================================================ Định lượng (A08)

  var recipeModal = document.getElementById("recipe-modal");
  var recipeForm = document.getElementById("recipe-form");
  var recipeLines = $("[data-recipe-lines]", recipeForm);
  var recipeLineTemplate = document.getElementById("recipe-line-template");
  var recipeProductId = null;
  var ingredients = [];
  var lineSeq = 0;

  async function openRecipe(row) {
    recipeProductId = row.dataset.id;
    $("#recipe-modal-title").textContent = "Định lượng: " + row.dataset.name;
    Ajax.clearErrors(recipeForm);
    recipeLines.replaceChildren(statusBox("Đang tải định lượng...", true));
    $("[data-recipe-empty]", recipeForm).hidden = true;
    var submit = $("[data-submit]", recipeForm);
    var addButton = $('[data-action="add-recipe-line"]', recipeForm);
    submit.disabled = true;
    addButton.disabled = true;
    recipeModal.showModal();

    try {
      var result = await Ajax.getJson(URLS.recipe(recipeProductId));
      ingredients = result.data.ingredients;
      recipeLines.replaceChildren();
      if (ingredients.length === 0) {
        recipeLines.replaceChildren(statusBox("Quán chưa có nguyên liệu nào. Hãy tạo nguyên liệu ở mục Kho nguyên liệu trước.", false));
        return;
      }
      result.data.items.forEach(function (item) { addRecipeLine(item); });
      updateRecipeEmpty();
      submit.disabled = false;
      addButton.disabled = false;
    } catch (err) {
      recipeLines.replaceChildren(statusBox(err.message, false));
    }
  }

  function statusBox(text, loading) {
    var box = document.createElement("div");
    box.className = "state";
    if (loading) {
      var spinner = document.createElement("span");
      spinner.className = "spinner";
      box.appendChild(spinner);
    }
    var p = document.createElement("span");
    p.textContent = text;
    box.appendChild(p);
    return box;
  }

  function addRecipeLine(item) {
    var line = recipeLineTemplate.content.firstElementChild.cloneNode(true);
    var id = "recipe-line-" + (++lineSeq);
    var select = $('[data-field="ingredientId"]', line);
    var quantity = $('[data-field="quantity"]', line);
    select.id = id + "-ingredient";
    quantity.id = id + "-quantity";
    $("[data-label-ingredient]", line).htmlFor = select.id;
    $("[data-label-quantity]", line).htmlFor = quantity.id;

    ingredients.forEach(function (ing) {
      var option = document.createElement("option");
      option.value = ing.id;
      option.textContent = ing.name;
      option.dataset.unit = ing.unit;
      select.appendChild(option);
    });
    if (item) {
      select.value = item.ingredientId;
      quantity.value = String(item.quantity).replace(".", ",");
    }
    recipeLines.appendChild(line);
    updateLineUnit(line);
    syncIngredientOptions();
    updateRecipeEmpty();
    if (!item) select.focus();
  }

  function removeRecipeLine(line) {
    line.remove();
    syncIngredientOptions();
    updateRecipeEmpty();
  }

  function updateRecipeEmpty() {
    $("[data-recipe-empty]", recipeForm).hidden = $all("[data-recipe-line]", recipeLines).length > 0;
  }

  function updateLineUnit(line) {
    var option = $('[data-field="ingredientId"]', line).selectedOptions[0];
    $('[data-field="unit"]', line).textContent = option && option.dataset.unit ? option.dataset.unit : "–";
  }

  /** Không cho chọn một nguyên liệu ở hai dòng: khóa lựa chọn đã dùng ở dòng khác. */
  function syncIngredientOptions() {
    var selects = $all('[data-field="ingredientId"]', recipeLines);
    var used = selects.map(function (s) { return s.value; });
    selects.forEach(function (select, index) {
      $all("option", select).forEach(function (option) {
        option.disabled = option.value !== "" && used.indexOf(option.value) >= 0 && used[index] !== option.value;
      });
    });
  }

  recipeLines.addEventListener("change", function (event) {
    if (event.target.matches('[data-field="ingredientId"]')) {
      updateLineUnit(event.target.closest("[data-recipe-line]"));
      syncIngredientOptions();
    }
  });

  function parseQuantity(text) {
    var normalized = text.trim().replace(",", ".");
    return /^\d+(\.\d{1,3})?$/.test(normalized) ? parseFloat(normalized) : NaN;
  }

  function showRecipeErrors(errors) {
    $all("[data-error-key]", recipeLines).forEach(function (el) { el.textContent = ""; });
    $all(".is-invalid", recipeLines).forEach(function (el) { el.classList.remove("is-invalid"); });
    var lines = $all("[data-recipe-line]", recipeLines);
    var general = [];
    Object.keys(errors).forEach(function (key) {
      var match = /^items\[(\d+)\]\.(ingredientId|quantity)$/i.exec(key);
      var line = match && lines[parseInt(match[1], 10)];
      if (!line) { general.push([].concat(errors[key]).join(" ")); return; }
      var field = match[2].toLowerCase() === "quantity" ? "quantity" : "ingredientId";
      $('[data-error-key="' + field + '"]', line).textContent = [].concat(errors[key]).join(" ");
      $('[data-field="' + field + '"]', line).classList.add("is-invalid");
    });
    $('[data-error-for=""]', recipeForm).textContent = general.join(" ");
  }

  recipeForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var lines = $all("[data-recipe-line]", recipeLines);
    var items = [];
    var errors = {};
    var seen = {};
    lines.forEach(function (line, i) {
      var ingredientId = $('[data-field="ingredientId"]', line).value;
      var quantity = parseQuantity($('[data-field="quantity"]', line).value);
      if (!ingredientId) errors["items[" + i + "].ingredientId"] = "Vui lòng chọn nguyên liệu.";
      else if (seen[ingredientId]) errors["items[" + i + "].ingredientId"] = "Nguyên liệu này đã có ở dòng trên.";
      seen[ingredientId] = true;
      if (isNaN(quantity) || quantity <= 0) errors["items[" + i + "].quantity"] = "Định lượng phải lớn hơn 0 (tối đa 3 chữ số thập phân).";
      items.push({ ingredientId: parseInt(ingredientId, 10) || 0, quantity: quantity });
    });
    showRecipeErrors(errors);
    if (Object.keys(errors).length) return;

    var submit = $("[data-submit]", recipeForm);
    setBusy(recipeModal, true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var result = await Ajax.postJson(URLS.recipe(recipeProductId), { items: items });
      delete recipeModal.dataset.busy;
      recipeModal.close();
      Ajax.toast(result.message, "success");
      applySummary(result.data.summary);
      loadList({ highlightId: recipeProductId });
    } catch (err) {
      if (err.status === 400) showRecipeErrors(err.data && err.data.errors ? err.data.errors : { "": err.message });
    } finally {
      setBusy(recipeModal, false);
      Ajax.setLoading(submit, false);
    }
  });

  // ================================================================ Khởi động

  readStateFromUrl();
  loadList();
})();
