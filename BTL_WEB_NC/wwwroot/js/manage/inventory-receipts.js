// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH12 – Tab Phiếu nhập. Danh sách và chi tiết phiếu là Partial View do server render (chèn innerHTML – Razor đã mã hóa).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var URLS = {
    list: "/manage/inventory/receipts/list",
    detail: function (id) { return "/manage/inventory/receipts/" + encodeURIComponent(id); }
  };
  var FLASH_KEY = "manage.inventory.flash";

  var listEl = $("[data-receipt-list]");
  var filterForm = $("[data-filter-form]");
  var defaultMonth = filterForm.elements.month.dataset.defaultMonth;
  var state = { branchId: "", month: defaultMonth, q: "", page: 1 };
  var requestSeq = 0;

  function params() {
    // month rỗng gửi "all" để server hiểu là mọi thời gian (không phải tháng mặc định).
    return { branchId: state.branchId, month: state.month || "all", q: state.q.trim(), page: state.page > 1 ? state.page : "" };
  }

  function writeUrl() {
    var search = new URLSearchParams();
    var p = params();
    Object.keys(p).forEach(function (k) {
      if (p[k] && !(k === "month" && p[k] === defaultMonth)) search.set(k, p[k]);
    });
    var text = search.toString();
    history.replaceState(null, "", location.pathname + (text ? "?" + text : ""));
  }

  function readUrl() {
    var search = new URLSearchParams(location.search);
    state.branchId = search.get("branchId") || "";
    var month = search.get("month");
    state.month = month === "all" ? "" : (month || defaultMonth);
    state.q = search.get("q") || "";
    state.page = parseInt(search.get("page"), 10) || 1;
    filterForm.elements.branchId.value = state.branchId;
    if (filterForm.elements.branchId.value !== state.branchId) state.branchId = "";
    filterForm.elements.month.value = state.month;
    filterForm.elements.q.value = state.q;
  }

  async function loadList() {
    var seq = ++requestSeq;
    writeUrl();
    listEl.setAttribute("aria-busy", "true");
    listEl.replaceChildren(document.getElementById("receipt-loading-template").content.cloneNode(true));
    try {
      var html = await Ajax.getHtml(URLS.list, params(), { silent: true });
      if (seq !== requestSeq) return;
      listEl.innerHTML = html; // Partial View do server render – Razor đã mã hóa
      var meta = $("[data-list-meta]", listEl);
      if (meta) {
        state.page = parseInt(meta.dataset.page, 10) || 1;
        $("[data-total-label]").textContent = meta.dataset.totalLabel;
        $("[data-total-value]").textContent = meta.dataset.totalValue;
        $("[data-total-count]").textContent = meta.dataset.totalCount;
      }
      writeUrl();
    } catch (err) {
      if (seq !== requestSeq || err.status === 401) return;
      var box = document.createElement("div");
      box.className = "state state-error";
      var title = document.createElement("p");
      title.className = "state-title";
      title.textContent = "Không tải được danh sách phiếu nhập";
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
  filterForm.elements.branchId.addEventListener("change", function () { setFilter({ branchId: filterForm.elements.branchId.value }); });
  filterForm.elements.month.addEventListener("change", function () { setFilter({ month: filterForm.elements.month.value }); });

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action], [data-page]");
    if (!target || target.disabled) return;
    if (target.dataset.page && listEl.contains(target)) {
      setFilter({ page: parseInt(target.dataset.page, 10) });
      return;
    }
    switch (target.dataset.action) {
      case "view": openDetail(target.closest("[data-receipt-id]")); break;
      case "retry": loadList(); break;
      case "clear-filters":
        filterForm.elements.branchId.value = "";
        filterForm.elements.month.value = defaultMonth;
        filterForm.elements.q.value = "";
        setFilter({ branchId: "", month: defaultMonth, q: "" });
        break;
    }
  });

  // ================================================================ Chi tiết phiếu (BM07)

  var modal = document.getElementById("receipt-modal");
  var body = $("[data-receipt-body]", modal);

  async function openDetail(row) {
    $("#receipt-title").textContent = "Phiếu nhập " + row.dataset.code;
    var loading = document.createElement("div");
    loading.className = "state";
    loading.setAttribute("role", "status");
    var spinner = document.createElement("span");
    spinner.className = "spinner";
    var text = document.createElement("span");
    text.textContent = "Đang tải phiếu nhập...";
    loading.append(spinner, text);
    body.replaceChildren(loading);
    modal.showModal();
    try {
      body.innerHTML = await Ajax.getHtml(URLS.detail(row.dataset.receiptId)); // Partial View do server render
    } catch (err) {
      if (err.status !== 401) modal.close();
    }
  }

  // Thông báo do trang Lập phiếu nhập để lại trước khi chuyển về đây.
  try {
    var flash = sessionStorage.getItem(FLASH_KEY);
    if (flash) {
      sessionStorage.removeItem(FLASH_KEY);
      Ajax.toast(flash, "success");
    }
  } catch (e) { /* sessionStorage bị chặn: bỏ qua */ }

  readUrl();
  loadList();
})();
