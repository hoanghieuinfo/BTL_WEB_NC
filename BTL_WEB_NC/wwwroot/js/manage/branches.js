// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH09 – Chi nhánh, khu vực, bàn, mã QR. Đọc: Partial View do server render (chèn innerHTML – Razor đã mã hóa).
// Ghi: JSON qua ajax.js (tự gửi anti-forgery token). Mọi dữ liệu khác điền bằng textContent / value.
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;
  var URLS = {
    list: "/manage/branches/list",
    zones: function (id) { return "/manage/branches/" + id + "/zones"; },
    create: "/manage/branches/create",
    update: function (id) { return "/manage/branches/" + id + "/update"; },
    zoneCreate: function (branchId) { return "/manage/branches/" + branchId + "/zones/create"; },
    zoneRename: function (id) { return "/manage/zones/" + id + "/rename"; },
    zoneMove: function (id) { return "/manage/zones/" + id + "/move"; },
    tableCreate: function (zoneId) { return "/manage/zones/" + zoneId + "/tables/create"; },
    tableUpdate: function (id) { return "/manage/tables/" + id + "/update"; },
    tableVisibility: function (id) { return "/manage/tables/" + id + "/visibility"; },
    qr: function (id) { return "/manage/branches/" + id + "/qr"; },
    qrImage: function (id) { return "/manage/branches/" + id + "/qr.png"; }
  };
  var SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

  var listEl = $("[data-branch-list]");
  var boardEl = $("[data-zone-board]");
  var selectedId = readSelectedId();

  function readSelectedId() {
    var meta = $("[data-list-meta]", listEl);
    return meta && meta.dataset.selectedId ? meta.dataset.selectedId : "";
  }

  // ================================================================ Tải lại Partial View

  async function reloadList() {
    try {
      listEl.innerHTML = await Ajax.getHtml(URLS.list, { selectedId: selectedId }, { silent: true }); // Partial View đã mã hóa
      selectedId = readSelectedId();
      applyUsage($("[data-list-meta]", listEl));
    } catch (err) {
      if (err.status !== 401) Ajax.toast("Không tải được danh sách chi nhánh. " + err.message, "error");
    }
  }

  async function reloadBoard() {
    if (!selectedId) return;
    boardEl.setAttribute("aria-busy", "true");
    boardEl.replaceChildren(document.getElementById("board-loading-template").content.cloneNode(true));
    try {
      boardEl.innerHTML = await Ajax.getHtml(URLS.zones(selectedId), null, { silent: true }); // Partial View đã mã hóa
    } catch (err) {
      if (err.status === 401) return;
      var box = document.createElement("div");
      box.className = "state state-error";
      var title = document.createElement("p");
      title.className = "state-title";
      title.textContent = "Không tải được khu vực và bàn";
      var retry = document.createElement("button");
      retry.type = "button";
      retry.className = "btn";
      retry.dataset.action = "reload-board";
      retry.textContent = "Thử lại";
      box.append(title, retry);
      boardEl.replaceChildren(box);
    } finally {
      boardEl.setAttribute("aria-busy", "false");
    }
  }

  function refreshAll() { return Promise.all([reloadList(), reloadBoard()]); }

  function selectBranch(id) {
    selectedId = String(id);
    history.replaceState(null, "", location.pathname + "?branchId=" + encodeURIComponent(selectedId));
    refreshAll();
  }

  /** Cập nhật nhãn "x/y chi nhánh của gói", nút Thêm và khối mức dùng ở sidebar (R11). */
  function applyUsage(meta) {
    if (!meta) return;
    var count = parseInt(meta.dataset.usageCount, 10);
    var max = parseInt(meta.dataset.usageMax, 10);
    var full = count >= max;
    $all("[data-usage-count]").forEach(function (el) { el.textContent = count; });
    $all("[data-usage-max]").forEach(function (el) { el.textContent = max; });
    $all("[data-usage-bar]").forEach(function (bar) { bar.max = max; bar.value = count; });
    $("[data-usage-pill]").classList.toggle("is-full", full);
    $('[data-action="add-branch"]').disabled = full;
    $("[data-limit-note]").hidden = !full;
    $("[data-branch-total]").textContent = meta.dataset.branchCount + " địa điểm";

    var sidebarValue = $('[data-sidebar-usage="branches"]');
    if (sidebarValue) sidebarValue.textContent = count + "/" + max;
    var sidebarBar = $('[data-sidebar-usage-bar="branches"]');
    if (sidebarBar) { sidebarBar.max = max; sidebarBar.value = count; sidebarBar.classList.toggle("is-full", full); }
  }

  // ================================================================ Sự kiện

  document.addEventListener("click", function (event) {
    var target = event.target.closest("[data-action]");
    if (!target || target.disabled) return;
    var branchRow = target.closest("[data-branch-id]");
    var zone = target.closest("[data-zone-id]");
    var table = target.closest("[data-table-id]");
    var menu = target.closest("details");
    if (menu) menu.open = false;

    switch (target.dataset.action) {
      case "select-branch": selectBranch(branchRow.dataset.branchId); break;
      case "add-branch": openBranchForm(null); break;
      case "edit-branch": openBranchForm(branchRow); break;
      case "show-qr": openQr(branchRow); break;
      case "reload-board": reloadBoard(); break;
      case "add-zone": openZoneForm(null); break;
      case "rename-zone": openZoneForm(zone); break;
      case "move-zone": moveZone(zone, target.dataset.direction); break;
      case "add-table": openTableForm(zone, null); break;
      case "edit-table": openTableForm(zone, table); break;
      case "hide-table": hideTable(table); break;
      case "show-table": setTableActive(table, true); break;
      case "copy-qr-url": copyQrUrl(); break;
      case "print-qr": printQr(); break;
    }
  });

  /** Gửi form: khóa hộp thoại, đóng khi thành công, hiện lỗi tại ô (400) – lỗi không gắn ô hiện ở [data-error-for=""]. */
  async function submitDialog(dialog, form, request, onSuccess, onError) {
    var submit = $("[data-submit]", form);
    ManageUi.setBusy(dialog, true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      var result = await request();
      ManageUi.closeDialog(dialog);
      Ajax.toast(result.message, "success");
      await onSuccess(result);
    } catch (err) {
      if (err.status === 400) {
        var errors = err.data && err.data.errors ? err.data.errors : { "": err.message };
        Ajax.showErrors(form, errors);
        if (onError) onError(errors, err.message);
      }
    } finally {
      ManageUi.setBusy(dialog, false);
      Ajax.setLoading(submit, false);
    }
  }

  // ================================================================ Chi nhánh (BM03)

  var branchModal = document.getElementById("branch-modal");
  var branchForm = document.getElementById("branch-form");
  var editingBranchId = null;
  var slugTouched = false;

  /** Cùng thuật toán với BranchService.SuggestSlug phía server. */
  function suggestSlug(name) {
    return name.trim().toLowerCase().replace(/đ/g, "d")
      .normalize("NFD").replace(/[̀-ͯ]/g, "")
      .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "")
      .slice(0, 60).replace(/-+$/, "");
  }

  function openBranchForm(row) {
    editingBranchId = row ? row.dataset.branchId : null;
    slugTouched = !!row;
    branchForm.reset();
    Ajax.clearErrors(branchForm);
    showSummary(null);
    $("#branch-modal-title").textContent = row ? "Sửa chi nhánh " + row.dataset.name : "Thêm chi nhánh mới";

    var f = branchForm.elements;
    if (row) {
      f.name.value = row.dataset.name;
      f.slug.value = row.dataset.slug;
      f.address.value = row.dataset.address;
      f.phone.value = row.dataset.phone || "";
      f.openTime.value = row.dataset.open;
      f.closeTime.value = row.dataset.close;
      f.isActive.value = row.dataset.active;
    } else {
      f.openTime.value = "07:00";
      f.closeTime.value = "22:00";
    }

    // "Sau khi lưu, chi nhánh dùng x/y suất của gói" – chỉ khi thêm mới.
    var meta = $("[data-list-meta]", listEl);
    var preview = $("[data-usage-preview]", branchForm);
    preview.hidden = !!row || !meta;
    if (!row && meta) {
      var next = parseInt(meta.dataset.usageCount, 10) + 1;
      var max = parseInt(meta.dataset.usageMax, 10);
      $("[data-preview-text]", preview).textContent = next + "/" + max;
      var bar = $("[data-preview-bar]", preview);
      bar.max = max;
      bar.value = next;
    }
    branchModal.showModal();
    f.name.focus();
  }

  branchForm.elements.name.addEventListener("input", function () {
    if (!slugTouched) branchForm.elements.slug.value = suggestSlug(branchForm.elements.name.value);
  });
  branchForm.elements.slug.addEventListener("input", function () { slugTouched = true; });

  function showSummary(text) {
    var box = $("[data-form-summary]", branchForm);
    box.hidden = !text;
    $("[data-summary-title]", box).textContent = editingBranchId ? "Không thể lưu chi nhánh" : "Không thể tạo chi nhánh";
    $("[data-summary-text]", box).textContent = text || "";
  }

  function branchErrors() {
    var f = branchForm.elements;
    var errors = {};
    if (!f.name.value.trim()) errors.name = "Vui lòng nhập tên chi nhánh.";
    var slug = f.slug.value.trim();
    if (!slug) errors.slug = "Vui lòng nhập đường dẫn thực đơn.";
    else if (!SLUG_PATTERN.test(slug)) errors.slug = "Chỉ dùng chữ thường không dấu, chữ số và dấu gạch ngang (ví dụ: cau-giay).";
    if (!f.address.value.trim()) errors.address = "Vui lòng nhập địa chỉ.";
    if (f.phone.value.trim() && !/^\d{10}$/.test(f.phone.value.trim())) errors.phone = "Số điện thoại gồm đúng 10 chữ số.";
    if (!f.openTime.value) errors.openTime = "Vui lòng nhập giờ mở cửa.";
    if (!f.closeTime.value) errors.closeTime = "Vui lòng nhập giờ đóng cửa.";
    else if (f.openTime.value && f.closeTime.value <= f.openTime.value) {
      errors.closeTime = "Giờ đóng cửa phải sau giờ mở cửa (" + f.openTime.value + ").";
    }
    return errors;
  }

  function summaryText(count) {
    return "Phát hiện " + count + " trường thông tin chưa chính xác. Vui lòng kiểm tra lại các ô được đánh dấu.";
  }

  branchForm.addEventListener("submit", function (event) {
    event.preventDefault();
    var errors = branchErrors();
    if (Object.keys(errors).length) {
      Ajax.showErrors(branchForm, errors);
      showSummary(summaryText(Object.keys(errors).length));
      return;
    }
    showSummary(null);

    var f = branchForm.elements;
    var body = {
      name: f.name.value.trim(),
      slug: f.slug.value.trim(),
      address: f.address.value.trim(),
      phone: f.phone.value.trim() || null,
      openTime: f.openTime.value + ":00", // "07:00" → "07:00:00" cho TimeOnly
      closeTime: f.closeTime.value + ":00",
      isActive: f.isActive.value === "true"
    };
    submitDialog(branchModal, branchForm,
      function () { return Ajax.postJson(editingBranchId ? URLS.update(editingBranchId) : URLS.create, body); },
      function (result) {
        if (!editingBranchId) selectBranch(result.data.id);
        else return refreshAll();
      },
      function (fieldErrors, message) {
        var keys = Object.keys(fieldErrors).filter(function (k) { return k; });
        showSummary(keys.length ? summaryText(keys.length) : message);
      });
  });

  // ================================================================ Khu vực

  var zoneModal = document.getElementById("zone-modal");
  var zoneForm = document.getElementById("zone-form");
  var editingZoneId = null;

  function boardMeta() { return $("[data-board-meta]", boardEl); }

  function openZoneForm(zone) {
    editingZoneId = zone ? zone.dataset.zoneId : null;
    zoneForm.reset();
    Ajax.clearErrors(zoneForm);
    var branchName = boardMeta() ? boardMeta().dataset.branchName : "";
    $("#zone-modal-title").textContent = zone ? "Đổi tên khu vực " + zone.dataset.zoneName : "Thêm khu vực – " + branchName;
    if (zone) zoneForm.elements.name.value = zone.dataset.zoneName;
    zoneModal.showModal();
    zoneForm.elements.name.focus();
  }

  zoneForm.addEventListener("submit", function (event) {
    event.preventDefault();
    var name = zoneForm.elements.name.value.trim();
    if (!name) { Ajax.showErrors(zoneForm, { name: "Vui lòng nhập tên khu vực." }); return; }
    var url = editingZoneId ? URLS.zoneRename(editingZoneId) : URLS.zoneCreate(boardMeta().dataset.branchId);
    submitDialog(zoneModal, zoneForm, function () { return Ajax.postJson(url, { name: name }); }, refreshAll);
  });

  async function moveZone(zone, direction) {
    try {
      await Ajax.postJson(URLS.zoneMove(zone.dataset.zoneId), { direction: direction });
      await reloadBoard();
    } catch (err) {
      if (err.status === 400) Ajax.toast(err.message, "error");
    }
  }

  // ================================================================ Bàn

  var tableModal = document.getElementById("table-modal");
  var tableForm = document.getElementById("table-form");
  var editingTableId = null;
  var tableZoneId = null;

  function openTableForm(zone, table) {
    editingTableId = table ? table.dataset.tableId : null;
    tableZoneId = zone.dataset.zoneId;
    tableForm.reset();
    Ajax.clearErrors(tableForm);

    // Ô Khu vực: các khu vực của chi nhánh đang chọn, mặc định là khu vực có nút vừa bấm.
    var select = tableForm.elements.zoneId;
    select.replaceChildren();
    $all("[data-zone-id]", boardEl).forEach(function (section) {
      var option = document.createElement("option");
      option.value = section.dataset.zoneId;
      option.textContent = section.dataset.zoneName + " (hiện có " + section.dataset.zoneCount + " bàn)";
      select.appendChild(option);
    });
    select.value = tableZoneId;

    var branchName = boardMeta().dataset.branchName;
    $("#table-modal-title").textContent = (table ? "Sửa " + table.dataset.name : "Thêm bàn") + " – " + zone.dataset.zoneName + ", " + branchName;
    if (table) {
      tableForm.elements.name.value = table.dataset.name;
      tableForm.elements.seats.value = table.dataset.seats || "";
      tableForm.elements.isActive.checked = table.dataset.active === "true";
    } else {
      tableForm.elements.name.value = suggestTableName(zone);
      tableForm.elements.seats.value = 4;
      tableForm.elements.isActive.checked = true;
    }
    tableModal.showModal();
    tableForm.elements.name.focus();
    tableForm.elements.name.select();
  }

  /** Gợi ý tên bàn tiếp theo theo mẫu của khu vực, ví dụ "Bàn 08" → "Bàn 09", "SV-06" → "SV-07". */
  function suggestTableName(zone) {
    var names = $all("[data-table-id]", zone).map(function (t) { return t.dataset.name; });
    var last = names[names.length - 1];
    var match = last && /^(.*?)(\d+)$/.exec(last);
    if (!match) return "Bàn " + String(names.length + 1).padStart(2, "0");
    var next = String(parseInt(match[2], 10) + 1).padStart(match[2].length, "0");
    return match[1] + next;
  }

  tableForm.addEventListener("submit", function (event) {
    event.preventDefault();
    var f = tableForm.elements;
    var name = f.name.value.trim();
    var seats = f.seats.value === "" ? null : parseInt(f.seats.value, 10);
    var errors = {};
    if (!name) errors.name = "Vui lòng nhập tên bàn.";
    if (seats !== null && (isNaN(seats) || seats < 1 || seats > 50)) errors.seats = "Số chỗ ngồi từ 1 đến 50.";
    if (Object.keys(errors).length) { Ajax.showErrors(tableForm, errors); return; }

    var body = { name: name, seats: seats, isActive: f.isActive.checked };
    var url;
    if (editingTableId) {
      body.zoneId = parseInt(f.zoneId.value, 10);
      url = URLS.tableUpdate(editingTableId);
    } else {
      url = URLS.tableCreate(f.zoneId.value); // khu vực lấy từ URL; server tra lại ZoneId theo tenant
    }
    submitDialog(tableModal, tableForm, function () { return Ajax.postJson(url, body); }, refreshAll);
  });

  function hideTable(table) {
    ManageUi.confirmAction({
      title: "Ẩn " + table.dataset.name + "?",
      message: "Bàn sẽ không còn hiện trên sơ đồ của thu ngân. Hóa đơn cũ của bàn vẫn giữ nguyên; bạn có thể hiện lại bàn bất cứ lúc nào.",
      okText: "Ẩn bàn",
      busyText: "Đang ẩn...",
      onConfirm: function () { return setTableActive(table, false, true); }
    });
  }

  async function setTableActive(table, isActive, rethrow) {
    try {
      var result = await Ajax.postJson(URLS.tableVisibility(table.dataset.tableId), { isActive: isActive });
      Ajax.toast(result.message, "success");
      await refreshAll();
    } catch (err) {
      if (rethrow) throw err; // để hộp thoại xác nhận hiện lỗi
      if (err.status === 400) Ajax.toast(err.message, "error");
    }
  }

  // ================================================================ Mã QR (R16)

  var qrModal = document.getElementById("qr-modal");

  async function openQr(row) {
    try {
      var result = await Ajax.getJson(URLS.qr(row.dataset.branchId));
      var info = result.data;
      var label = info.tenantName + " – " + info.branchName;
      $("#qr-modal-title").textContent = "Mã QR thực đơn – " + info.branchName;
      $("[data-qr-subtitle]", qrModal).textContent = "Chi nhánh " + info.branchName;
      $("[data-qr-eyebrow]", qrModal).textContent = label;
      $("[data-qr-title]", qrModal).textContent = label;
      var image = $("[data-qr-image]", qrModal);
      image.src = URLS.qrImage(info.branchId) + "?v=" + Date.now();
      image.alt = "Mã QR thực đơn " + label;
      $("[data-qr-url]", qrModal).value = info.url;
      $("[data-qr-open]", qrModal).href = info.url;
      $("[data-qr-download]", qrModal).href = URLS.qrImage(info.branchId) + "?download=true";
      qrModal.showModal();
    } catch (err) {
      if (err.status === 400) Ajax.toast(err.message, "error");
    }
  }

  async function copyQrUrl() {
    var input = $("[data-qr-url]", qrModal);
    try {
      await navigator.clipboard.writeText(input.value);
    } catch (e) {
      input.select();
      document.execCommand("copy"); // trình duyệt cũ hoặc không cho dùng Clipboard API
    }
    Ajax.toast("Đã sao chép đường dẫn thực đơn.", "success");
  }

  /** In chỉ thẻ QR: CSS @media print ẩn mọi thứ trừ .qr-print-area khi body có lớp is-printing-qr. */
  function printQr() {
    document.body.classList.add("is-printing-qr");
    window.addEventListener("afterprint", function done() {
      document.body.classList.remove("is-printing-qr");
      window.removeEventListener("afterprint", done);
    });
    window.print();
  }
})();
