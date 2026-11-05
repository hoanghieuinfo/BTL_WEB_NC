// [AI] Claude hỗ trợ – Module 1 (Nguyễn Thành Đạt)
using System.ComponentModel.DataAnnotations;

namespace BTL_WEB_NC.Models;

/// <summary>BM01 – Phiếu đăng ký sử dụng dịch vụ (R01, UC01).</summary>
public class RegisterTenantViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên quán cà phê.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên quán từ 3 đến 100 ký tự.")]
    [Display(Name = "Tên quán cà phê")]
    public string ShopName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đường dẫn riêng của quán.")]
    [StringLength(60, MinimumLength = 3, ErrorMessage = "Đường dẫn từ 3 đến 60 ký tự.")]
    [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Đường dẫn chỉ gồm chữ thường không dấu, số và gạch ngang (ví dụ: ca-phe-may).")]
    [Display(Name = "Đường dẫn riêng (slug)")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên chủ quán.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên chủ quán")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu cần tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 chữ số.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không trùng khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn gói dịch vụ.")]
    [Display(Name = "Gói dịch vụ")]
    public int ServicePlanId { get; set; } = 1;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với Điều khoản dịch vụ và Chính sách bảo mật.")]
    public bool AgreeTerms { get; set; }
}
