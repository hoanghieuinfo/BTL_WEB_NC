// [AI] Claude hỗ trợ – Phụ lục Z.7
// MH07 – Gói dịch vụ: thêm/sửa gói (POST /system/plans/save), ngừng/cung cấp lại (POST /system/plans/{id}/toggle).
(function () {
  "use strict";

  var $ = ManageUi.$;
  var modal = document.getElementById("plan-modal");
  var form = document.getElementById("plan-form");
  var title = document.getElementById("plan-modal-title");

  function openForm(btn) {
    form.reset();
    Ajax.clearErrors(form);
    var f = fields();
    if (btn) {
      title.textContent = "Chỉnh sửa gói dịch vụ";
      f.id.value = btn.dataset.id;
      f.name.value = btn.dataset.name;
      f.pricePerMonth.value = btn.dataset.price;
      f.maxBranches.value = btn.dataset.branches;
      f.maxStaff.value = btn.dataset.staff;
      f.trialDays.value = btn.dataset.trial || "";
      f.isActive.checked = btn.dataset.active === "true";
    } else {
      title.textContent = "Thêm gói dịch vụ";
      f.id.value = "";
      f.pricePerMonth.value = "0";
      f.maxBranches.value = "1";
      f.maxStaff.value = "3";
    }
    modal.showModal();
    f.name.focus();
  }

  /** Lấy ô theo name bằng namedItem: tránh trùng với thuộc tính sẵn có của form (form.name, form.id). */
  function fields() {
    var get = function (n) { return form.elements.namedItem(n); };
    return { id: get("id"), name: get("name"), pricePerMonth: get("pricePerMonth"), maxBranches: get("maxBranches"),
      maxStaff: get("maxStaff"), trialDays: get("trialDays"), isActive: get("isActive") };
  }

  function toIntOrNull(value) { return value === "" ? null : parseInt(value, 10); }

  document.addEventListener("click", function (event) {
    var btn = event.target.closest("[data-action]");
    if (!btn) return;

    if (btn.dataset.action === "add") openForm(null);
    else if (btn.dataset.action === "edit") openForm(btn);
    else if (btn.dataset.action === "toggle") {
      var active = btn.dataset.active === "true";
      ManageUi.confirmAction({
        title: active ? "Ngừng cung cấp gói" : "Cung cấp lại gói",
        message: active
          ? "Gói \"" + btn.dataset.name + "\" sẽ không hiện ở trang đăng ký nữa. Các quán đang dùng gói này không bị ảnh hưởng."
          : "Gói \"" + btn.dataset.name + "\" sẽ hiện lại ở trang đăng ký.",
        okText: active ? "Ngừng cung cấp" : "Cung cấp lại",
        onConfirm: async function () {
          await Ajax.postJson("/system/plans/" + btn.dataset.id + "/toggle", {});
          location.reload();
        }
      });
    }
  });

  form.addEventListener("submit", async function (event) {
    event.preventDefault();
    var f = fields();
    if (!f.name.value.trim()) {
      Ajax.showErrors(form, { name: ["Vui lòng nhập tên gói."] });
      return;
    }

    var payload = {
      id: toIntOrNull(f.id.value),
      name: f.name.value.trim(),
      pricePerMonth: Number(f.pricePerMonth.value || 0),
      maxBranches: toIntOrNull(f.maxBranches.value),
      maxStaff: toIntOrNull(f.maxStaff.value),
      trialDays: toIntOrNull(f.trialDays.value),
      isActive: f.isActive.checked
    };

    var submit = $("[data-submit]", form);
    ManageUi.setBusy(modal, true);
    Ajax.setLoading(submit, true, "Đang lưu...");
    try {
      await Ajax.postJson("/system/plans/save", payload);
      ManageUi.closeDialog(modal);
      location.reload();
    } catch (err) {
      if (err.status === 400) Ajax.showErrors(form, (err.data && err.data.errors) || { "": [err.message] });
    } finally {
      ManageUi.setBusy(modal, false);
      Ajax.setLoading(submit, false);
    }
  });
})();
