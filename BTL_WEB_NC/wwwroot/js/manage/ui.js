// [AI] Claude hỗ trợ – Phụ lục Z.7
// Tiện ích giao diện dùng chung cho các màn hình Module 2: hộp thoại <dialog>, khóa form khi đang lưu, hộp thoại xác nhận.
(function () {
  "use strict";

  function $(selector, root) { return (root || document).querySelector(selector); }
  function $all(selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); }

  /** Nút [data-close] đóng hộp thoại; không cho đóng (kể cả bằng Esc) khi đang lưu. */
  $all("dialog.modal").forEach(function (dialog) {
    dialog.addEventListener("click", function (event) {
      if (event.target.closest("[data-close]") && !dialog.dataset.busy) dialog.close();
    });
    dialog.addEventListener("cancel", function (event) { if (dialog.dataset.busy) event.preventDefault(); });
  });

  // Đóng menu ⋯ (<details class="row-menu">) khi bấm ra ngoài.
  document.addEventListener("click", function (event) {
    $all(".row-menu[open]").forEach(function (menu) { if (!menu.contains(event.target)) menu.open = false; });
  });

  /** Khóa mọi ô nhập và nút trong hộp thoại (trừ nút gửi – nút này do Ajax.setLoading xử lý). */
  function setBusy(dialog, busy) {
    if (busy) dialog.dataset.busy = "true"; else delete dialog.dataset.busy;
    $all("input, select, textarea, button", dialog).forEach(function (el) {
      if (!el.matches("[data-submit]")) el.disabled = busy;
    });
  }

  /** Đóng hộp thoại ngay cả khi đang ở trạng thái bận (gọi sau khi lưu thành công). */
  function closeDialog(dialog) {
    delete dialog.dataset.busy;
    dialog.close();
  }

  /**
   * Hộp thoại xác nhận dùng #confirm-modal của trang.
   * options: { title, message, okText, busyText, onConfirm: async () => {} }.
   * Lỗi 400 (không gắn ô) hiện toast; các mã khác ajax.js đã tự báo.
   */
  function confirmAction(options) {
    var modal = document.getElementById("confirm-modal");
    $("[data-confirm-title]", modal).textContent = options.title;
    $("[data-confirm-message]", modal).textContent = options.message;
    var ok = $("[data-confirm-ok]", modal);
    ok.textContent = options.okText;
    ok.onclick = async function () {
      setBusy(modal, true);
      Ajax.setLoading(ok, true, options.busyText || "Đang xử lý...");
      try {
        await options.onConfirm();
        closeDialog(modal);
      } catch (err) {
        if (err.status === 400) Ajax.toast(err.message, "error");
      } finally {
        setBusy(modal, false);
        Ajax.setLoading(ok, false);
      }
    };
    modal.showModal();
  }

  window.ManageUi = { $: $, $all: $all, setBusy: setBusy, closeDialog: closeDialog, confirmAction: confirmAction };
})();
