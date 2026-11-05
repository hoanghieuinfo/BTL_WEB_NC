// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Models;

/// <summary>MH18 – Đổi mật khẩu tài khoản (R03, UC03).</summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu mới cần tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không trùng khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
