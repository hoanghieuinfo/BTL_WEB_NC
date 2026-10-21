// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH11 – Nhân viên. Danh sách: Partial View do server render (chèn innerHTML – Razor đã mã hóa).
// Mọi dữ liệu khác (kể cả mật khẩu khởi tạo) điền bằng textContent / value.
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;
  var URLS = {
    list: "/manage/staff/list",
    create: "/manage/staff/create",
    update: function (id) { return "/manage/staff/" + encodeURIComponent(id) + "/update"; },
    resetPassword: function (id) { return "/manage/staff/" + encodeURIComponent(id) + "/reset-password"; },
    transfer: function (id) { return "/manage/staff/" + encodeURIComponent(id) + "/transfer"; },
    lock: function (id) { return "/manage/staff/" + encodeURIComponent(id) + "/lock"; },
    unlock: function (id) { return "/manage/staff/" + encodeURIComponent(id) + "/unlock"; }
  };

  var state = { q: "", branchId: "", status: "", page: 1 };
  var listEl = $("[data-staff-list]");
  var filterForm = $("[data-filter-form]");
  var requestSeq = 0;

  // ================================================================ Danh sách

  function params() {
    return { q: state.q.trim(), branchId: state.branchId, status: state.status, page: state.page > 1 ? state.page : "" };
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
    state.branchId = search.get("branchId") || "";
    state.status = search.get("status") || "";
    state.page = parseInt(search.get("page"), 10) || 1;
    filterForm.elements.q.value = state.q;
    filterForm.elements.branchId.value = state.branchId;
    filterForm.elements.status.value = state.status;
  }

  async function loadList() {
    var seq = ++requestSeq;
    writeUrl();
    listEl.setAttribute("aria-busy", "true");
    listEl.replaceChildren(document.getElementById("staff-loading-template").content.cloneNode(true));
    try {
      var html = await Ajax.getHtml(URLS.list, params(), { silent: true });
      if (seq !== requestSeq) return;
      listEl.innerHTML = html; // Partial View do server render – Razor đã mã hóa
      var meta = $("[data-list-meta]", listEl);
      if (meta) {
        state.page = parseInt(meta.dataset.page, 10) || 1;
        applyUsage(meta);
      }
      writeUrl();
    } catch (err) {
      if (seq !== requestSeq || err.status === 401) return;
      var box = document.createElement("div");
      box.className = "state state-error";
      var title = document.createElement("p");
      title.className = "state-title";
      title.textContent = "Không tải được danh sách nhân viên";
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

  var timer = null;
  filterForm.elements.q.addEventListener("input", function () {
    clearTimeout(timer);
    timer = setTimeout(function () { setFilter({ q: filterForm.elements.q.value }); }, 300);
  });
  filterForm.elements.branchId.addEventListener("change", function () { setFilter({ branchId: filterForm.elements.branchId.value }); });
  filterForm.elements.status.addEventListener("change", function () { setFilter({ status: filterForm.elements.status.value }); });

  /** Nhãn "x/y nhân viên của gói", nút Thêm và khối mức dùng ở sidebar (R17). */
  function applyUsage(meta) {
    if (!meta.dataset.usageMax) return;
    var count = parseInt(meta.dataset.usageCount, 10);
    var max = parseInt(meta.dataset.usageMax, 10);
    var full = count >= max;
    $all("[data-usage-count]").forEach(function (el) { el.textContent = count; });
    $all("[data-usage-max]").forEach(function (el) { el.textContent = max; });
    $("[data-usage-pill]").classList.toggle("is-full", full);
    $("[data-limit-note]").hidden = !full;
    var add = $('[data-action="add-staff"]');
    add.disabled = full || filterForm.elements.branchId.options.length <= 1;

    var sidebarValue = $('[data-sidebar-usage="staff"]');
    if (sidebarValue) sidebarValue.textContent = count + "/" + max;
    var sidebarBar = $('[data-sidebar-usage-bar="staff"]');
    if (sidebarBar) { sidebarBar.max = max; sidebarBar.value = count; sidebarBar.classList.toggle("is-full", full); }
  }

  function usageNumbers() {
    return {
      count: parseInt($("[data-usage-count]").textContent, 10),
      max: parseInt($("[data-usage-max]").textContent, 10)
    };
  }

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action], [data-page]");
    if (!target || target.disabled) return;
    if (target.dataset.page && listEl.contains(target)) {
      setFilter({ page: parseInt(target.dataset.page, 10) });
      return;
    }
    var row = target.closest("[data-staff-id]");
    var menu = target.closest("details");
    if (menu) menu.open = false;
    switch (target.dataset.action) {
      case "add-staff": openStaffForm(); break;
      case "edit": openEdit(row); break;
      case "reset-password": resetPassword(row); break;
      case "transfer": openTransfer(row); break;
      case "lock": lockStaff(row); break;
      case "unlock": unlockStaff(row); break;
      case "retry": loadList(); break;
      case "copy-password": copyPassword(); break;
      case "clear-filters":
        filterForm.reset();
        setFilter({ q: "", branchId: "", status: "" });
        break;
    }
  });

  // ================================================================ Thêm nhân viên (BM08)

  var staffModal = document.getElementById("staff-modal");
  var staffForm = document.getElementById("staff-form");

  function openStaffForm() {
    var startDate = staffForm.elements.startDate.value;
    staffForm.reset();
    staffForm.elements.startDate.value = startDate; // giữ mặc định hôm nay
    if (state.branchId) staffForm.elements.branchId.value = state.branchId;
    Ajax.clearErrors(staffForm);
    showSummary(null);
    var usage = usageNumbers();
    $("[data-preview-text]", staffForm).textContent = (usage.count + 1) + "/" + usage.max;
    staffModal.showModal();
    staffForm.elements.fullName.focus();
  }

  function showSummary(text) {
    var box = $("[data-form-summary]", staffForm);
    box.hidden = !text;
    $("[data-summary-text]", box).textContent = text || "";
  }

  function staffErrors() {
    var f = staffForm.elements;
    var errors = {};
    var name = f.fullName.value.trim();
    if (name.length < 2) errors.fullName = "Họ tên từ 2 đến 100 ký tự.";
    var email = f.email.value.trim();
    if (!email) errors.email = "Vui lòng nhập email đăng nhập.";
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = "Email không đúng định dạng.";
    if (!/^\d{10}$/.test(f.phone.value.trim())) errors.phone = "Số điện thoại gồm đúng 10 chữ số.";
    if (!f.startDate.value) errors.startDate = "Vui lòng nhập ngày vào làm.";
    if (!f.branchId.value) errors.branchId = "Vui lòng chọn chi nhánh làm việc.";
    return errors;
  }

  staffForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var errors = staffErrors();
    if (Object.keys(errors).length) {
      Ajax.showErrors(staffForm, errors);
      showSummary("Phát hiện " + Object.keys(errors).length + " thông tin chưa chính xác.");
      return;
    }
    showSummary(null);

    var f = staffForm.elements;
    var body = {
      fullName: f.fullName.value.trim(),
      email: f.email.value.trim(),
      phone: f.phone.value.trim(),
      startDate: f.startDate.value,
      branchId: parseInt(f.branchId.value, 10)
    };
    var submit = $("[data-submit]", staffForm);
    ManageUi.setBusy(staffModal, true);
    Ajax.setLoading(submit, true, "Đang tạo...");
    try {
      var result = await Ajax.postJson(URLS.create, body);
      ManageUi.closeDialog(staffModal);
      showPassword(result.data, false);
      loadList();
    } catch (err) {
      if (err.status === 400) {
        var serverErrors = err.data && err.data.errors ? err.data.errors : null;
        if (serverErrors) {
          Ajax.showErrors(staffForm, serverErrors);
          showSummary(serverErrors.email ? "Vui lòng kiểm tra lại địa chỉ email." : "Phát hiện thông tin chưa chính xác.");
        } else {
          showSummary(err.message); // ví dụ đã hết suất nhân viên của gói
        }
      }
    } finally {
      ManageUi.setBusy(staffModal, false);
      Ajax.setLoading(submit, false);
    }
  });

  // ================================================================ Mật khẩu khởi tạo – hiện một lần

  var passwordModal = document.getElementById("password-modal");

  /** isReset: true khi đặt lại mật khẩu, false khi vừa tạo tài khoản. */
  function showPassword(created, isReset) {
    $("[data-password-title]", passwordModal).textContent =
      (isReset ? "Đã đặt lại mật khẩu cho " : "Đã tạo tài khoản cho ") + created.fullName;
    $("[data-password-subtitle]", passwordModal).textContent = isReset
      ? "Mật khẩu cũ đã hết hiệu lực; các phiên đang mở sẽ bị đăng xuất trong vài phút."
      : "Tài khoản nhân viên thu ngân tại chi nhánh " + created.branchName + " đã sẵn sàng.";
    $("[data-password-value]", passwordModal).textContent = created.initialPassword;
    $("[data-password-email]", passwordModal).textContent = created.email;
    $("[data-password-branch]", passwordModal).textContent = created.branchName;
    passwordModal.showModal();
  }

  // Đóng hộp thoại là xóa mật khẩu khỏi trang – không thể mở lại xem.
  passwordModal.addEventListener("close", function () {
    $("[data-password-value]", passwordModal).textContent = "";
  });

  async function copyPassword() {
    var text = $("[data-password-value]", passwordModal).textContent;
    try {
      await navigator.clipboard.writeText(text);
      Ajax.toast("Đã sao chép mật khẩu khởi tạo.", "success");
    } catch (e) {
      Ajax.toast("Trình duyệt không cho sao chép tự động. Hãy chọn và sao chép thủ công.", "warn");
    }
  }

  // ================================================================ Sửa thông tin (họ tên, số điện thoại)

  var editModal = document.getElementById("edit-modal");
  var editForm = document.getElementById("edit-form");
  var editId = null;

  function openEdit(row) {
    editId = row.dataset.staffId;
    editForm.reset();
    Ajax.clearErrors(editForm);
    $("#edit-title").textContent = "Sửa thông tin " + row.dataset.name;
    $("[data-edit-email]", editModal).textContent = row.dataset.email;
    editForm.elements.fullName.value = row.dataset.name;
    editForm.elements.phone.value = row.dataset.phone || "";
    editModal.showModal();
    editForm.elements.fullName.focus();
  }

  editForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var f = editForm.elements;
    var body = { fullName: f.fullName.value.trim(), phone: f.phone.value.trim() };
    var errors = {};
    if (body.fullName.length < 2) errors.fullName = "Họ tên từ 2 đến 100 ký tự.";
    if (!/^\d{10}$/.test(body.phone)) errors.phone = "Số điện thoại gồm đúng 10 chữ số.";
    if (Object.keys(errors).length) { Ajax.showErrors(editForm, errors); return; }

    var submit = $("[data-submit]", editForm);
    ManageUi.setBusy(editModal, true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var result = await Ajax.postJson(URLS.update(editId), body);
      ManageUi.closeDialog(editModal);
      Ajax.toast(result.message, "success");
      loadList();
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(editForm, err.data && err.data.errors ? err.data.errors : { "": err.message });
    } finally {
      ManageUi.setBusy(editModal, false);
      Ajax.setLoading(submit, false);
    }
  });

  // ================================================================ Đặt lại mật khẩu

  var confirmNote = $("[data-confirm-note]", document.getElementById("confirm-modal"));

  function resetPassword(row) {
    var name = row.dataset.name;
    confirmNote.textContent = "Mật khẩu mới chỉ hiện một lần. Gửi cho nhân viên qua kênh riêng.";
    ManageUi.confirmAction({
      title: "Đặt lại mật khẩu cho " + name + "?",
      message: "Mật khẩu hiện tại sẽ hết hiệu lực ngay. " + name.trim().split(/\s+/).pop()
        + " bị đăng xuất trong vài phút và phải đổi mật khẩu ở lần đăng nhập tới.",
      okText: "Đặt lại mật khẩu",
      busyText: "Đang đặt lại...",
      onConfirm: async function () {
        var result = await Ajax.postJson(URLS.resetPassword(row.dataset.staffId), {});
        showPassword(result.data, true); // mở chồng lên hộp xác nhận; hộp xác nhận tự đóng ngay sau đó
        loadList();
      }
    });
  }

  // ================================================================ Chuyển chi nhánh

  var transferModal = document.getElementById("transfer-modal");
  var transferForm = document.getElementById("transfer-form");
  var transferId = null;

  function initials(name) {
    var parts = name.trim().split(/\s+/);
    return (parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : parts[0][0]).toUpperCase();
  }

  function openTransfer(row) {
    transferId = row.dataset.staffId;
    var name = row.dataset.name;
    var shortName = name.trim().split(/\s+/).pop();
    Ajax.clearErrors(transferForm);
    $("#transfer-title").textContent = "Chuyển " + name + " sang chi nhánh khác";
    $("[data-transfer-initials]", transferModal).textContent = initials(name);
    $("[data-transfer-name]", transferModal).textContent = name;
    $("[data-transfer-contact]", transferModal).textContent = row.dataset.email + (row.dataset.phone ? " • " + row.dataset.phone : "");
    $("[data-transfer-current]", transferModal).textContent = row.dataset.branchName || "Chưa phân công";
    $("[data-transfer-note]", transferModal).textContent =
      shortName + " sẽ bị đăng xuất trong vài phút và chỉ bán hàng được ở chi nhánh mới.";

    // Mặc định chọn chi nhánh khác chi nhánh hiện tại.
    var select = transferForm.elements.branchId;
    var other = Array.prototype.find.call(select.options, function (o) { return o.value !== row.dataset.branchId; });
    select.value = other ? other.value : select.options[0].value;
    transferModal.showModal();
    select.focus();
  }

  transferForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var submit = $("[data-submit]", transferForm);
    ManageUi.setBusy(transferModal, true);
    Ajax.setLoading(submit, true, "Đang chuyển...");
    try {
      var result = await Ajax.postJson(URLS.transfer(transferId), { branchId: parseInt(transferForm.elements.branchId.value, 10) });
      ManageUi.closeDialog(transferModal);
      Ajax.toast(result.message, "success");
      loadList();
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(transferForm, err.data && err.data.errors ? err.data.errors : { "": err.message });
    } finally {
      ManageUi.setBusy(transferModal, false);
      Ajax.setLoading(submit, false);
    }
  });

  // ================================================================ Khóa / mở khóa

  function lockStaff(row) {
    var name = row.dataset.name;
    var shortName = name.trim().split(/\s+/).pop();
    confirmNote.textContent = "Chủ quán có thể mở khóa lại tài khoản bất kỳ lúc nào ở bộ lọc trạng thái \"Đã nghỉ (đã khóa)\".";
    ManageUi.confirmAction({
      title: "Khóa tài khoản " + name + "?",
      message: shortName + " sẽ bị đăng xuất khỏi mọi thiết bị trong vài phút và không đăng nhập lại được. Hóa đơn "
        + shortName + " đã lập vẫn được giữ trong báo cáo.",
      okText: "Khóa tài khoản",
      busyText: "Đang khóa...",
      onConfirm: async function () {
        var result = await Ajax.postJson(URLS.lock(row.dataset.staffId), {});
        Ajax.toast(result.message, "success");
        await loadList();
      }
    });
  }

  async function unlockStaff(row) {
    var button = $('[data-action="unlock"]', row);
    Ajax.setLoading(button, true, "Đang mở...");
    try {
      var result = await Ajax.postJson(URLS.unlock(row.dataset.staffId), {});
      Ajax.toast(result.message, "success");
      await loadList();
    } catch (err) {
      if (err.status === 400) Ajax.toast(err.message, "error"); // ví dụ đã hết suất nhân viên
      Ajax.setLoading(button, false);
    }
  }

  readUrl();
  loadList();
})();
