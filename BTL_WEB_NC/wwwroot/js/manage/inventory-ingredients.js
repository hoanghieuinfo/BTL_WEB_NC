// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH12 – Tab Nguyên liệu. Danh sách là Partial View do server render (chèn innerHTML – Razor đã mã hóa);
// panel Thêm/Sửa điền bằng value/textContent.
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;
  var URLS = {
    list: "/manage/inventory/ingredients/list",
    create: "/manage/inventory/ingredients/create",
    update: function (id) { return "/manage/inventory/ingredients/" + encodeURIComponent(id) + "/update"; },
    remove: function (id) { return "/manage/inventory/ingredients/" + encodeURIComponent(id) + "/delete"; }
  };

  var listEl = $("[data-ingredient-list]");
  var filterForm = $("[data-filter-form]");
  var state = { q: "", unit: "", page: 1 };
  var requestSeq = 0;

  // ================================================================ Danh sách

  function params() {
    return { q: state.q.trim(), unit: state.unit, page: state.page > 1 ? state.page : "" };
  }

  function writeUrl() {
    var search = new URLSearchParams();
    var p = params();
    Object.keys(p).forEach(function (k) { if (p[k]) search.set(k, p[k]); });
    var text = search.toString();
    history.replaceState(null, "", location.pathname + (text ? "?" + text : ""));
  }

  function readUrl() {
    var search = new URLSearchParams(location.search);
    state.q = search.get("q") || "";
    state.unit = search.get("unit") || "";
    state.page = parseInt(search.get("page"), 10) || 1;
    filterForm.elements.q.value = state.q;
    filterForm.elements.unit.value = state.unit;
    if (filterForm.elements.unit.value !== state.unit) state.unit = ""; // đơn vị lạ trên URL
  }

  async function loadList() {
    var seq = ++requestSeq;
    writeUrl();
    listEl.setAttribute("aria-busy", "true");
    listEl.replaceChildren(document.getElementById("ingredient-loading-template").content.cloneNode(true));
    try {
      var html = await Ajax.getHtml(URLS.list, params(), { silent: true });
      if (seq !== requestSeq) return;
      listEl.innerHTML = html; // Partial View do server render – Razor đã mã hóa
      var meta = $("[data-list-meta]", listEl);
      if (meta) state.page = parseInt(meta.dataset.page, 10) || 1;
      writeUrl();
      markEditing();
    } catch (err) {
      if (seq !== requestSeq || err.status === 401) return;
      var box = document.createElement("div");
      box.className = "state state-error";
      var title = document.createElement("p");
      title.className = "state-title";
      title.textContent = "Không tải được danh sách nguyên liệu";
      var retry = document.createElement("button");
      retry.type = "button";
      retry.className = "btn";
      retry.dataset.action = "retry";
      retry.textContent = "Thử lại";
      box.append(title, retry);
      listEl.replaceChildren(box);
    } finally {
      if (seq === requestSeq) listEl.setAttribute("aria-busy", "false");
    }
  }

  function setFilter(changes) {
    Object.assign(state, changes, { page: changes.page || 1 });
    loadList();
  }

  filterForm.addEventListener("submit", function (event) { event.preventDefault(); });
  var timer = null;
  filterForm.elements.q.addEventListener("input", function () {
    clearTimeout(timer);
    timer = setTimeout(function () { setFilter({ q: filterForm.elements.q.value }); }, 300);
  });
  filterForm.elements.unit.addEventListener("change", function () { setFilter({ unit: filterForm.elements.unit.value }); });

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action], [data-page], .unit-chip");
    if (!target || target.disabled) return;
    if (target.dataset.page && listEl.contains(target)) {
      setFilter({ page: parseInt(target.dataset.page, 10) });
      return;
    }
    if (target.classList.contains("unit-chip")) {
      form.elements.unit.value = target.dataset.unit;
      syncUnit();
      return;
    }
    switch (target.dataset.action) {
      case "edit": startEdit(target.closest("[data-id]")); break;
      case "reset-form": resetForm(); break;
      case "delete-ingredient": deleteIngredient(); break;
      case "retry": loadList(); break;
      case "clear-filters":
        filterForm.reset();
        setFilter({ q: "", unit: "" });
        break;
    }
  });

  // ================================================================ Panel Thêm / Sửa

  var form = document.getElementById("ingredient-form");
  var submit = $("[data-submit]", form);
  var deleteButton = $('[data-action="delete-ingredient"]', form);
  var editing = null; // { id, name, unitLocked, productCount } khi đang sửa

  function syncUnit() {
    var unit = form.elements.unit.value.trim();
    $("[data-min-unit]", form).textContent = unit;
    $all(".unit-chip", form).forEach(function (chip) { chip.classList.toggle("is-active", chip.dataset.unit === unit); });
  }
  form.elements.unit.addEventListener("input", syncUnit);

  function resetForm() {
    editing = null;
    form.reset();
    Ajax.clearErrors(form);
    $("[data-name-tag]", form).hidden = true;
    $("#ingredient-form-title").textContent = "Thêm nguyên liệu";
    $("[data-form-icon]", form).textContent = "+";
    $("[data-form-hint]", form).textContent = "Nguyên liệu dùng trong định lượng món và phiếu nhập kho.";
    $("[data-unit-hint]", form).textContent = "Định lượng món và tồn kho đều tính theo đơn vị này.";
    form.elements.unit.readOnly = false;
    $all(".unit-chip", form).forEach(function (chip) { chip.disabled = false; });
    submit.textContent = "+ Lưu nguyên liệu";
    deleteButton.hidden = true;
    syncUnit();
    markEditing();
  }

  function startEdit(row) {
    editing = {
      id: row.dataset.id,
      name: row.dataset.name,
      unitLocked: row.dataset.unitLocked === "true",
      productCount: parseInt(row.dataset.productCount, 10) || 0
    };
    Ajax.clearErrors(form);
    $("[data-name-tag]", form).hidden = true;
    $("#ingredient-form-title").textContent = "Sửa nguyên liệu";
    $("[data-form-icon]", form).textContent = "✎";
    $("[data-form-hint]", form).textContent = "Đang sửa \"" + editing.name + "\". Bấm Đặt lại để thêm nguyên liệu mới.";
    form.elements.name.value = row.dataset.name;
    form.elements.unit.value = row.dataset.unit;
    form.elements.minStock.value = row.dataset.minStock;
    // Đơn vị cơ sở bị khóa khi đã có định lượng/tồn/phiếu nhập (server cũng chặn).
    form.elements.unit.readOnly = editing.unitLocked;
    $all(".unit-chip", form).forEach(function (chip) { chip.disabled = editing.unitLocked; });
    $("[data-unit-hint]", form).textContent = editing.unitLocked
      ? "Không đổi được vì đã có định lượng, tồn kho hoặc phiếu nhập theo đơn vị này."
      : "Định lượng món và tồn kho đều tính theo đơn vị này.";
    submit.textContent = "Lưu thay đổi";
    deleteButton.hidden = false;
    syncUnit();
    markEditing();
    form.elements.name.focus();
  }

  /** Tô dòng đang sửa trong bảng. */
  function markEditing() {
    $all(".ingredient-row", listEl).forEach(function (row) {
      row.classList.toggle("is-editing", !!editing && row.dataset.id === editing.id);
    });
  }

  function formErrors(body) {
    var errors = {};
    if (!body.name) errors.name = "Vui lòng nhập tên nguyên liệu.";
    if (!body.unit) errors.unit = "Vui lòng nhập đơn vị cơ sở.";
    if (form.elements.minStock.value === "") errors.minStock = "Vui lòng nhập mức tồn tối thiểu.";
    else if (!(body.minStock >= 0)) errors.minStock = "Mức tồn tối thiểu phải từ 0 trở lên.";
    else if (Math.round(body.minStock * 1000) / 1000 !== body.minStock) errors.minStock = "Tối đa 3 chữ số thập phân.";
    return errors;
  }

  form.addEventListener("submit", async function (event) {
    event.preventDefault();
    var body = {
      name: form.elements.name.value.trim(),
      unit: form.elements.unit.value.trim(),
      minStock: parseFloat(form.elements.minStock.value)
    };
    var errors = formErrors(body);
    if (Object.keys(errors).length) { Ajax.showErrors(form, errors); return; }

    var wasEditing = !!editing;
    var saved = false;
    setFormBusy(true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var result = await Ajax.postJson(wasEditing ? URLS.update(editing.id) : URLS.create, body);
      Ajax.toast(result.message, "success");
      if (!wasEditing) adjustCount(1);
      addUnitOption(body.unit);
      saved = true;
    } catch (err) {
      if (err.status === 400) {
        var serverErrors = err.data && err.data.errors ? err.data.errors : { "": err.message };
        Ajax.showErrors(form, serverErrors);
        $("[data-name-tag]", form).hidden = !serverErrors.name; // nhãn "Đã tồn tại" như ảnh thiết kế
      }
    } finally {
      setFormBusy(false);
      Ajax.setLoading(submit, false);
    }
    if (saved) {
      resetForm(); // sau setLoading(false) để chữ trên nút về "+ Lưu nguyên liệu"
      loadList();
    }
  });

  function setFormBusy(busy) {
    $all("input, button", form).forEach(function (el) {
      if (el === submit) return;
      el.disabled = busy || (el.classList.contains("unit-chip") && !!editing && editing.unitLocked);
    });
  }

  function deleteIngredient() {
    if (!editing) return;
    var target = editing;
    if (target.productCount > 0) {
      // Server cũng chặn; báo sớm để khỏi mở hộp xác nhận vô ích.
      Ajax.toast("\"" + target.name + "\" đang dùng trong định lượng của " + target.productCount
        + " món. Bỏ nguyên liệu khỏi các món đó trước khi xóa.", "warn");
      return;
    }
    ManageUi.confirmAction({
      title: "Xóa nguyên liệu \"" + target.name + "\"?",
      message: "Nguyên liệu sẽ ẩn khỏi danh mục, tồn kho và phiếu nhập mới. Phiếu nhập cũ vẫn được giữ.",
      okText: "Xóa nguyên liệu",
      busyText: "Đang xóa...",
      onConfirm: async function () {
        var result = await Ajax.postJson(URLS.remove(target.id), {});
        Ajax.toast(result.message, "success");
        adjustCount(-1);
        resetForm();
        await loadList();
      }
    });
  }

  /** Số trên tab "Nguyên liệu". */
  function adjustCount(delta) {
    var badge = $(".inventory-tab.is-active .tab-badge");
    if (badge) badge.textContent = Math.max(0, (parseInt(badge.textContent, 10) || 0) + delta);
  }

  function addUnitOption(unit) {
    var select = filterForm.elements.unit;
    if (Array.prototype.some.call(select.options, function (o) { return o.value === unit; })) return;
    var option = document.createElement("option");
    option.value = unit;
    option.textContent = "Đơn vị: " + unit;
    select.append(option);
  }

  readUrl();
  resetForm();
  loadList();
})();
