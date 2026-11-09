// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH15 – Khung "Đơn hiện tại": vẽ lại đơn từ JSON, hộp thoại thêm/sửa món (A13, R23) và hủy món (A14, R25).
// Các thao tác ghi đều trả về OrderDetailDto; render() cập nhật danh sách, tổng tiền, trạng thái, RowVersion.
// Nạp trước order.js. Dựng DOM bằng createElement/textContent, không dùng innerHTML (Bảng 3.22 – XSS).
(function () {
  "use strict";

  var app = document.getElementById("order-app");
  if (!app || app.getAttribute("data-open") !== "true") return;   // đơn đã đóng: chỉ xem

  var P = window.Pos, h = P.h, $ = P.$;
  var orderId = app.getAttribute("data-order-id");

  var linesBox = $("order-lines-box");
  var modal = $("item-modal");
  var form = $("item-form");
  var qtyInput = $("item-qty");
  var noteInput = $("item-note");
  var submitBtn = $("item-submit");

  var cancelModal = $("cancel-modal");
  var cancelForm = $("cancel-form");
  var cancelReason = $("cancel-reason");
  var cancelSubmit = $("cancel-submit");

  var mode = null;          // { type: "add", productId } hoặc { type: "edit", itemId }
  var submitting = false;
  var cancelling = null;    // { itemId } khi hộp thoại hủy món đang mở
  var cancelBusy = false;

  // ------------------------------------------------------------ Vẽ đơn

  function statusOf(d) {
    if (d.status === 1) return ["Đã thanh toán", "st-done"];
    if (d.status === 2) return ["Đã hủy", "st-cancel"];
    return d.tableStatus === P.TableStatus.AwaitingPayment ? ["Chờ thanh toán", "st-2"] : ["Đang phục vụ", "st-1"];
  }

  function lineItem(line) {
    var main = [h("span", { class: "line-name", text: line.productName })];
    if (line.note) main.push(h("span", { class: "line-note", text: line.note }));
    if (line.isCancelled) {
      main.push(h("span", { class: "line-cancel", text: "Đã hủy" + (line.cancelReason ? ": " + line.cancelReason : "") }));
    }

    var actions = [];
    if (!line.isCancelled) {
      actions.push(h("button", {
        type: "button", class: "btn btn-sm", text: "Sửa",
        "data-edit-line": line.id, "aria-label": "Sửa món " + line.productName
      }));
      actions.push(h("button", {
        type: "button", class: "btn btn-sm btn-danger", text: "Hủy",
        "data-cancel-line": line.id, "aria-label": "Hủy món " + line.productName
      }));
    }

    return h("li", {
      class: "line" + (line.isCancelled ? " is-cancelled" : ""),
      "data-line-id": line.id, "data-name": line.productName, "data-qty": line.quantity, "data-note": line.note || ""
    }, [
      h("div", { class: "line-main" }, main),
      h("span", { class: "line-qty", text: "× " + line.quantity }),
      h("span", { class: "line-amount", text: P.money(line.amount) }),
      h("span", { class: "line-actions" }, actions)
    ]);
  }

  function renderLines(lines) {
    linesBox.textContent = "";
    if (!lines.length) {
      linesBox.appendChild(h("div", { class: "state state-soft", id: "order-empty" }, [
        h("p", { class: "state-title", text: "Chưa có món nào" }),
        h("p", { text: "Chọn món bên cạnh để bắt đầu gọi." })
      ]));
      return;
    }
    linesBox.appendChild(h("ol", { class: "line-list", id: "order-lines" }, lines.map(lineItem)));
  }

  function render(d) {
    renderLines(d.lines || []);

    var active = (d.lines || []).filter(function (l) { return !l.isCancelled; }).length;
    $("order-count").textContent = "(" + active + " món)";
    $("sum-subtotal").textContent = P.money(d.subTotal);
    $("sum-total").textContent = P.money(d.total);
    $("sum-discount").textContent = "− " + P.money(d.discount);
    $("sum-discount-row").hidden = !(d.discount > 0);

    var st = statusOf(d);
    var pill = $("order-status");
    pill.textContent = st[0];
    pill.className = "status-pill " + st[1];

    // Đổi bàn (A15) làm đổi tên bàn/khu vực và bàn hiện tại của đơn.
    $("order-table").textContent = d.tableName;
    $("order-zone").textContent = d.zoneName;
    app.setAttribute("data-table-id", d.tableId);
    document.title = "Đơn " + d.tableName;

    // RowVersion mới nhất, dùng cho các thao tác cần chống ghi đè (A16).
    app.setAttribute("data-row-version", d.rowVersion);
    // Tổng tiền hàng hiện tại: hộp thoại thanh toán (A16) dùng để tính sẵn giảm giá và tiền thừa. Chỉ để hiển thị, server tính lại.
    app.setAttribute("data-subtotal", d.subTotal);
  }

  // ------------------------------------------------------------ Hộp thoại thêm/sửa món

  function clampQty(n) { return Math.min(99, Math.max(1, n)); }

  function openDialog(config) {
    mode = config.mode;
    window.Ajax.clearErrors(form);
    $("item-modal-title").textContent = config.title;
    $("item-modal-sub").textContent = config.subtitle;
    submitBtn.textContent = config.submitText;
    qtyInput.value = String(config.quantity);
    noteInput.value = config.note;
    modal.showModal();
    (config.focusNote ? noteInput : qtyInput).focus();
    if (!config.focusNote) qtyInput.select();
  }

  function readQuantity() {
    var raw = qtyInput.value.trim();
    if (!/^\d+$/.test(raw)) return null;
    var n = parseInt(raw, 10);
    return n >= 1 && n <= 99 ? n : null;
  }

  function submit(event) {
    event.preventDefault();
    if (submitting || !mode) return;

    var quantity = readQuantity();
    if (quantity === null) {
      window.Ajax.showErrors(form, { quantity: ["Số lượng phải từ 1 đến 99."] });
      return;
    }
    window.Ajax.clearErrors(form);

    var body = { quantity: quantity, note: noteInput.value.trim() || null };
    if (mode.type === "add") body.productId = mode.productId; else body.itemId = mode.itemId;

    submitting = true;
    window.Ajax.setLoading(submitBtn, true, "Đang lưu…");
    window.Ajax.postJson("/pos/orders/" + encodeURIComponent(orderId) + "/items", body)
      .then(function (res) {
        render(res.data);
        window.Ajax.toast(res.message || "Đã lưu.", "success");
        closeDialog();
      })
      .catch(function (err) {
        submitting = false;
        window.Ajax.setLoading(submitBtn, false);
        if (!err || err.status === 401) return;
        if (err.status === 400 && err.data && err.data.errors) {
          window.Ajax.showErrors(form, err.data.errors);
          return;
        }
        // Món ngừng bán (400 không gắn ô), 404, 409: ajax.js đã hiện toast. Đóng hộp thoại; 409 nghĩa là đơn đã đổi
        // ở nơi khác nên tải lại trang để thấy đúng dữ liệu.
        closeDialog();
        if (err.status === 409) setTimeout(function () { location.reload(); }, 1500);
      });
  }

  function closeDialog() {
    if (modal.open) modal.close();
  }

  // ------------------------------------------------------------ Sự kiện

  // Bấm món ở thực đơn (order.js phát sự kiện) → hộp thoại thêm món.
  document.addEventListener("pos:add-product", function (e) {
    if (modal.open) return;
    var d = e.detail;
    openDialog({
      mode: { type: "add", productId: d.productId },
      title: "Thêm món", subtitle: d.name + " • " + P.money(d.price),
      submitText: "Thêm vào đơn", quantity: 1, note: "", focusNote: false
    });
  });

  // Bấm "Sửa" ở một dòng (ủy quyền vì danh sách được vẽ lại).
  linesBox.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-edit-line]");
    if (!btn || !linesBox.contains(btn)) return;
    var li = btn.closest("li");
    openDialog({
      mode: { type: "edit", itemId: Number(btn.getAttribute("data-edit-line")) },
      title: "Sửa món", subtitle: li.getAttribute("data-name") || "",
      submitText: "Lưu thay đổi",
      quantity: clampQty(parseInt(li.getAttribute("data-qty"), 10) || 1),
      note: li.getAttribute("data-note") || "", focusNote: false
    });
  });

  $("qty-minus").addEventListener("click", function () {
    qtyInput.value = String(clampQty((parseInt(qtyInput.value, 10) || 1) - 1));
  });
  $("qty-plus").addEventListener("click", function () {
    qtyInput.value = String(clampQty((parseInt(qtyInput.value, 10) || 0) + 1));
  });

  form.addEventListener("submit", submit);
  modal.querySelectorAll("[data-close]").forEach(function (el) { el.addEventListener("click", closeDialog); });
  modal.addEventListener("close", function () {
    mode = null;
    submitting = false;
    window.Ajax.setLoading(submitBtn, false);
  });

  // ------------------------------------------------------------ Hủy món (A14)

  function closeCancel() {
    if (cancelModal.open) cancelModal.close();
  }

  // Bấm "Hủy" ở một dòng → hỏi lý do (R25: bắt buộc).
  linesBox.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-cancel-line]");
    if (!btn || !linesBox.contains(btn) || cancelModal.open) return;
    var li = btn.closest("li");
    cancelling = { itemId: Number(btn.getAttribute("data-cancel-line")) };
    window.Ajax.clearErrors(cancelForm);
    $("cancel-modal-sub").textContent = (li.getAttribute("data-name") || "") + " × " + (li.getAttribute("data-qty") || "");
    cancelReason.value = "";
    cancelModal.showModal();
    cancelReason.focus();
  });

  cancelForm.addEventListener("submit", function (event) {
    event.preventDefault();
    if (cancelBusy || !cancelling) return;

    var reason = cancelReason.value.trim();
    if (!reason) {
      window.Ajax.showErrors(cancelForm, { reason: ["Vui lòng nhập lý do hủy món."] });
      cancelReason.focus();
      return;
    }
    window.Ajax.clearErrors(cancelForm);

    cancelBusy = true;
    window.Ajax.setLoading(cancelSubmit, true, "Đang hủy…");
    window.Ajax.postJson("/pos/orders/" + encodeURIComponent(orderId) + "/items/" + encodeURIComponent(cancelling.itemId) + "/cancel", { reason: reason })
      .then(function (res) {
        render(res.data);
        window.Ajax.toast(res.message || "Đã hủy món.", "success");
        closeCancel();
      })
      .catch(function (err) {
        cancelBusy = false;
        window.Ajax.setLoading(cancelSubmit, false);
        if (!err || err.status === 401) return;
        if (err.status === 400 && err.data && err.data.errors) {
          window.Ajax.showErrors(cancelForm, err.data.errors);
          return;
        }
        // 404/409: ajax.js đã hiện toast. Đơn hoặc món đã đổi ở nơi khác → đóng hộp thoại và tải lại để thấy dữ liệu đúng.
        closeCancel();
        if (err.status === 409 || err.status === 404) setTimeout(function () { location.reload(); }, 1500);
      });
  });

  cancelModal.querySelectorAll("[data-close]").forEach(function (el) { el.addEventListener("click", closeCancel); });
  cancelModal.addEventListener("close", function () {
    cancelling = null;
    cancelBusy = false;
    window.Ajax.setLoading(cancelSubmit, false);
  });

  // Các bước sau (chuyển bàn, thanh toán) dùng lại render() và orderId.
  P.Order = { id: orderId, render: render };
})();
