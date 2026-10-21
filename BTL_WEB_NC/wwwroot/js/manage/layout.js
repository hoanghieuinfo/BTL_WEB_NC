// [AI] Claude hỗ trợ – Phụ lục Z.7
// Khung trang quản trị: mở/đóng menu bên dưới 1280px, đóng menu tài khoản khi bấm ra ngoài.
(function () {
  "use strict";

  var body = document.body;
  var toggle = document.querySelector("[data-sidebar-toggle]");
  var backdrop = document.querySelector("[data-sidebar-close]");
  var sidebar = document.getElementById("sidebar");

  function setSidebar(open) {
    body.classList.toggle("is-sidebar-open", open);
    if (toggle) {
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
      toggle.setAttribute("aria-label", open ? "Đóng menu" : "Mở menu");
    }
    if (backdrop) backdrop.hidden = !open;
    if (open && sidebar) {
      var firstLink = sidebar.querySelector("a");
      if (firstLink) firstLink.focus();
    }
  }

  if (toggle) {
    toggle.addEventListener("click", function () {
      setSidebar(!body.classList.contains("is-sidebar-open"));
    });
  }
  if (backdrop) backdrop.addEventListener("click", function () { setSidebar(false); });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && body.classList.contains("is-sidebar-open")) {
      setSidebar(false);
      if (toggle) toggle.focus();
    }
  });

  // Lên ≥ 1280px thì menu luôn hiện: bỏ trạng thái mở để không kẹt lớp phủ.
  var desktop = window.matchMedia("(min-width: 1280px)");
  desktop.addEventListener("change", function (e) { if (e.matches) setSidebar(false); });

  var userMenu = document.querySelector(".user-menu");
  if (userMenu) {
    document.addEventListener("click", function (event) {
      if (userMenu.open && !userMenu.contains(event.target)) userMenu.open = false;
    });
  }
})();
