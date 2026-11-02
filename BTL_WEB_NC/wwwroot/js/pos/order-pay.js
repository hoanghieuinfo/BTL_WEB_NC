// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH15 – Thanh toán (A16, UC16, R26): hộp thoại giảm giá + lý do, hình thức, tiền khách đưa; JS tính sẵn tiền thừa.
// Mọi con số ở đây chỉ để hiển thị và chặn sớm: server tự tính lại tổng từ CSDL và kiểm tra lại tất cả (Bảng 3.22).
// RowVersion lấy từ data-row-version (order-panel.js cập nhật sau mỗi thao tác) và gửi lại để chống thanh toán trùng (409).
// Nạp sau order-panel.js (dùng P.Order). Dựng DOM bằng textContent, không dùng innerHTML.
(function () {
  "use strict";

  var app = document.getElementById("order-app");
  if (!app || app.getAttribute("data-open") !== "true") return;

  var P = window.Pos, $ = P.$;
  if (!P.Order) return;

  var METHOD_CASH = 0, METHOD_TRANSFER = 1;   // khớp enum PaymentMethod ở server

  var openBtn = $("pay-open");
  var modal = $("pay-modal");
  var form = $("pay-form");
  var discountInput = $("pay-discount");
  var reasonField = $("pay-reason-field");
  var reasonInput = $("pay-reason");
  var cashField = $("pay-cash-field");
  var cashInput = $("pay-cash");
  var changeEl = $("pay-change");
  var submitBtn = $("pay-submit");
  var methodBtns = modal.querySelectorAll("[data-pay-method]");

  var method = METHOD_CASH;
  var busy = false;

  function subTotal() { return Number(app.getAttribute("data-subtotal")) || 0; }
  function hasActiveLines() { return document.querySelectorAll("#order-lines li.line:not(.is-cancelled)").length > 0; }

  // "50.000", "50 000", "50,000" → 50000. Rỗng → 0 hoặc null tùy emptyValue; ký tự lạ → NaN.
  function parseMoney(raw, emptyValue) {
    var s = String(raw).replace(/[.,\s]/g, "");
    if (s === "") return emptyValue;
    return /^\d+$/.test(s) ? parseInt(s, 10) : NaN;
  }

  // ------------------------------------------------------------ Tính và kiểm tra (giống UC16 3a, 3b)

  function compute() {
    var sub = subTotal();
    var discount = parseMoney(discountInput.value, 0);
    var reason = reasonInput.value.trim();
    var errors = {};

    if (isNaN(discount)) {
      errors.discount = "Giảm giá phải là số tiền nguyên (đồng).";
    } else if (discount > sub) {
      errors.discount = "Giảm giá không được lớn hơn tổng tiền hàng.";
    } else if (discount > 0 && !reason) {
      errors.discountReason = "Vui lòng nhập lý do giảm giá.";
    }

    var validDiscount = !isNaN(discount) && discount <= sub;
    var total = validDiscount ? sub - discount : sub;

    var cash = null, change = 0;
    if (method === METHOD_CASH) {
      cash = parseMoney(cashInput.value, null);
      if (cash === null) {
        errors.cashReceived = "Vui lòng nhập tiền khách đưa.";
      } else if (isNaN(cash)) {
        errors.cashReceived = "Tiền khách đưa phải là số tiền nguyên (đồng).";
      } else if (cash < total) {
        errors.cashReceived = "Tiền khách đưa chưa đủ để thanh toán.";
      } else {
        change = cash - total;
      }
    }
    return { sub: sub, discount: validDiscount ? discount : 0, reason: reason, total: total, cash: cash, change: change, errors: errors };
  }

  // Cập nhật số hiển thị và nút xác nhận mỗi khi người dùng gõ. Lỗi chỉ hiện chi tiết khi bấm xác nhận để không "la" lúc đang gõ.
  function refresh() {
    var c = compute();
    $("pay-total").textContent = P.money(c.total);
    reasonField.hidden = !(c.discount > 0);

    changeEl.className = "field-hint pay-change";
    if (method !== METHOD_CASH) {
      changeEl.textContent = "";
    } else if (c.errors.cashReceived || c.cash === null) {
      var short = c.cash !== null && !isNaN(c.cash) && c.cash < c.total;
      changeEl.textContent = short ? "Còn thiếu " + P.money(c.total - c.cash) : "";
      if (short) changeEl.classList.add("is-short");
    } else {
      changeEl.textContent = "Tiền thừa: " + P.money(c.change);
      changeEl.classList.add("is-ok");
    }
    // UC16 3a: tiền mặt mà khách đưa thiếu thì không cho xác nhận. Các lỗi khác (giảm giá, lý do) hiện khi bấm xác nhận.
    submitBtn.disabled = busy || !!c.errors.cashReceived;
    return c;
  }

  function setMethod(value) {
    method = value;
    methodBtns.forEach(function (b) {
      var on = Number(b.getAttribute("data-pay-method")) === value;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-pressed", on ? "true" : "false");
    });
    cashField.hidden = value !== METHOD_CASH;
    refresh();
  }

  // ------------------------------------------------------------ Mở / đóng

  function closeModal() {
    if (modal.open) modal.close();
  }

  function reload(delay) { setTimeout(function () { location.reload(); }, delay || 1500); }

  openBtn.addEventListener("click", function () {
    if (modal.open) return;
    if (!hasActiveLines()) {
      window.Ajax.toast("Đơn chưa có món nào nên không thể thanh toán.", "warn");
      return;
    }
    window.Ajax.clearErrors(form);
    discountInput.value = "";
    reasonInput.value = "";
    cashInput.value = "";
    $("pay-subtotal").textContent = P.money(subTotal());
    setMethod(METHOD_CASH);
    modal.showModal();
    cashInput.focus();
  });

  methodBtns.forEach(function (b) {
    b.addEventListener("click", function () { setMethod(Number(b.getAttribute("data-pay-method"))); });
  });
  [discountInput, reasonInput, cashInput].forEach(function (el) { el.addEventListener("input", refresh); });

  // ------------------------------------------------------------ Gửi A16

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    if (busy) return;

    var c = compute();
    window.Ajax.clearErrors(form);
    if (Object.keys(c.errors).length) {
      var shown = {};
      Object.keys(c.errors).forEach(function (k) { shown[k] = [c.errors[k]]; });
      window.Ajax.showErrors(form, shown);
      return;
    }

    // Chỉ gửi những gì nhân viên nhập + RowVersion gốc; không gửi tổng tiền (server tự tính).
    var body = {
      rowVersion: app.getAttribute("data-row-version"),
      discount: c.discount,
      discountReason: c.discount > 0 ? c.reason : null,
      method: method,
      cashReceived: method === METHOD_CASH ? c.cash : null
    };

    busy = true;
    submitBtn.disabled = true;
    window.Ajax.setLoading(submitBtn, true, "Đang thanh toán…");
    window.Ajax.postJson("/pos/orders/" + encodeURIComponent(P.Order.id) + "/pay", body)
      .then(function (res) {
        var d = res.data || {};
        var msg = res.message || "Đã thanh toán.";
        if (method === METHOD_CASH && d.change > 0) msg += " Tiền thừa: " + P.money(d.change);
        window.Ajax.toast(msg, "success");
        closeModal();
        // MH16 (làm ở bước 9): hóa đơn BM06 của đơn này.
        location.assign("/pos/orders/" + encodeURIComponent(P.Order.id) + "/print");
      })
      .catch(function (err) {
        busy = false;
        window.Ajax.setLoading(submitBtn, false);
        refresh();
        if (!err || err.status === 401) return;
        if (err.status === 400 && err.data && err.data.errors) {
          window.Ajax.showErrors(form, err.data.errors);
          return;
        }
        // 409: đơn đã thanh toán/đổi ở máy khác (hoặc tồn kho vừa bị sửa) → toast đã hiện, tải lại để thấy đúng đơn. 404: tương tự.
        closeModal();
        if (err.status === 409 || err.status === 404) reload();
      });
  });

  modal.querySelectorAll("[data-close]").forEach(function (el) { el.addEventListener("click", closeModal); });
  modal.addEventListener("close", function () {
    busy = false;
    window.Ajax.setLoading(submitBtn, false);
  });
  // Quay lại bằng nút Back từ trang in: trang trong bộ nhớ đệm có thể còn dữ liệu cũ → tải lại.
  window.addEventListener("pageshow", function (e) { if (e.persisted) location.reload(); });
})();
