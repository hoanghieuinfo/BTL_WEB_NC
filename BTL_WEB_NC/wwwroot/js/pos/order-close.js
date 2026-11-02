// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH15 – Tạm tính (A29) và hủy đơn (P05 ngoại lệ, R25 – A30).
//  - Tạm tính: POST /pos/orders/{id}/provisional → bàn sang Chờ thanh toán, rồi mở trang in phiếu tạm tính (MH16).
//  - Hủy đơn: hộp thoại lý do. Đơn đã có món (còn dòng chưa hủy) thì bắt buộc lý do; đơn chưa có món hủy luôn. Xong quay lại sơ đồ bàn.
// Nạp sau order-panel.js (dùng P.Order.render). Dựng DOM bằng textContent, không dùng innerHTML.
(function () {
  "use strict";

  var app = document.getElementById("order-app");
  if (!app || app.getAttribute("data-open") !== "true") return;

  var P = window.Pos, $ = P.$;
  if (!P.Order) return;

  var orderId = P.Order.id;
  var provBtn = $("provisional-open");
  var openBtn = $("order-cancel-open");
  var modal = $("cancel-order-modal");
  var form = $("cancel-order-form");
  var reasonInput = $("cancel-order-reason");
  var submitBtn = $("cancel-order-submit");

  var provBusy = false;
  var cancelBusy = false;

  // Đơn có món chưa hủy không? Đọc từ danh sách đang hiển thị (đã được render() cập nhật sau mỗi thao tác).
  // Chỉ để gợi ý giao diện; server luôn kiểm tra lại từ CSDL.
  function hasActiveLines() {
    return document.querySelectorAll("#order-lines li.line:not(.is-cancelled)").length > 0;
  }

  function reload(delay) { setTimeout(function () { location.reload(); }, delay || 1500); }

  // ------------------------------------------------------------ Tạm tính (A29)

  provBtn.addEventListener("click", function () {
    if (provBusy) return;
    if (!hasActiveLines()) {
      window.Ajax.toast("Đơn chưa có món nào nên chưa thể tạm tính.", "warn");
      return;
    }
    provBusy = true;
    window.Ajax.setLoading(provBtn, true, "Đang tạm tính…");
    window.Ajax.postJson("/pos/orders/" + encodeURIComponent(orderId) + "/provisional", {})
      .then(function (res) {
        P.Order.render(res.data);
        window.Ajax.toast(res.message || "Đã tạm tính.", "success");
        // MH16 (làm ở bước 9): phiếu tạm tính của đơn này.
        location.assign("/pos/orders/" + encodeURIComponent(orderId) + "/print");
      })
      .catch(function (err) {
        provBusy = false;
        window.Ajax.setLoading(provBtn, false);
        if (!err || err.status === 401) return;
        // 400/404/409: ajax.js đã hiện toast. 404/409 nghĩa là đơn đã đổi ở nơi khác → tải lại để thấy dữ liệu đúng.
        if (err.status === 409 || err.status === 404) reload();
      });
  });

  // ------------------------------------------------------------ Hủy đơn (A30)

  function closeModal() {
    if (modal.open) modal.close();
  }

  openBtn.addEventListener("click", function () {
    if (modal.open) return;
    var needReason = hasActiveLines();
    window.Ajax.clearErrors(form);
    reasonInput.value = "";
    reasonInput.required = needReason;
    $("cancel-order-star").hidden = !needReason;
    $("cancel-order-hint").textContent = needReason
      ? "Đơn đã có món nên cần ghi lý do. Đơn đóng lại và bàn về Trống, không hoàn tác được."
      : "Đơn chưa có món nào, có thể hủy luôn (lý do không bắt buộc). Bàn sẽ về Trống.";
    modal.showModal();
    reasonInput.focus();
  });

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    if (cancelBusy) return;

    var reason = reasonInput.value.trim();
    if (hasActiveLines() && !reason) {
      window.Ajax.showErrors(form, { reason: ["Vui lòng nhập lý do hủy đơn."] });
      reasonInput.focus();
      return;
    }
    window.Ajax.clearErrors(form);

    cancelBusy = true;
    window.Ajax.setLoading(submitBtn, true, "Đang hủy…");
    window.Ajax.postJson("/pos/orders/" + encodeURIComponent(orderId) + "/cancel", { reason: reason || null })
      .then(function (res) {
        window.Ajax.toast(res.message || "Đã hủy đơn.", "success");
        closeModal();
        setTimeout(function () { location.assign("/pos"); }, 800);
      })
      .catch(function (err) {
        cancelBusy = false;
        window.Ajax.setLoading(submitBtn, false);
        if (!err || err.status === 401) return;
        if (err.status === 400 && err.data && err.data.errors) {
          window.Ajax.showErrors(form, err.data.errors);
          return;
        }
        // 404/409: ajax.js đã hiện toast. Đơn đã đổi ở nơi khác (đã thanh toán, đã hủy…) → đóng hộp thoại và tải lại.
        closeModal();
        if (err.status === 409 || err.status === 404) reload();
      });
  });

  // Quay lại bằng nút Back từ trang in: trang lấy từ bộ nhớ đệm có thể còn nút đang khóa → tải lại để đúng trạng thái.
  window.addEventListener("pageshow", function (e) { if (e.persisted) location.reload(); });

  modal.querySelectorAll("[data-close]").forEach(function (el) { el.addEventListener("click", closeModal); });
  modal.addEventListener("close", function () {
    cancelBusy = false;
    window.Ajax.setLoading(submitBtn, false);
  });
})();
