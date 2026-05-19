using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;
    }

    // 2. DTO cho Super Admin tạo School Admin
    public class CreateSchoolAdminDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Phải chọn trường (Institution) cho Admin này")]
        public Guid InstitutionId { get; set; }
    }

    // 3. DTO cho School Admin tạo Giảng viên / Sinh viên
    public class CreateUserDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Phải xác định quyền (LECTURER hoặc STUDENT)")]
        public AppRole Role { get; set; }

        // Sinh viên thì bắt buộc phải có mã số, Giảng viên thì không cần
        public string? StudentCode { get; set; }
    }
}