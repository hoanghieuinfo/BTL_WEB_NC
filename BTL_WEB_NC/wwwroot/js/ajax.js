// [AI] Claude hỗ trợ – Phụ lục Z.7
// Hàm AJAX dùng chung (mục 3.2.6): GET để đọc, POST để ghi; mọi POST gửi anti-forgery token
// trong header RequestVerificationToken. Chỉ chèn dữ liệu bằng textContent (không innerHTML).
(function () {
  "use strict";

  var DEFAULT_MESSAGES = {
    400: "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra lại.",
    401: "Phiên đăng nhập đã hết. Đang chuyển tới trang đăng nhập…",
    403: "Bạn không có quyền thực hiện thao tác này.",
    404: "Không tìm thấy dữ liệu. Có thể dữ liệu đã bị xóa.",
    409: "Dữ liệu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang rồi thử lại.",
    429: "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít giây.",
    500: "Hệ thống đang gặp sự cố. Vui lòng thử lại sau.",
    0: "Không kết nối được máy chủ. Vui lòng kiểm tra mạng."
  };

  /** Lỗi trả về cho nơi gọi: status, message, data (có thể chứa data.errors). */
  function AjaxError(status, message, data) {
    this.name = "AjaxError";
    this.status = status;
    this.message = message;
    this.data = data || null;
  }
  AjaxError.prototype = Object.create(Error.prototype);

  function token() {
    var meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.getAttribute("content") : "";
  }

  function withQuery(url, params) {
    if (!params) return url;
    var query = new URLSearchParams();
    Object.keys(params).forEach(function (key) {
      var value = params[key];
      if (value !== undefined && value !== null && value !== "") query.append(key, value);
    });
    var text = query.toString();
    if (!text) return url;
    return url + (url.indexOf("?") >= 0 ? "&" : "?") + text;
  }

  async function readBody(response) {
    var type = response.headers.get("Content-Type") || "";
    if (type.indexOf("application/json") >= 0) {
      try { return await response.json(); } catch (e) { return null; }
    }
    return await response.text();
  }

  async function send(url, init, options) {
    options = options || {};
    init.credentials = "same-origin";
    init.headers = Object.assign({ "X-Requested-With": "XMLHttpRequest" }, init.headers || {});
    if (init.method === "POST") init.headers["RequestVerificationToken"] = token();

    var response;
    try {
      response = await fetch(url, init);
    } catch (networkError) {
      return fail(0, null, options);
    }

    var body = await readBody(response);
    if (response.ok) return body;
    return fail(response.status, body, options);
  }

  function fail(status, body, options) {
    var json = body && typeof body === "object" ? body : null;
    var key = DEFAULT_MESSAGES[status] ? status : (status >= 500 ? 500 : 400);
    // 401 luôn dùng thông báo mặc định vì trang sẽ chuyển đi.
    var message = status !== 401 && json && json.message ? json.message : DEFAULT_MESSAGES[key];

    if (status === 401) {
      toast(message, "warn");
      var returnUrl = encodeURIComponent(location.pathname + location.search);
      setTimeout(function () { location.href = "/dang-nhap?returnUrl=" + returnUrl; }, 1200);
    } else if (status !== 400 && !options.silent) {
      // 400 để nơi gọi hiển thị lỗi tại từng ô bằng showErrors().
      toast(message, status === 409 || status === 429 ? "warn" : "error");
    }

    throw new AjaxError(status, message, json ? json.data : null);
  }

  /** GET trả JSON { success, message, data }. */
  function getJson(url, params, options) {
    return send(withQuery(url, params), { method: "GET", headers: { Accept: "application/json" } }, options);
  }

  /** GET trả Partial View (HTML do Razor render, đã được mã hóa). */
  function getHtml(url, params, options) {
    return send(withQuery(url, params), { method: "GET", headers: { Accept: "text/html" } }, options);
  }

  /** POST dữ liệu dạng JSON. */
  function postJson(url, data, options) {
    return send(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(data || {})
    }, options);
  }

  /** POST FormData (multipart) – dùng khi có upload ảnh. Không đặt Content-Type để trình duyệt tự thêm boundary. */
  function postForm(url, formData, options) {
    return send(url, { method: "POST", headers: { Accept: "application/json" }, body: formData }, options);
  }

  // ------------------------------------------------------------ Lỗi tại từng ô

  function sameKey(a, b) { return a.toLowerCase() === b.toLowerCase(); }

  /** Xóa lỗi cũ trong form. */
  function clearErrors(form) {
    form.querySelectorAll("[data-error-for]").forEach(function (el) { el.textContent = ""; });
    form.querySelectorAll(".is-invalid").forEach(function (el) {
      el.classList.remove("is-invalid");
      el.removeAttribute("aria-invalid");
    });
  }

  /**
   * Hiển thị data.errors = { tenTruong: [thông báo] } tại phần tử [data-error-for="tenTruong"].
   * Lỗi không gắn được với ô nào (key rỗng hoặc lạ) hiện ở [data-error-for=""] hoặc toast.
   */
  function showErrors(form, errors) {
    clearErrors(form);
    if (!errors) return;

    var holders = Array.prototype.slice.call(form.querySelectorAll("[data-error-for]"));
    var fields = Array.prototype.slice.call(form.elements);
    var general = [];
    var firstInvalid = null;

    Object.keys(errors).forEach(function (key) {
      var text = [].concat(errors[key]).join(" ");
      var holder = key && holders.filter(function (h) { return sameKey(h.getAttribute("data-error-for"), key); })[0];
      if (!holder) { general.push(text); return; }
      holder.textContent = text;

      var input = fields.filter(function (f) { return f.name && sameKey(f.name, key); })[0];
      if (input) {
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        if (!firstInvalid) firstInvalid = input;
      }
    });

    if (general.length) {
      var generalHolder = form.querySelector('[data-error-for=""]');
      if (generalHolder) generalHolder.textContent = general.join(" ");
      else toast(general.join(" "), "error");
    }
    if (firstInvalid) firstInvalid.focus();
  }

  // ------------------------------------------------------------ Toast, trạng thái nút

  function toast(message, type) {
    var region = document.getElementById("toast-region");
    if (!region) { alert(message); return; }
    var item = document.createElement("div");
    item.className = "toast toast-" + (type || "success");
    item.textContent = message;
    region.appendChild(item);
    setTimeout(function () { item.remove(); }, type === "error" ? 6000 : 3500);
  }

  /** Khóa nút và hiện vòng quay trong lúc chờ server (trạng thái "đang lưu"). */
  function setLoading(button, loading, loadingText) {
    if (!button) return;
    if (loading) {
      button.dataset.originalText = button.textContent;
      if (loadingText) button.textContent = loadingText;
      button.disabled = true;
      button.classList.add("is-loading");
    } else {
      if (button.dataset.originalText !== undefined) button.textContent = button.dataset.originalText;
      button.disabled = false;
      button.classList.remove("is-loading");
    }
  }

  window.Ajax = {
    getJson: getJson,
    getHtml: getHtml,
    postJson: postJson,
    postForm: postForm,
    showErrors: showErrors,
    clearErrors: clearErrors,
    toast: toast,
    setLoading: setLoading,
    AjaxError: AjaxError
  };
})();
