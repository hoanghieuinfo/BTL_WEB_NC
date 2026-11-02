// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH15 – Thực đơn khi bán (R24): tìm món, lọc theo danh mục bằng AJAX (A11).
// Bấm một món chỉ phát sự kiện "pos:add-product" trên document; việc thêm vào đơn (A13) gắn vào sự kiện này ở bước sau.
(function () {
  "use strict";

  var app = document.getElementById("order-app");
  if (!app || app.getAttribute("data-open") !== "true") return;   // đơn đã đóng: không tải thực đơn

  var P = window.Pos, h = P.h, $ = P.$;
  var DEBOUNCE_MS = 250;

  var qInput = $("menu-q");
  var catsBox = $("menu-cats");
  var grid = $("menu-grid");
  var loading = $("menu-loading");
  var empty = $("menu-empty");
  var errorBox = $("menu-error");

  var state = { q: "", categoryId: null };
  var seq = 0;                 // đánh số request: kết quả đến muộn của request cũ bị bỏ qua
  var timer = null;
  var catsJson = "";

  function show(el, on) { el.hidden = !on; }

  // Đường dẫn ảnh do server tạo (/uploads/{tenant}/{guid}.ext); vẫn kiểm tra dạng trước khi gán vào src.
  function safeImage(path) {
    return typeof path === "string" && /^\/uploads\/[\w\-.\/]+$/.test(path) ? path : null;
  }

  // ------------------------------------------------------------ Danh mục

  function renderCategories(categories) {
    var json = JSON.stringify(categories);
    if (json === catsJson) { markActiveCategory(); return; }
    catsJson = json;

    catsBox.textContent = "";
    catsBox.appendChild(chip(null, "Tất cả"));
    categories.forEach(function (c) { catsBox.appendChild(chip(c.id, c.name)); });
    markActiveCategory();
  }

  function chip(id, name) {
    return h("button", {
      type: "button", class: "chip", text: name,
      "data-category": id === null ? "" : String(id)
    });
  }

  function markActiveCategory() {
    Array.prototype.forEach.call(catsBox.children, function (el) {
      var raw = el.getAttribute("data-category");
      var active = (raw === "" && state.categoryId === null) || (raw !== "" && Number(raw) === state.categoryId);
      el.classList.toggle("is-active", active);
      el.setAttribute("aria-pressed", active ? "true" : "false");
    });
  }

  // ------------------------------------------------------------ Món

  function productCard(p) {
    var img = safeImage(p.imagePath);
    var thumb = img
      ? h("img", { class: "menu-thumb", src: img, alt: "", width: "64", height: "64", loading: "lazy" })
      : h("span", { class: "menu-thumb menu-thumb-empty", "aria-hidden": "true", text: (p.name || "?").charAt(0).toUpperCase() });

    var button = h("button", {
      type: "button", class: "menu-item", "data-product-id": p.id,
      "aria-label": p.name + ", " + P.money(p.price) + ". Bấm để thêm vào đơn."
    }, [
      thumb,
      h("span", { class: "menu-name", text: p.name }),
      h("span", { class: "menu-price", text: P.money(p.price) })
    ]);
    button.addEventListener("click", function () {
      document.dispatchEvent(new CustomEvent("pos:add-product", {
        detail: { productId: p.id, name: p.name, price: p.price }
      }));
    });
    return h("li", { class: "menu-cell" }, [button]);
  }

  function renderProducts(products) {
    grid.textContent = "";
    products.forEach(function (p) { grid.appendChild(productCard(p)); });
    show(grid, products.length > 0);
    show(empty, products.length === 0);
  }

  // ------------------------------------------------------------ Tải dữ liệu (A11)

  function load() {
    var mine = ++seq;
    show(errorBox, false);
    show(empty, false);
    show(grid, false);
    show(loading, true);

    return window.Ajax.getJson("/pos/products/search", { q: state.q, categoryId: state.categoryId }, { silent: true })
      .then(function (res) {
        if (mine !== seq) return;   // đã có request mới hơn
        show(loading, false);
        var data = res.data || { categories: [], products: [] };
        renderCategories(data.categories || []);
        renderProducts(data.products || []);
      })
      .catch(function (err) {
        if (mine !== seq || (err && err.status === 401)) return;
        if (err && err.status === 404 && state.categoryId !== null) {
          // Danh mục vừa bị chủ quán xóa: quay về "Tất cả".
          state.categoryId = null;
          load();
          return;
        }
        show(loading, false);
        show(errorBox, true);
      });
  }

  // ------------------------------------------------------------ Sự kiện

  qInput.addEventListener("input", function () {
    clearTimeout(timer);
    timer = setTimeout(function () {
      var next = qInput.value.trim();
      if (next === state.q) return;
      state.q = next;
      load();
    }, DEBOUNCE_MS);
  });

  catsBox.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-category]");
    if (!btn || !catsBox.contains(btn)) return;
    var raw = btn.getAttribute("data-category");
    var next = raw === "" ? null : Number(raw);
    if (next === state.categoryId) return;
    state.categoryId = next;
    markActiveCategory();
    load();
  });

  $("menu-retry").addEventListener("click", load);

  load();
})();
