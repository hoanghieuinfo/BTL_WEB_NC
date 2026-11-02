// [AI] Claude hỗ trợ – Phụ lục Z.7
// Tiện ích dùng chung của giao diện bán hàng (Area Pos). Cần nạp sau js/ajax.js.
// Dựng DOM bằng textContent / createElement, không dùng innerHTML với dữ liệu từ server (Bảng 3.22 – XSS).
(function (global) {
  "use strict";

  var nf = new Intl.NumberFormat("vi-VN");

  /** h("div", { class: "a", text: "x" }, [con...]) – tạo phần tử an toàn. */
  function h(tag, props, children) {
    var el = document.createElement(tag);
    Object.keys(props || {}).forEach(function (key) {
      var value = props[key];
      if (value === null || value === undefined || value === false) return;
      if (key === "class") el.className = value;
      else if (key === "text") el.textContent = value;
      else el.setAttribute(key, value === true ? "" : value);
    });
    [].concat(children === undefined ? [] : children).forEach(function (child) {
      if (child === null || child === undefined || child === false) return;
      el.appendChild(typeof child === "object" ? child : document.createTextNode(String(child)));
    });
    return el;
  }

  global.Pos = {
    h: h,
    $: function (id) { return document.getElementById(id); },
    money: function (n) { return nf.format(Math.round(Number(n) || 0)) + " đ"; },
    // Khớp enum TableStatus ở server: 0 Trống, 1 Đang phục vụ, 2 Chờ thanh toán.
    TableStatus: { Empty: 0, Serving: 1, AwaitingPayment: 2 },
    statusText: ["Trống", "Đang phục vụ", "Chờ thanh toán"]
  };
})(window);
