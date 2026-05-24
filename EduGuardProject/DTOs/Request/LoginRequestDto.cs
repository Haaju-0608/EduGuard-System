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

    // 3. DTO cho School Admin tạo Giảng viên / Sinh viên và đăng kí Auth luôn nha 
    public class CreateUserDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Phải xác định quyền (SUPER_ADMIN, SCHOOL_ADMIN, LECTURER hoặc STUDENT)")]
        public AppRole Role { get; set; }

        // Sinh viên thì bắt buộc phải có mã số, Giảng viên thì không cần
        public string? StudentCode { get; set; }

        //  Bổ sung các trường này từ API User vào đây để đồng bộ 100%
        public Guid? InstitutionId { get; set; }
        public string? Phone { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Active;
    }
}