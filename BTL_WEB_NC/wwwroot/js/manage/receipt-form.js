// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH12 – Lập phiếu nhập kho (BM07). Dòng nguyên liệu tạo từ <template> bằng cloneNode, chữ điền bằng textContent.
// Thành tiền chỉ để xem trước: server tính lại TotalAmount theo cùng công thức round(Số lượng × Đơn giá).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;
  var form = document.getElementById("receipt-form");
  if (!form) return; // chưa có chi nhánh hoặc nguyên liệu

  var URLS = { create: "/manage/inventory/receipts/create", list: "/manage/inventory/receipts" };
  var FLASH_KEY = "manage.inventory.flash";
  var FIELDS = ["ingredientId", "purchaseUnit", "conversionRate", "quantity", "unitPrice"];

  var linesEl = $("[data-lines]", form);
  var template = document.getElementById("line-template");
  var submit = $("[data-submit]", form);
  var vi = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 3 });
  var lineSeq = 0;
  var dirty = false;

  function number(value) { return value === "" ? NaN : Number(value); }
  function money(value) { return vi.format(value) + " đ"; }

  /** Thành tiền: số lượng tối đa 3 số lẻ → tính trên phần nghìn để tránh sai số dấu phẩy động. */
  function lineAmount(quantity, price) {
    if (!(quantity > 0) || !(price >= 0)) return 0;
    return Math.round(Math.round(quantity * 1000) * price / 1000);
  }

  function field(line, name) { return $('[data-field="' + name + '"]', line); }

  function branchName() {
    var option = form.elements.branchId.selectedOptions[0];
    return option ? option.textContent.replace(" (tạm ngưng)", "") : "";
  }

  // ================================================================ Thêm / xóa dòng

  function addLine(ingredientId) {
    var line = template.content.firstElementChild.cloneNode(true);
    line.dataset.lineKey = String(++lineSeq);
    linesEl.append(line);
    if (ingredientId) {
      field(line, "ingredientId").value = ingredientId;
      applyIngredient(line, true);
    }
    renumber();
    updateLine(line);
    return line;
  }

  function removeLine(line) {
    line.remove();
    if (!linesEl.children.length) addLine();
    renumber();
    updateTotal();
    dirty = true;
  }

  /**
   * Đặt lại name/id theo vị trí: lines[0].quantity… khớp khóa lỗi của server (data.errors) để Ajax.showErrors
   * hiện lỗi đúng ô.
   */
  function renumber() {
    $all(".receipt-line", linesEl).forEach(function (line, index) {
      FIELDS.forEach(function (name) {
        var key = "lines[" + index + "]." + name;
        var input = field(line, name);
        input.name = key;
        input.id = "line-" + line.dataset.lineKey + "-" + name;
        var label = $('[data-label-for="' + name + '"]', line);
        if (label) label.htmlFor = input.id;
        $('[data-error-field="' + name + '"]', line).setAttribute("data-error-for", key);
      });
      $('[data-action="remove-line"]', line).setAttribute("aria-label", "Xóa dòng " + (index + 1));
    });
    $("[data-line-count]", form).textContent = linesEl.children.length;
  }

  // ================================================================ Tính toán từng dòng

  /** Chọn nguyên liệu → đơn vị nhập mặc định = đơn vị cơ sở, quy đổi 1 (khi người dùng chưa tự nhập). */
  function applyIngredient(line, force) {
    var option = field(line, "ingredientId").selectedOptions[0];
    var baseUnit = option && option.dataset.unit ? option.dataset.unit : "";
    var unitInput = field(line, "purchaseUnit");
    var rateInput = field(line, "conversionRate");
    if (baseUnit && (force || !unitInput.value || unitInput.value === line.dataset.baseUnit)) {
      unitInput.value = baseUnit;
      rateInput.value = "1";
    }
    line.dataset.baseUnit = baseUnit;
  }

  function updateLine(line) {
    var baseUnit = line.dataset.baseUnit || "";
    var unit = field(line, "purchaseUnit").value.trim();
    var rate = number(field(line, "conversionRate").value);
    var quantity = number(field(line, "quantity").value);
    var price = number(field(line, "unitPrice").value);

    $("[data-rate-hint]", line).textContent = baseUnit && unit && rate > 0 && unit !== baseUnit
      ? "1 " + unit + " = " + vi.format(rate) + " " + baseUnit : "";

    var hint = $("[data-line-hint]", line);
    if (!baseUnit) {
      hint.textContent = "Chưa chọn nguyên liệu";
      hint.classList.remove("is-ok");
    } else if (quantity > 0 && rate > 0) {
      hint.textContent = "Cộng " + vi.format(Math.round(quantity * rate * 1000) / 1000) + " " + baseUnit + " vào kho " + branchName();
      hint.classList.add("is-ok");
    } else {
      hint.textContent = "Đơn vị cơ sở: " + baseUnit;
      hint.classList.remove("is-ok");
    }

    $("[data-line-amount]", line).textContent = money(lineAmount(quantity, price));
  }

  function updateTotal() {
    var total = $all(".receipt-line", linesEl).reduce(function (sum, line) {
      return sum + lineAmount(number(field(line, "quantity").value), number(field(line, "unitPrice").value));
    }, 0);
    $("[data-grand-total]", form).textContent = money(total);
  }

  function updateAll() {
    $("[data-branch-name]", form).textContent = branchName();
    $all(".receipt-line", linesEl).forEach(updateLine);
    updateTotal();
  }

  function onLineEdit(event) {
    var line = event.target.closest(".receipt-line");
    if (!line) return;
    if (event.type === "change" && event.target.dataset.field === "ingredientId") applyIngredient(line, false);
    dirty = true;
    updateLine(line);
    updateTotal();
  }
  linesEl.addEventListener("input", onLineEdit);
  linesEl.addEventListener("change", onLineEdit);
  form.elements.branchId.addEventListener("change", updateAll);

  form.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action]");
    if (!target) return;
    if (target.dataset.action === "add-line") {
      if (linesEl.children.length >= 50) {
        Ajax.toast("Một phiếu tối đa 50 dòng.", "warn");
        return;
      }
      field(addLine(), "ingredientId").focus();
    } else if (target.dataset.action === "remove-line") {
      removeLine(target.closest(".receipt-line"));
    }
  });

  // ================================================================ Kiểm tra và lưu

  function collect() {
    return {
      branchId: parseInt(form.elements.branchId.value, 10) || null,
      receivedDate: form.elements.receivedDate.value || null,
      supplier: form.elements.supplier.value.trim() || null,
      lines: $all(".receipt-line", linesEl).map(function (line) {
        return {
          ingredientId: parseInt(field(line, "ingredientId").value, 10) || 0,
          purchaseUnit: field(line, "purchaseUnit").value.trim(),
          conversionRate: number(field(line, "conversionRate").value),
          quantity: number(field(line, "quantity").value),
          unitPrice: number(field(line, "unitPrice").value)
        };
      })
    };
  }

  function threeDecimals(value) { return Math.round(value * 1000) / 1000 === value; }

  /** Cùng quy tắc với server (ReceiptFormModel, ReceiptLineModel) để báo lỗi tại ô trước khi gửi. */
  function validate(body) {
    var errors = {};
    if (!body.branchId) errors.branchId = "Vui lòng chọn chi nhánh nhập.";
    if (!body.receivedDate) errors.receivedDate = "Vui lòng chọn ngày nhập.";
    else if (body.receivedDate > form.elements.receivedDate.max) errors.receivedDate = "Ngày nhập không được sau hôm nay.";
    body.lines.forEach(function (line, i) {
      var key = "lines[" + i + "].";
      if (!line.ingredientId) errors[key + "ingredientId"] = "Vui lòng chọn nguyên liệu.";
      if (!line.purchaseUnit) errors[key + "purchaseUnit"] = "Nhập đơn vị.";
      if (!(line.conversionRate > 0)) errors[key + "conversionRate"] = "Phải lớn hơn 0";
      else if (!threeDecimals(line.conversionRate)) errors[key + "conversionRate"] = "Tối đa 3 chữ số thập phân.";
      if (!(line.quantity > 0)) errors[key + "quantity"] = "Phải lớn hơn 0";
      else if (!threeDecimals(line.quantity)) errors[key + "quantity"] = "Tối đa 3 chữ số thập phân.";
      if (isNaN(line.unitPrice)) errors[key + "unitPrice"] = "Nhập đơn giá.";
      else if (line.unitPrice < 0 || Math.floor(line.unitPrice) !== line.unitPrice) errors[key + "unitPrice"] = "Đơn giá là số nguyên từ 0 trở lên.";
    });
    return errors;
  }

  form.addEventListener("submit", async function (event) {
    event.preventDefault();
    var body = collect();
    var errors = validate(body);
    if (Object.keys(errors).length) {
      Ajax.showErrors(form, errors);
      return;
    }
    // Khóa form trong lúc lưu (CSS .is-busy chặn thao tác) để không gửi trùng.
    $all("input, select, button, a.btn", form).forEach(function (el) { if (el !== submit) el.setAttribute("aria-disabled", "true"); });
    form.classList.add("is-busy");
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var result = await Ajax.postJson(URLS.create, body);
      dirty = false;
      try { sessionStorage.setItem(FLASH_KEY, result.message); } catch (e) { /* bỏ qua */ }
      location.href = URLS.list;
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(form, err.data && err.data.errors ? err.data.errors : { "": err.message });
      form.classList.remove("is-busy");
      $all("[aria-disabled]", form).forEach(function (el) { el.removeAttribute("aria-disabled"); });
      Ajax.setLoading(submit, false);
    }
  });

  // Rời trang khi đã nhập dở → trình duyệt hỏi lại.
  window.addEventListener("beforeunload", function (event) {
    if (dirty) { event.preventDefault(); event.returnValue = ""; }
  });

  addLine(form.dataset.prefillIngredient || "");
  updateAll();
  var first = field(linesEl.firstElementChild, form.dataset.prefillIngredient ? "quantity" : "ingredientId");
  if (first) first.focus();
})();
