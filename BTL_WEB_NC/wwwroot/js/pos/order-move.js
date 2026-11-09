// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH15 – Chuyển bàn và gộp bàn (A15, R25). Danh sách bàn lấy từ A10 rồi lọc ở trình duyệt; server kiểm tra lại tất cả.
//  - Chuyển: chọn bàn Trống → POST /pos/orders/{id}/transfer, đơn được vẽ lại ngay tại trang.
//  - Gộp: chọn bàn đang có đơn → POST /pos/orders/{id}/merge, rồi chuyển sang đơn đích (đơn này đã bị hủy).
// Nạp sau order-panel.js (dùng P.Order.render). Dựng DOM bằng createElement/textContent, không dùng innerHTML.
(function () {
  "use strict";

  var app = document.getElementById("order-app");
  if (!app || app.getAttribute("data-open") !== "true") return;

  var P = window.Pos, h = P.h, $ = P.$;
  if (!P.Order) return;

  var openBtn = $("move-open");
  var modal = $("move-modal");
  var form = $("move-form");
  var zonesBox = $("move-zones");
  var loading = $("move-loading");
  var empty = $("move-empty");
  var errorBox = $("move-error");
  var submitBtn = $("move-submit");

  var mode = "transfer";      // "transfer" | "merge"
  var zones = [];             // dữ liệu A10 gần nhất
  var picked = null;          // { id, name }
  var seq = 0;                // đánh số request: kết quả đến muộn của request cũ bị bỏ qua
  var submitting = false;

  var TEXT = {
    transfer: {
      hint: "Chọn một bàn đang trống. Đơn và các món được giữ nguyên, bàn hiện tại sẽ trống.",
      empty: "Hiện không có bàn trống nào.",
      submit: "Chuyển bàn"
    },
    merge: {
      hint: "Chọn bàn đang có khách. Toàn bộ món của đơn này sẽ chuyển sang đơn của bàn đó, đơn này được đóng lại và không hoàn tác được.",
      empty: "Hiện không có bàn nào khác đang có khách.",
      submit: "Gộp bàn"
    }
  };

  function show(el, on) { el.hidden = !on; }
  function currentTableId() { return Number(app.getAttribute("data-table-id")); }
  function hasOrder(t) { return t.orderId !== null && t.orderId !== undefined; }

  // ------------------------------------------------------------ Vẽ danh sách bàn đích

  function eligible(t) {
    if (t.id === currentTableId()) return false;
    return mode === "transfer" ? t.status === P.TableStatus.Empty : hasOrder(t);
  }

  function targetCard(t) {
    var label = P.statusText[t.status] || "";
    var showTotal = mode === "merge" && t.orderTotal !== null && t.orderTotal !== undefined;
    var selected = picked !== null && picked.id === t.id;
    var button = h("button", {
      type: "button", class: "table-card is-action st-" + t.status + (selected ? " is-selected" : ""),
      "data-target": t.id, "data-name": t.name, "aria-pressed": selected ? "true" : "false",
      "aria-label": t.name + ", " + label + (showTotal ? ", " + P.money(t.orderTotal) : "")
    }, [
      h("span", { class: "t-name", text: t.name }),
      h("span", { class: "t-state", text: label }),
      showTotal ? h("span", { class: "t-total", text: P.money(t.orderTotal) }) : null
    ]);
    return button;
  }

  function renderTargets() {
    zonesBox.textContent = "";
    var count = 0;
    zones.forEach(function (z) {
      var tables = (z.tables || []).filter(eligible);
      if (!tables.length) return;
      count += tables.length;
      zonesBox.appendChild(h("section", { class: "move-zone" }, [
        h("h3", { text: z.name }),
        h("div", { class: "move-grid" }, tables.map(targetCard))
      ]));
    });
    $("move-empty-title").textContent = TEXT[mode].empty;
    show(zonesBox, count > 0);
    show(empty, count === 0);
  }

  function updatePicked() {
    submitBtn.disabled = picked === null || submitting;
    $("move-picked").textContent = picked ? "Đã chọn: " + picked.name : "";
  }

  function setMode(next) {
    mode = next;
    picked = null;
    Array.prototype.forEach.call(modal.querySelectorAll("[data-move-mode]"), function (el) {
      var active = el.getAttribute("data-move-mode") === mode;
      el.classList.toggle("is-active", active);
      el.setAttribute("aria-pressed", active ? "true" : "false");
    });
    $("move-hint").textContent = TEXT[mode].hint;
    submitBtn.textContent = TEXT[mode].submit;
    window.Ajax.clearErrors(form);
    renderTargets();
    updatePicked();
  }

  // ------------------------------------------------------------ Tải sơ đồ bàn (A10)

  function load() {
    var mine = ++seq;
    show(errorBox, false);
    show(empty, false);
    show(zonesBox, false);
    show(loading, true);

    return window.Ajax.getJson("/pos/tables/status", null, { silent: true })
      .then(function (res) {
        if (mine !== seq) return;
        show(loading, false);
        zones = res.data || [];
        // Bàn đã chọn có thể vừa đổi trạng thái ở máy khác: bỏ chọn nếu không còn hợp lệ.
        if (picked && !zones.some(function (z) { return (z.tables || []).some(function (t) { return t.id === picked.id && eligible(t); }); })) {
          picked = null;
        }
        renderTargets();
        updatePicked();
      })
      .catch(function (err) {
        if (mine !== seq || (err && err.status === 401)) return;
        show(loading, false);
        show(errorBox, true);
      });
  }

  // ------------------------------------------------------------ Gửi

  function submit(event) {
    event.preventDefault();
    if (submitting || picked === null) return;

    var isMerge = mode === "merge";
    var url = "/pos/orders/" + encodeURIComponent(P.Order.id) + (isMerge ? "/merge" : "/transfer");

    submitting = true;
    updatePicked();
    window.Ajax.clearErrors(form);
    window.Ajax.setLoading(submitBtn, true, isMerge ? "Đang gộp…" : "Đang chuyển…");

    window.Ajax.postJson(url, { targetTableId: picked.id })
      .then(function (res) {
        window.Ajax.toast(res.message || "Đã lưu.", "success");
        if (isMerge) {
          // Đơn này đã bị hủy: sang xem đơn đích (Id là số nguyên từ server nên nối vào URL an toàn).
          setTimeout(function () { location.href = "/pos/orders/" + encodeURIComponent(res.data.targetOrderId); }, 700);
          return;
        }
        P.Order.render(res.data);
        closeDialog();
      })
      .catch(function (err) {
        submitting = false;
        window.Ajax.setLoading(submitBtn, false);
        submitBtn.textContent = TEXT[mode].submit;
        updatePicked();
        if (!err || err.status === 401) return;
        if (err.status === 400 && err.data && err.data.errors) {
          window.Ajax.showErrors(form, err.data.errors);
          return;
        }
        // 409 (bàn đích vừa có khách / vừa thanh toán…) hoặc 404 (bàn bị xóa): ajax.js đã hiện toast.
        // Giữ hộp thoại, bỏ chọn và tải lại sơ đồ để chọn bàn khác. Đơn đã đóng hoặc không còn → tải lại cả trang.
        picked = null;
        if (err.status === 404) { closeDialog(); setTimeout(function () { location.reload(); }, 1500); return; }
        load();
      });
  }

  function closeDialog() {
    if (modal.open) modal.close();
  }

  // ------------------------------------------------------------ Sự kiện

  openBtn.addEventListener("click", function () {
    if (modal.open) return;
    $("move-from").textContent = $("order-table").textContent;
    picked = null;
    setMode("transfer");
    modal.showModal();
    load();
  });

  modal.addEventListener("click", function (event) {
    var modeBtn = event.target.closest("[data-move-mode]");
    if (modeBtn && modal.contains(modeBtn)) { setMode(modeBtn.getAttribute("data-move-mode")); return; }

    var card = event.target.closest("[data-target]");
    if (card && zonesBox.contains(card)) {
      picked = { id: Number(card.getAttribute("data-target")), name: card.getAttribute("data-name") || "" };
      window.Ajax.clearErrors(form);
      renderTargets();
      updatePicked();
    }
  });

  $("move-retry").addEventListener("click", load);
  form.addEventListener("submit", submit);
  modal.querySelectorAll("[data-close]").forEach(function (el) { el.addEventListener("click", closeDialog); });
  modal.addEventListener("close", function () {
    seq++;                       // bỏ kết quả tải đang bay
    picked = null;
    submitting = false;
    window.Ajax.setLoading(submitBtn, false);
  });
})();
