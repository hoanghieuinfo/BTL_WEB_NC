// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH08 – Tổng quan. Đổi chi nhánh → gửi form GET ngay (không cần bấm "Xem"); nút "Xem" chỉ dành cho khi JS tắt.
(function () {
  "use strict";

  var form = document.querySelector("[data-branch-form]");
  if (!form) return;
  var submit = form.querySelector("[data-branch-submit]");
  submit.hidden = true;
  form.elements.branchId.addEventListener("change", function () {
    // "Tất cả chi nhánh" → bỏ hẳn tham số cho URL gọn.
    if (!form.elements.branchId.value) { location.href = form.getAttribute("action"); return; }
    form.submit();
  });
})();
