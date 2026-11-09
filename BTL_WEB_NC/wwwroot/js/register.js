// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
// Kiểm tra trùng lặp email và slug bằng AJAX (A01, A02), chống gửi form lặp (R01, R36)

document.addEventListener("DOMContentLoaded", function () {
    var emailInput = document.getElementById("Email");
    var slugInput = document.getElementById("Slug");
    var slugHint = document.getElementById("slug-hint");
    var slugStatusIcon = document.getElementById("slug-status-icon");
    var form = document.getElementById("register-form");
    var submitBtn = document.getElementById("btn-submit-register");

    // Chọn gói dịch vụ (Radio card)
    var planCards = document.querySelectorAll(".plan-radio-card");
    planCards.forEach(function (card) {
        card.addEventListener("click", function () {
            planCards.forEach(function (c) { c.classList.remove("is-selected"); });
            this.classList.add("is-selected");
        });
    });

    // 1. Kiểm tra Email qua AJAX (Endpoint A01)
    if (emailInput) {
        emailInput.addEventListener("blur", async function () {
            var val = this.value.trim();
            if (!val || val.indexOf("@") === -1) return;

            try {
                var res = await fetch("/api/account/check-email?email=" + encodeURIComponent(val), {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                var data = await res.json();
                if (!data.available) {
                    showError("Email", data.message || "Email này đã được sử dụng.");
                } else {
                    clearError("Email");
                }
            } catch (err) {
                console.error("Lỗi kiểm tra email:", err);
            }
        });
    }

    // 2. Kiểm tra Slug qua AJAX (Endpoint A02)
    if (slugInput) {
        slugInput.addEventListener("blur", async function () {
            var val = this.value.trim().toLowerCase();
            this.value = val;
            if (!val) {
                if (slugStatusIcon) slugStatusIcon.textContent = "";
                return;
            }

            try {
                var res = await fetch("/api/account/check-slug?slug=" + encodeURIComponent(val), {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                var data = await res.json();
                if (!data.available) {
                    showError("Slug", data.message || "Đường dẫn không hợp lệ.");
                    if (slugStatusIcon) slugStatusIcon.textContent = "❌";
                    if (data.suggestion && slugHint) {
                        slugHint.textContent = "Gợi ý khả dụng: " + data.suggestion;
                        slugHint.style.color = "var(--primary)";
                    }
                } else {
                    clearError("Slug");
                    if (slugStatusIcon) slugStatusIcon.textContent = "✅";
                    if (slugHint) {
                        slugHint.textContent = "Đường dẫn hợp lệ và khả dụng!";
                        slugHint.style.color = "var(--primary)";
                    }
                }
            } catch (err) {
                console.error("Lỗi kiểm tra slug:", err);
            }
        });
    }

    // Hiển thị lỗi trường bằng textContent (chống XSS)
    function showError(fieldName, message) {
        var errEl = document.querySelector('[data-error-for="' + fieldName + '"]');
        if (errEl) {
            errEl.textContent = message;
            errEl.style.display = "block";
        }
        var inputEl = document.getElementById(fieldName);
        if (inputEl) {
            inputEl.classList.add("input-error");
        }
    }

    function clearError(fieldName) {
        var errEl = document.querySelector('[data-error-for="' + fieldName + '"]');
        if (errEl) {
            errEl.textContent = "";
            errEl.style.display = "none";
        }
        var inputEl = document.getElementById(fieldName);
        if (inputEl) {
            inputEl.classList.remove("input-error");
        }
    }

    // Chặn bấm submit 2 lần
    if (form && submitBtn) {
        form.addEventListener("submit", function () {
            if (form.checkValidity()) {
                submitBtn.disabled = true;
                submitBtn.textContent = "Đang khởi tạo quán...";
            }
        });
    }
});
