// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH14 – Sơ đồ bàn (R22): vẽ bàn theo khu vực và trạng thái, tự cập nhật mỗi 10 giây bằng AJAX (A10).
// Bấm bàn Trống → hộp thoại số khách → A12 mở đơn → chuyển tới MH15; bấm bàn có đơn → vào thẳng đơn (R23).
(function () {
  "use strict";

  var P = window.Pos, h = P.h, $ = P.$;
  var REFRESH_MS = 10000;

  var zonesBox = $("tables-zones");
  var loading = $("tables-loading");
  var empty = $("tables-empty");
  var errorBox = $("tables-error");
  var lastJson = "";

  var modal = $("open-modal");
  var form = $("open-form");
  var guestsInput = $("open-guests");
  var submitBtn = $("open-submit");
  var openingTableId = null;   // bàn đang mở trong hộp thoại
  var submitting = false;

  function show(el, on) { el.hidden = !on; }

  function hasOrder(t) { return t.orderId !== null && t.orderId !== undefined; }

  function tableCard(t) {
    var label = P.statusText[t.status] || "";
    var detail = null;
    if (t.orderTotal !== null && t.orderTotal !== undefined) {
      detail = h("span", { class: "t-total", text: P.money(t.orderTotal) });
    } else if (t.seats) {
      detail = h("span", { class: "t-seats", text: t.seats + " chỗ" });
    }
    var spoken = t.name + ", " + label +
      (t.orderTotal !== null && t.orderTotal !== undefined ? ", " + P.money(t.orderTotal) : "");
    var children = [
      h("span", { class: "t-name", text: t.name }),
      h("span", { class: "t-state", text: label }),
      detail
    ];

    // Màu luôn đi kèm chữ trạng thái, không chỉ dựa vào màu (R34).
    var cls = "table-card st-" + t.status;
    if (t.status === P.TableStatus.Empty) {
      // Bàn trống: nút mở hộp thoại. data-* chỉ chứa Id/tên để đọc lại khi bấm.
      return h("button", {
        type: "button", class: cls + " is-action", role: "listitem",
        "data-open-table": t.id, "data-table-name": t.name,
        "aria-label": spoken + ". Bấm để mở đơn."
      }, children);
    }
    if (hasOrder(t)) {
      // Bàn có đơn: liên kết tới MH15. Id là số nguyên từ server nên nối vào URL an toàn.
      return h("a", {
        class: cls + " is-action", role: "listitem",
        href: "/pos/orders/" + encodeURIComponent(t.orderId),
        "aria-label": spoken + ". Bấm để xem đơn."
      }, children);
    }
    // Trạng thái bàn không khớp đơn (dữ liệu lệch): không cho bấm, tránh dẫn tới trang lỗi.
    return h("div", { class: cls, role: "listitem", "aria-label": spoken }, children);
  }

  function render(zones) {
    // Dữ liệu không đổi thì không vẽ lại để khỏi mất tiêu điểm bàn phím.
    var json = JSON.stringify(zones);
    if (json === lastJson) return;
    lastJson = json;

    zonesBox.textContent = "";
    var any = false;
    zones.forEach(function (zone) {
      if (!zone.tables.length) return;
      any = true;
      zonesBox.appendChild(h("section", { class: "zone", "aria-label": "Khu vực " + zone.name }, [
        h("h2", { text: zone.name }),
        h("div", { class: "table-grid", role: "list" }, zone.tables.map(tableCard))
      ]));
    });
    show(empty, !any);
  }

  function load(first) {
    // Lần làm mới nền im lặng: lỗi mạng thoáng qua không bật toast mỗi 10 giây.
    return window.Ajax.getJson("/pos/tables/status", null, { silent: !first })
      .then(function (res) {
        show(loading, false);
        show(errorBox, false);
        render(res.data || []);
      })
      .catch(function (err) {
        if (err && err.status === 401) return;   // ajax.js đang chuyển về trang đăng nhập
        show(loading, false);
        // Lỗi lúc làm mới nền thì giữ sơ đồ cũ; chưa có sơ đồ nào thì báo lỗi kèm nút thử lại.
        if (first || !zonesBox.children.length) show(errorBox, true);
      });
  }

  function refresh() {
    if (!document.hidden && !modal.open) load(false);   // đang nhập số khách thì không vẽ lại nền
  }

  // ------------------------------------------------------------ Hộp thoại mở bàn (A12)

  function openModal(tableId, name) {
    openingTableId = tableId;
    window.Ajax.clearErrors(form);
    form.reset();
    $("open-modal-title").textContent = "Mở " + name;   // textContent: tên bàn do chủ quán nhập
    modal.showModal();
    guestsInput.focus();
  }

  function closeModal() {
    if (modal.open) modal.close();
  }

  /** Số khách để trống → null. Có nhập thì phải là số nguyên 1–99 (khớp OpenOrderModel). */
  function readGuests() {
    var raw = guestsInput.value.trim();
    if (raw === "") return { ok: true, value: null };
    if (!/^\d+$/.test(raw)) return { ok: false };
    var n = parseInt(raw, 10);
    return n >= 1 && n <= 99 ? { ok: true, value: n } : { ok: false };
  }

  function submitOpen(event) {
    event.preventDefault();
    if (submitting || openingTableId === null) return;

    var guests = readGuests();
    if (!guests.ok) {
      window.Ajax.showErrors(form, { guestCount: ["Số khách phải từ 1 đến 99."] });
      return;
    }
    window.Ajax.clearErrors(form);

    submitting = true;
    window.Ajax.setLoading(submitBtn, true, "Đang mở…");
    // BranchId không gửi lên: server lấy từ claim (CLAUDE.md – Đa tenant).
    window.Ajax.postJson("/pos/orders/open", { tableId: openingTableId, guestCount: guests.value })
      .then(function (res) {
        // Giữ nút khóa tới khi trang mới tải xong để tránh bấm lần hai.
        location.href = "/pos/orders/" + encodeURIComponent(res.data.orderId);
      })
      .catch(function (err) {
        submitting = false;
        window.Ajax.setLoading(submitBtn, false);
        if (!err || err.status === 401) return;
        if (err.status === 400) {
          window.Ajax.showErrors(form, err.data && err.data.errors);
          return;
        }
        // 404 (bàn không còn) và 409 (bàn vừa có khách): ajax.js đã hiện toast; đóng hộp thoại, tải lại sơ đồ.
        closeModal();
        load(false);
      });
  }

  // ------------------------------------------------------------ Sự kiện

  // Ủy quyền sự kiện: thẻ bàn được vẽ lại mỗi khi dữ liệu đổi.
  zonesBox.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-open-table]");
    if (!btn || !zonesBox.contains(btn)) return;
    openModal(parseInt(btn.getAttribute("data-open-table"), 10), btn.getAttribute("data-table-name") || "bàn");
  });

  form.addEventListener("submit", submitOpen);
  modal.querySelectorAll("[data-close]").forEach(function (el) {
    el.addEventListener("click", closeModal);
  });
  modal.addEventListener("close", function () {
    openingTableId = null;
    submitting = false;
    window.Ajax.setLoading(submitBtn, false);
  });

  $("tables-retry").addEventListener("click", function () {
    show(errorBox, false);
    show(loading, true);
    load(true);
  });

  document.addEventListener("visibilitychange", refresh);   // quay lại tab thì cập nhật ngay
  load(true);
  setInterval(refresh, REFRESH_MS);
})();
