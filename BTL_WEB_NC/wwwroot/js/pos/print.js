// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH16 – tự mở hộp thoại in khi trang phiếu vừa tải xong (R27) và nút "In lại". Không dùng innerHTML, không gửi dữ liệu đi đâu.
(function () {
  "use strict";

  var slip = document.getElementById("slip");
  var btn = document.getElementById("print-now");

  if (btn) btn.addEventListener("click", function () { window.print(); });

  // Chờ chữ và bố cục ổn định rồi mới in để phiếu không bị cắt. Quay lại bằng nút Back (trang lấy từ bộ nhớ đệm) thì không in lại.
  if (slip && slip.getAttribute("data-auto-print") === "true") {
    var printed = false;
    var run = function () {
      if (printed) return;
      printed = true;
      setTimeout(function () { window.print(); }, 150);
    };
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(run, run); else run();
  }
})();
