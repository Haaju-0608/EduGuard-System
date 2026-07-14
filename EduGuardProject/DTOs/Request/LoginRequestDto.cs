using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    // 1. DTO cho Login
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;
    }

    // 2. DTO cho Super Admin tạo School Admin
    public class CreateSchoolAdminDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Institution ID is required for this Admin.")]
        public Guid InstitutionId { get; set; }
    }

    // 3. DTO cho School Admin tạo giảng viên hoặc sinh viên.
    public class CreateUserDto : IValidatableObject
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Role (SUPER_ADMIN, SCHOOL_ADMIN, LECTURER, or STUDENT) is required.")]
        public AppRole Role { get; set; }

        public string? StudentCode { get; set; }

        public Guid? InstitutionId { get; set; }
        public string? Phone { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Active;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Role == AppRole.Student && string.IsNullOrWhiteSpace(StudentCode))
            {
                yield return new ValidationResult(
                    "Student code is required when creating a STUDENT account.",
                    new[] { nameof(StudentCode) }
                );
            }
        }
    }
}
