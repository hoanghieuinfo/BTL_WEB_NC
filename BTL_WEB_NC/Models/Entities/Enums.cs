// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Models.Entities;

// Kiểu nền byte → cột tinyint đúng Bảng 3.10, 3.13, 3.17.

public enum TenantStatus : byte
{
    PendingActivation = 0, // Chờ kích hoạt
    Active = 1,            // Hoạt động
    Locked = 2,            // Bị khóa
    Rejected = 3           // Từ chối
}

public enum TableStatus : byte
{
    Empty = 0,          // Trống
    Serving = 1,        // Đang phục vụ
    AwaitingPayment = 2 // Chờ thanh toán
}

public enum OrderStatus : byte
{
    Serving = 0,   // Đang phục vụ
    Paid = 1,      // Đã thanh toán
    Cancelled = 2  // Đã hủy
}

public enum PaymentMethod : byte
{
    Cash = 0,         // Tiền mặt
    BankTransfer = 1  // Chuyển khoản
}
