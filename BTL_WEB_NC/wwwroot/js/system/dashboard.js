// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH05 – Tổng quan hệ thống: vẽ biểu đồ từ data-* do Razor render (không Html.Raw), duyệt nhanh quán chờ kích hoạt (A04).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var $all = ManageUi.$all;

  // ------------------------------------------------------------ Biểu đồ

  var monthly = document.getElementById("chart-monthly");
  if (monthly && window.Chart) {
    new Chart(monthly, {
      type: "line",
      data: {
        labels: monthly.dataset.labels.split(","),
        datasets: [{
          label: "Quán đăng ký mới",
          data: monthly.dataset.values.split(",").map(Number),
          borderColor: "#2F6B4F",
          backgroundColor: "rgba(47, 107, 79, 0.1)",
          fill: true,
          tension: 0.3,
          pointRadius: 4,
          pointBackgroundColor: "#2F6B4F"
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
      }
    });
  }

  var plans = document.getElementById("chart-plans");
  var planItems = $all("#plan-data li");
  if (plans && planItems.length && window.Chart) {
    new Chart(plans, {
      type: "doughnut",
      data: {
        labels: planItems.map(function (li) { return li.dataset.name + " (" + li.dataset.pct + "%)"; }),
        datasets: [{
          data: planItems.map(function (li) { return Number(li.dataset.count); }),
          backgroundColor: ["#7CC49E", "#2F6B4F", "#2B1D16", "#EBC98A", "#C0392B"],
          borderWidth: 2,
          borderColor: "#fff"
        }]
      },
      // Chú thích đã có ở danh sách #plan-data bên dưới → tắt chú thích của Chart.js để không lặp và không tràn ở 360px.
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } } }
    });
  }

  // ------------------------------------------------------------ Kích hoạt (xác nhận) / Từ chối (bắt buộc lý do)

  var rejectModal = document.getElementById("reject-modal");
  var rejectForm = document.getElementById("reject-form");
  var rejectId = null;

  document.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-action]");
    if (!btn) return;
    var id = btn.dataset.id;
    var name = btn.dataset.name;

    if (btn.dataset.action === "activate") {
      ManageUi.confirmAction({
        title: "Kích hoạt quán",
        message: "Kích hoạt quán \"" + name + "\"? Chủ quán sẽ đăng nhập và dùng được dịch vụ ngay.",
        okText: "Kích hoạt",
        busyText: "Đang kích hoạt...",
        onConfirm: async function () {
          await Ajax.postJson("/system/tenants/" + id + "/status", { status: 1 });
          location.reload();
        }
      });
    } else if (btn.dataset.action === "reject") {
      rejectId = id;
      rejectForm.reset();
      Ajax.clearErrors(rejectForm);
      $("[data-tenant-name]", rejectModal).textContent = name;
      rejectModal.showModal();
    }
  });

  rejectForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    var reason = rejectForm.elements.lockReason.value.trim();
    if (!reason) {
      Ajax.showErrors(rejectForm, { lockReason: ["Vui lòng nhập lý do từ chối."] });
      return;
    }

    var submit = $("[data-submit]", rejectForm);
    ManageUi.setBusy(rejectModal, true);
    Ajax.setLoading(submit, true, "Đang từ chối...");
    try {
      await Ajax.postJson("/system/tenants/" + rejectId + "/status", { status: 3, lockReason: reason });
      ManageUi.closeDialog(rejectModal);
      location.reload();
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(rejectForm, (err.data && err.data.errors) || { "": [err.message] });
    } finally {
      ManageUi.setBusy(rejectModal, false);
      Ajax.setLoading(submit, false);
    }
  });
})();
