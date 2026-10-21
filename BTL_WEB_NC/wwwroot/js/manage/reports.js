// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH13 – Báo cáo doanh thu. A09 trả JSON: JS điền chữ bằng textContent, vẽ biểu đồ bằng Chart.js (wwwroot/lib/chartjs).
// Hóa đơn trong kỳ và chi tiết hóa đơn là Partial View do server render (chèn innerHTML – Razor đã mã hóa).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;
  var URLS = {
    revenue: "/manage/reports/revenue",
    orders: "/manage/reports/orders",
    order: function (id) { return "/manage/reports/orders/" + encodeURIComponent(id); }
  };
  var MAX_DAYS = 366;
  var DEFAULT_DAYS = 14; // khớp ReportQuery.DefaultDays ở server

  var form = $("[data-filter-form]");
  var body = $("[data-report]");
  var emptyBox = $("[data-empty]");
  var errorBox = $("[data-error]");
  var orderList = $("[data-order-list]");
  var submit = $("[data-submit]", form);
  var todayText = form.dataset.today;
  var state = { from: "", to: "", branchId: "", page: 1 };
  var seq = 0;
  var charts = { day: null, branch: null };

  var vi = new Intl.NumberFormat("vi-VN");
  var vi1 = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 });

  // ================================================================ Ngày và định dạng

  function parseDate(text) { var p = text.split("-"); return new Date(+p[0], +p[1] - 1, +p[2]); }
  function iso(date) {
    return date.getFullYear() + "-" + String(date.getMonth() + 1).padStart(2, "0") + "-" + String(date.getDate()).padStart(2, "0");
  }
  function addDays(text, days) { var d = parseDate(text); d.setDate(d.getDate() + days); return iso(d); }
  function dayCount(from, to) { return Math.round((parseDate(to) - parseDate(from)) / 86400000) + 1; }
  function dmy(text) { var p = text.split("-"); return p[2] + "/" + p[1] + "/" + p[0]; }
  function dm(text) { var p = text.split("-"); return p[2] + "/" + p[1]; }

  function money(v) { return vi.format(v) + " đ"; }
  /** "354,4 tr đ" như ảnh thiết kế; dưới 1 triệu ghi đủ. */
  function shortMoney(v) {
    if (Math.abs(v) >= 1e9) return vi1.format(v / 1e9) + " tỷ đ";
    if (Math.abs(v) >= 1e6) return vi1.format(v / 1e6) + " tr đ";
    return money(v);
  }
  function axisMoney(v) {
    if (v === 0) return "0";
    if (Math.abs(v) >= 1e6) return vi1.format(v / 1e6) + " tr";
    if (Math.abs(v) >= 1e3) return vi1.format(v / 1e3) + " k";
    return vi.format(v);
  }
  function cssVar(name) { return getComputedStyle(document.documentElement).getPropertyValue(name).trim(); }

  // ================================================================ Bộ lọc

  function quickRange(kind) {
    if (kind === "today") return { from: todayText, to: todayText };
    if (kind === "month") return { from: todayText.slice(0, 8) + "01", to: todayText };
    return { from: addDays(todayText, -(parseInt(kind, 10) - 1)), to: todayText };
  }

  function markQuick() {
    $all("[data-range]", form).forEach(function (chip) {
      var r = quickRange(chip.dataset.range);
      var active = r.from === state.from && r.to === state.to;
      chip.classList.toggle("is-active", active);
      chip.setAttribute("aria-pressed", active ? "true" : "false");
    });
  }

  function readUrl() {
    var search = new URLSearchParams(location.search);
    var def = quickRange(String(DEFAULT_DAYS));
    state.from = /^\d{4}-\d{2}-\d{2}$/.test(search.get("from") || "") ? search.get("from") : def.from;
    state.to = /^\d{4}-\d{2}-\d{2}$/.test(search.get("to") || "") ? search.get("to") : def.to;
    state.branchId = search.get("branchId") || "";
    state.page = parseInt(search.get("page"), 10) || 1;
    form.elements.from.value = state.from;
    form.elements.to.value = state.to;
    form.elements.branchId.value = state.branchId;
    if (form.elements.branchId.value !== state.branchId) state.branchId = "";
  }

  function writeUrl() {
    var search = new URLSearchParams({ from: state.from, to: state.to });
    if (state.branchId) search.set("branchId", state.branchId);
    if (state.page > 1) search.set("page", state.page);
    history.replaceState(null, "", location.pathname + "?" + search.toString());
  }

  function params(withPage) {
    var p = { from: state.from, to: state.to, branchId: state.branchId };
    if (withPage && state.page > 1) p.page = state.page;
    return p;
  }

  /** Cùng quy tắc với server: đủ hai ngày, from ≤ to, tối đa 366 ngày. */
  function validate(from, to) {
    var errors = {};
    if (!from) errors.from = "Vui lòng chọn Từ ngày.";
    if (!to) errors.to = "Vui lòng chọn Đến ngày.";
    if (from && to && from > to) errors.to = "Đến ngày phải bằng hoặc sau Từ ngày.";
    else if (from && to && dayCount(from, to) > MAX_DAYS) errors.from = "Khoảng thời gian tối đa " + MAX_DAYS + " ngày.";
    return errors;
  }

  function apply(from, to, branchId) {
    var errors = validate(from, to);
    if (Object.keys(errors).length) { Ajax.showErrors(form, errors); return; }
    Ajax.clearErrors(form);
    state.from = from;
    state.to = to;
    state.branchId = branchId;
    state.page = 1;
    form.elements.from.value = from;
    form.elements.to.value = to;
    load();
  }

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    apply(form.elements.from.value, form.elements.to.value, form.elements.branchId.value);
  });

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-range], [data-action], [data-page]");
    if (!target || target.disabled) return;
    if (target.dataset.range) {
      var r = quickRange(target.dataset.range);
      apply(r.from, r.to, form.elements.branchId.value);
    } else if (target.dataset.page && orderList.contains(target)) {
      state.page = parseInt(target.dataset.page, 10);
      loadOrders();
    } else if (target.dataset.action === "view-order") {
      openOrder(target.closest("[data-order-id]"));
    } else if (target.dataset.action === "reset-default") {
      // Cùng mốc mặc định của trang (ReportQuery.DefaultDays = 14).
      var def = quickRange(String(DEFAULT_DAYS));
      apply(def.from, def.to, form.elements.branchId.value);
    } else if (target.dataset.action === "pick-range") {
      form.elements.from.focus();
      if (form.elements.from.showPicker) { try { form.elements.from.showPicker(); } catch (e) { /* không hỗ trợ */ } }
    } else if (target.dataset.action === "retry") {
      load();
    }
  });

  // ================================================================ Tải báo cáo (A09)

  async function load() {
    var my = ++seq;
    writeUrl();
    markQuick();
    var days = dayCount(state.from, state.to);
    var prevFrom = addDays(state.from, -days);
    var prevTo = addDays(state.from, -1);
    $("[data-period-text]").textContent = dmy(state.from) + " – " + dmy(state.to) + ", so với " + dm(prevFrom) + " – " + dm(prevTo);
    $("[data-compare-note]", form).textContent = "Khung đối sánh: chu kỳ " + days + " ngày liền kề trước đó";

    body.hidden = false;
    emptyBox.hidden = true;
    errorBox.hidden = true;
    body.classList.add("is-loading");
    body.setAttribute("aria-busy", "true");
    Ajax.setLoading(submit, true, "Đang tải...");
    try {
      var result = await Ajax.getJson(URLS.revenue, params(false), { silent: true });
      if (my !== seq) return;
      var report = result.data;
      if (report.summary.orders === 0) {
        showEmpty();
        return;
      }
      render(report);
      body.classList.remove("is-loading");
      await loadOrders();
    } catch (err) {
      if (my !== seq || err.status === 401) return;
      if (err.status === 400 && err.data && err.data.errors) {
        Ajax.showErrors(form, err.data.errors);
      } else if (err.status === 404) {
        Ajax.showErrors(form, { branchId: "Không tìm thấy chi nhánh." });
      }
      body.hidden = true;
      errorBox.hidden = false;
    } finally {
      if (my === seq) {
        body.setAttribute("aria-busy", "false");
        Ajax.setLoading(submit, false);
      }
    }
  }

  function showEmpty() {
    body.hidden = true;
    emptyBox.hidden = false;
    var branch = form.elements.branchId.selectedOptions[0];
    $("[data-empty-text]", emptyBox).textContent = "Hệ thống chưa ghi nhận hóa đơn đã thanh toán từ ngày " + dmy(state.from)
      + " đến " + dmy(state.to) + (state.branchId ? " tại " + branch.textContent : "")
      + ". Hãy thử nới rộng khoảng ngày hoặc chọn chi nhánh khác.";
  }

  function render(report) {
    var s = report.summary;
    $('[data-stat="revenue"]').textContent = shortMoney(s.revenue);
    $('[data-stat="orders"]').textContent = vi.format(s.orders);
    $('[data-stat="average"]').textContent = money(s.average);
    $('[data-stat="discount"]').textContent = shortMoney(s.discount);
    $('[data-stat="discounted"]').textContent = vi.format(s.discountedOrders) + " hóa đơn có giảm giá";
    trend("revenue", report.change.revenue, s.revenue, " so với kỳ trước");
    trend("orders", report.change.orders, s.orders, "");
    trend("average", report.change.average, s.average, "");

    $("[data-day-sub]").textContent = "Số liệu tổng hợp hàng ngày từ " + dm(report.from) + " đến " + dmy(report.to);
    drawDayChart(report.days);
    renderBranches(report.branches, s.revenue);
    renderTop(report.topProducts);
  }

  /** Nhãn tăng/giảm so với kỳ trước; change = null khi kỳ trước bằng 0. */
  function trend(key, change, current, suffix) {
    var el = $('[data-trend="' + key + '"]');
    el.className = "trend";
    if (change === null || change === undefined) {
      el.textContent = current > 0 ? "Mới phát sinh (kỳ trước chưa có)" : "Chưa có dữ liệu";
      el.classList.add("trend-flat");
    } else if (change >= 0) {
      el.textContent = "↑ Tăng " + vi1.format(change) + "%" + suffix;
      el.classList.add("trend-up");
    } else {
      el.textContent = "↓ Giảm " + vi1.format(-change) + "%" + suffix;
      el.classList.add("trend-down");
    }
  }

  function drawDayChart(days) {
    var primary = cssVar("--primary");
    var muted = cssVar("--muted");
    var data = {
      labels: days.map(function (d) { return dm(d.date); }),
      datasets: [
        {
          label: "Kỳ này", data: days.map(function (d) { return d.revenue; }),
          borderColor: primary, backgroundColor: "rgba(47, 107, 79, 0.12)", fill: true, tension: 0.35,
          pointRadius: days.length > 31 ? 0 : 3, pointBackgroundColor: "#fff", borderWidth: 2
        },
        {
          label: "Kỳ trước", data: days.map(function (d) { return d.previousRevenue; }),
          borderColor: muted, borderDash: [5, 4], fill: false, tension: 0.35, pointRadius: 0, borderWidth: 1.5
        }
      ]
    };
    if (charts.day) {
      charts.day.data = data;
      charts.day.update();
      return;
    }
    charts.day = new Chart(document.getElementById("day-chart"), {
      type: "line",
      data: data,
      options: {
        responsive: true, maintainAspectRatio: false, interaction: { mode: "index", intersect: false },
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { label: function (ctx) { return ctx.dataset.label + ": " + money(ctx.parsed.y); } } }
        },
        scales: {
          y: { beginAtZero: true, ticks: { callback: axisMoney }, grid: { color: "#EEE9E4" } },
          x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } }
        }
      }
    });
  }

  function renderBranches(branches, total) {
    $("[data-branch-count]").textContent = branches.length + " cơ sở";
    var list = $("[data-branch-bars]");
    list.replaceChildren.apply(list, branches.map(function (b, i) {
      var item = document.createElement("li");
      var head = document.createElement("div");
      head.className = "branch-line";
      var name = document.createElement("span");
      name.className = "branch-name";
      name.textContent = b.name;
      var value = document.createElement("span");
      value.className = "branch-value";
      value.textContent = shortMoney(b.revenue) + " (" + vi1.format(b.share) + "%)";
      head.append(name, value);
      var bar = document.createElement("span");
      bar.className = "branch-bar";
      var fill = document.createElement("span");
      fill.className = "branch-fill branch-fill-" + Math.min(i, 2);
      fill.style.width = (total > 0 ? Math.max(2, b.revenue * 100 / total) : 0) + "%";
      bar.append(fill);
      item.append(head, bar);
      return item;
    }));

    var data = {
      labels: branches.map(function (b) { return b.name; }),
      datasets: [{ label: "Doanh thu", data: branches.map(function (b) { return b.revenue; }), backgroundColor: cssVar("--primary"), borderRadius: 6 }]
    };
    if (charts.branch) {
      charts.branch.data = data;
      charts.branch.update();
      return;
    }
    charts.branch = new Chart(document.getElementById("branch-chart"), {
      type: "bar",
      data: data,
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (ctx) { return money(ctx.parsed.y); } } } },
        scales: { y: { beginAtZero: true, ticks: { callback: axisMoney } }, x: { grid: { display: false } } }
      }
    });
  }

  function renderTop(items) {
    var branch = form.elements.branchId.selectedOptions[0];
    $("[data-top-scope]").textContent = state.branchId ? branch.textContent : "Tất cả chi nhánh";
    var tbody = $("[data-top-body]");
    tbody.replaceChildren.apply(tbody, items.map(function (p) {
      var tr = document.createElement("tr");
      var rank = document.createElement("td");
      rank.className = "center";
      var badge = document.createElement("span");
      badge.className = "rank" + (p.rank <= 3 ? " rank-" + p.rank : "");
      badge.textContent = p.rank;
      rank.append(badge);
      var name = document.createElement("td");
      name.textContent = p.name;
      var qty = document.createElement("td");
      qty.className = "num";
      qty.textContent = vi.format(p.quantity);
      var revenue = document.createElement("td");
      revenue.className = "num" + (p.rank === 1 ? " top-1" : "");
      revenue.textContent = money(p.revenue);
      tr.append(rank, name, qty, revenue);
      return tr;
    }));
  }

  // ================================================================ Hóa đơn trong kỳ

  async function loadOrders() {
    writeUrl();
    orderList.setAttribute("aria-busy", "true");
    orderList.replaceChildren(document.getElementById("orders-loading-template").content.cloneNode(true));
    try {
      orderList.innerHTML = await Ajax.getHtml(URLS.orders, params(true), { silent: true }); // Partial View do server render
      var meta = $("[data-list-meta]", orderList);
      if (meta) state.page = parseInt(meta.dataset.page, 10) || 1;
      writeUrl();
    } catch (err) {
      if (err.status === 401) return;
      var box = document.createElement("div");
      box.className = "state state-error";
      var title = document.createElement("p");
      title.className = "state-title";
      title.textContent = "Không tải được danh sách hóa đơn";
      box.append(title);
      orderList.replaceChildren(box);
    } finally {
      orderList.setAttribute("aria-busy", "false");
    }
  }

  var orderModal = document.getElementById("order-modal");
  var orderBody = $("[data-order-body]", orderModal);

  async function openOrder(row) {
    var title = $("#order-title");
    title.textContent = "Hóa đơn " + row.dataset.code;
    var loading = document.createElement("div");
    loading.className = "state";
    loading.setAttribute("role", "status");
    var spinner = document.createElement("span");
    spinner.className = "spinner";
    var text = document.createElement("span");
    text.textContent = "Đang tải hóa đơn...";
    loading.append(spinner, text);
    orderBody.replaceChildren(loading);
    orderModal.showModal();
    try {
      orderBody.innerHTML = await Ajax.getHtml(URLS.order(row.dataset.orderId)); // Partial View do server render
      var meta = $("[data-order-meta]", orderBody);
      var badge = document.createElement("span");
      badge.className = "badge " + meta.dataset.statusClass;
      badge.textContent = meta.dataset.status;
      title.textContent = "Hóa đơn " + meta.dataset.code + " ";
      title.append(badge);
    } catch (err) {
      if (err.status !== 401) orderModal.close();
    }
  }

  readUrl();
  load();
})();
