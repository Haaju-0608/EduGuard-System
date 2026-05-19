using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class ContactRequestDto
    {
        [Required(ErrorMessage = "Tên trường học không được để trống.")]
        [StringLength(150, ErrorMessage = "Tên trường không được quá 150 ký tự.")]
        public string SchoolName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên người liên hệ không được để trống.")]
        public string ContactPersonName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email liên hệ không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống.")]
        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Nội dung lời nhắn không được vượt quá 500 ký tự.")]
        public string? Message { get; set; }
    }
}
