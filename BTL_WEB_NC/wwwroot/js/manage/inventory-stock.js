// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH12 – Tab Tồn kho. Danh sách và hộp thoại Chi tiết là Partial View do server render (chèn innerHTML – Razor đã mã hóa);
// chữ do JS tự điền dùng textContent.
(function () {
  "use strict";

  var $ = ManageUi.$;
  var listEl = $("[data-stock-list]");
  if (!listEl) return; // quán chưa có chi nhánh

  var URLS = {
    list: "/manage/inventory/stock",
    detail: function (branchId, ingredientId) {
      return "/manage/inventory/stock/" + encodeURIComponent(branchId) + "/" + encodeURIComponent(ingredientId);
    }
  };
  var BRANCH_KEY = "manage.inventory.branchId";

  var filterForm = $("[data-filter-form]");
  var state = { branchId: "", q: "", needOnly: false, page: 1 };
  var requestSeq = 0;

  function storage(action, value) {
    try {
      if (action === "get") return localStorage.getItem(BRANCH_KEY);
      localStorage.setItem(BRANCH_KEY, value);
    } catch (e) { /* trình duyệt chặn localStorage: bỏ qua, chỉ là tiện ích */ }
    return null;
  }

  function hasBranch(id) {
    return Array.prototype.some.call(filterForm.elements.branchId.options, function (o) { return o.value === id; });
  }

  function params() {
    return { branchId: state.branchId, q: state.q.trim(), needOnly: state.needOnly ? "true" : "", page: state.page > 1 ? state.page : "" };
  }

  function writeUrl() {
    var search = new URLSearchParams();
    var p = params();
    Object.keys(p).forEach(function (k) { if (p[k]) search.set(k, p[k]); });
    history.replaceState(null, "", location.pathname + "?" + search.toString());
  }

  function readUrl() {
    var search = new URLSearchParams(location.search);
    var branch = search.get("branchId") || storage("get") || "";
    state.branchId = hasBranch(branch) ? branch : filterForm.elements.branchId.options[0].value;
    state.q = search.get("q") || "";
    state.needOnly = search.get("needOnly") === "true";
    state.page = parseInt(search.get("page"), 10) || 1;
    filterForm.elements.branchId.value = state.branchId;
    filterForm.elements.q.value = state.q;
    filterForm.elements.needOnly.checked = state.needOnly;
  }

  async function loadList() {
    var seq = ++requestSeq;
    writeUrl();
    listEl.setAttribute("aria-busy", "true");
    listEl.replaceChildren(document.getElementById("stock-loading-template").content.cloneNode(true));
    try {
      var html = await Ajax.getHtml(URLS.list, params(), { silent: true });
      if (seq !== requestSeq) return;
      listEl.innerHTML = html; // Partial View do server render – Razor đã mã hóa
      var meta = $("[data-list-meta]", listEl);
      if (meta) {
        state.page = parseInt(meta.dataset.page, 10) || 1;
        showNeedBadge(parseInt(meta.dataset.need, 10) || 0);
      }
      writeUrl();
    } catch (err) {
      if (seq !== requestSeq || err.status === 401) return;
      listEl.replaceChildren(errorState());
    } finally {
      if (seq === requestSeq) listEl.setAttribute("aria-busy", "false");
    }
  }

  function errorState() {
    var card = document.createElement("section");
    card.className = "card";
    var box = document.createElement("div");
    box.className = "state state-error";
    var title = document.createElement("p");
    title.className = "state-title";
    title.textContent = "Không tải được tồn kho";
    var retry = document.createElement("button");
    retry.type = "button";
    retry.className = "btn";
    retry.dataset.action = "retry";
    retry.textContent = "Thử lại";
    box.append(title, retry);
    card.append(box);
    return card;
  }

  /** Nhãn "N cần nhập" trên tab Tồn kho (Âm kho + Dưới mức của chi nhánh đang xem). */
  function showNeedBadge(count) {
    var badge = $("[data-need-badge]");
    if (!badge) return;
    badge.hidden = count === 0;
    badge.textContent = count + " cần nhập";
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
  filterForm.elements.branchId.addEventListener("change", function () {
    storage("set", filterForm.elements.branchId.value);
    setFilter({ branchId: filterForm.elements.branchId.value });
  });
  filterForm.elements.needOnly.addEventListener("change", function () {
    setFilter({ needOnly: filterForm.elements.needOnly.checked });
  });

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action], [data-page]");
    if (!target || target.disabled) return;
    if (target.dataset.page && listEl.contains(target)) {
      setFilter({ page: parseInt(target.dataset.page, 10) });
      return;
    }
    switch (target.dataset.action) {
      case "detail": openDetail(target.closest("[data-ingredient-id]")); break;
      case "retry": loadList(); break;
      case "clear-filters":
        filterForm.elements.q.value = "";
        filterForm.elements.needOnly.checked = false;
        setFilter({ q: "", needOnly: false });
        break;
    }
  });

  // ================================================================ Hộp thoại Chi tiết

  var detailModal = document.getElementById("stock-detail-modal");
  var detailBody = $("[data-detail-body]", detailModal);

  async function openDetail(row) {
    $("#stock-detail-title").textContent = row.dataset.name;
    $("[data-detail-subtitle]", detailModal).textContent = "";
    var spinner = document.createElement("div");
    spinner.className = "state";
    spinner.setAttribute("role", "status");
    var icon = document.createElement("span");
    icon.className = "spinner";
    var text = document.createElement("span");
    text.textContent = "Đang tải chi tiết...";
    spinner.append(icon, text);
    detailBody.replaceChildren(spinner);
    detailModal.showModal();

    try {
      var html = await Ajax.getHtml(URLS.detail(state.branchId, row.dataset.ingredientId));
      detailBody.innerHTML = html; // Partial View do server render
      var meta = $("[data-detail-meta]", detailBody);
      $("[data-detail-subtitle]", detailModal).textContent = "Tồn kho tại " + meta.dataset.branch;
      $("[data-detail-import]", detailModal).href = meta.dataset.importHref;
    } catch (err) {
      if (err.status !== 401) detailModal.close();
    }
  }

  readUrl();
  loadList();
})();
