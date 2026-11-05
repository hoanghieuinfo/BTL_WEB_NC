// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH06 – Quản lý tenant: tải danh sách (A03), kích hoạt/mở khóa, khóa/từ chối có lý do (A04), đổi gói (A05), gia hạn (A29).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var container = document.getElementById("tenants-container");
  var filter = document.getElementById("filter-form");
  var state = { page: 1 };

  // ------------------------------------------------------------ Danh sách (A03)

  // Từ MH05 sang: ?status=0 (quán chờ duyệt) hoặc ?expired=true (quán hết hạn gói).
  var query = new URLSearchParams(location.search);
  if (query.has("status")) filter.elements.namedItem("status").value = query.get("status");
  if (query.get("expired") === "true") filter.elements.namedItem("status").value = "expired";

  function buildParams() {
    var status = filter.elements.namedItem("status").value;
    var expired = status === "expired"; // "Hết hạn gói" không phải TenantStatus → tham số riêng
    return {
      page: state.page,
      keyword: filter.elements.namedItem("keyword").value.trim(),
      status: expired ? "" : status,
      planId: filter.elements.namedItem("planId").value,
      expired: expired ? "true" : ""
    };
  }

  /** Trạng thái lỗi dựng bằng DOM (textContent), có nút Thử lại. */
  function renderError() {
    var card = document.createElement("div");
    card.className = "card";
    var box = document.createElement("div");
    box.className = "state state-error";
    box.setAttribute("role", "alert");
    var title = document.createElement("span");
    title.className = "state-title";
    title.textContent = "Không tải được danh sách quán";
    var text = document.createElement("span");
    text.textContent = "Kiểm tra kết nối rồi thử lại.";
    var retry = document.createElement("button");
    retry.type = "button";
    retry.className = "btn";
    retry.dataset.action = "retry";
    retry.textContent = "Thử lại";
    box.append(title, text, retry);
    card.appendChild(box);
    container.replaceChildren(card);
  }

  async function loadTenants(page) {
    state.page = page || 1;
    container.setAttribute("aria-busy", "true");
    container.classList.add("is-loading");
    try {
      // Partial View do server render (Razor đã mã hóa dữ liệu) → được chèn bằng innerHTML (CLAUDE.md mục 6).
      container.innerHTML = await Ajax.getHtml("/system/tenants/list", buildParams(), { silent: true });
    } catch (err) {
      if (err.status !== 401) renderError();
    } finally {
      container.setAttribute("aria-busy", "false");
      container.classList.remove("is-loading");
    }
  }

  var debounce;
  filter.addEventListener("input", function (event) {
    if (event.target.name !== "keyword") return;
    clearTimeout(debounce);
    debounce = setTimeout(function () { loadTenants(1); }, 300);
  });
  filter.addEventListener("change", function (event) {
    if (event.target.name !== "keyword") loadTenants(1);
  });
  filter.addEventListener("reset", function () { setTimeout(function () { loadTenants(1); }); });
  filter.addEventListener("submit", function (event) { event.preventDefault(); loadTenants(1); });

  // ------------------------------------------------------------ Hộp thoại

  var reasonModal = document.getElementById("reason-modal");
  var reasonForm = document.getElementById("reason-form");
  var planModal = document.getElementById("plan-modal");
  var planForm = document.getElementById("plan-form");
  var expiryModal = document.getElementById("expiry-modal");
  var expiryForm = document.getElementById("expiry-form");
  var current = { id: null, status: 2 };

  function openDialog(modal, form, btn) {
    current.id = btn.dataset.id;
    form.reset();
    Ajax.clearErrors(form);
    $("[data-tenant-name]", modal).textContent = btn.dataset.name;
    modal.showModal();
  }

  /** Ngày mai theo giờ máy người dùng, dạng yyyy-MM-dd cho input type=date (server vẫn kiểm tra lại). */
  function tomorrowIso() {
    var d = new Date();
    d.setDate(d.getDate() + 1);
    return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
  }

  document.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-action]");
    if (!btn) return;

    switch (btn.dataset.action) {
      case "retry":
        loadTenants(state.page);
        break;
      case "page":
        loadTenants(parseInt(btn.dataset.page, 10));
        break;
      case "activate": // Kích hoạt (chờ → hoạt động) hoặc Mở khóa (khóa → hoạt động)
        ManageUi.confirmAction({
          title: btn.textContent.trim() + " quán",
          message: btn.textContent.trim() + " quán \"" + btn.dataset.name + "\"? Tài khoản của quán sẽ đăng nhập được ngay.",
          okText: btn.textContent.trim(),
          busyText: "Đang xử lý...",
          onConfirm: async function () {
            var res = await Ajax.postJson("/system/tenants/" + btn.dataset.id + "/status", { status: 1 });
            Ajax.toast(res.message, "success");
            loadTenants(state.page);
          }
        });
        break;
      case "lock":
      case "reject":
        var reject = btn.dataset.action === "reject";
        current.status = reject ? 3 : 2;
        $("[data-reason-title]", reasonModal).textContent = reject ? "Từ chối đăng ký" : "Khóa hoạt động quán";
        $("[data-reason-label]", reasonModal).textContent = reject ? "Lý do từ chối" : "Lý do khóa quán";
        $("[data-lock-warning]", reasonModal).hidden = reject;
        $("[data-submit]", reasonForm).textContent = reject ? "Từ chối" : "Khóa quán";
        openDialog(reasonModal, reasonForm, btn);
        break;
      case "plan":
        openDialog(planModal, planForm, btn);
        planForm.elements.namedItem("planId").value = btn.dataset.plan;
        break;
      case "expiry":
        openDialog(expiryModal, expiryForm, btn);
        $("[data-expiry-current]", expiryModal).textContent = btn.dataset.expiryText || "Chưa đặt";
        expiryForm.elements.namedItem("planExpiresAt").min = tomorrowIso();
        break;
    }
  });

  /** Gửi form trong hộp thoại: khóa form khi đang lưu, lỗi 400 hiện tại từng ô (data.errors) hoặc dòng lỗi chung. */
  function handleSubmit(modal, form, buildRequest, busyText) {
    form.addEventListener("submit", async function (event) {
      event.preventDefault();
      var request = buildRequest();
      if (request.errors) { Ajax.showErrors(form, request.errors); return; }

      var submit = $("[data-submit]", form);
      ManageUi.setBusy(modal, true);
      Ajax.setLoading(submit, true, busyText);
      try {
        var res = await Ajax.postJson(request.url, request.body);
        ManageUi.closeDialog(modal);
        Ajax.toast(res.message, "success");
        loadTenants(state.page);
      } catch (err) {
        if (err.status === 400) Ajax.showErrors(form, (err.data && err.data.errors) || { "": [err.message] });
      } finally {
        ManageUi.setBusy(modal, false);
        Ajax.setLoading(submit, false);
      }
    });
  }

  handleSubmit(reasonModal, reasonForm, function () {
    var reason = reasonForm.elements.namedItem("lockReason").value.trim();
    if (!reason) {
      return { errors: { lockReason: [current.status === 3 ? "Vui lòng nhập lý do từ chối." : "Vui lòng nhập lý do khóa quán."] } };
    }
    return { url: "/system/tenants/" + current.id + "/status", body: { status: current.status, lockReason: reason } };
  }, "Đang lưu...");

  handleSubmit(planModal, planForm, function () {
    return {
      url: "/system/tenants/" + current.id + "/plan",
      body: { planId: parseInt(planForm.elements.namedItem("planId").value, 10) }
    };
  }, "Đang lưu...");

  handleSubmit(expiryModal, expiryForm, function () {
    var date = expiryForm.elements.namedItem("planExpiresAt").value;
    if (!date) return { errors: { planExpiresAt: ["Vui lòng chọn ngày hết hạn mới."] } };
    return { url: "/system/tenants/" + current.id + "/expiry", body: { planExpiresAt: date } };
  }, "Đang gia hạn...");

  loadTenants(1);
})();
