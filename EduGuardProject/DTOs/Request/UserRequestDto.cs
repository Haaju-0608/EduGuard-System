using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class BulkImportUsersRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }

    public class UpdateUserDto
    {
        public Guid? InstitutionId { get; set; }

        [MaxLength(50, ErrorMessage = "Student code must not exceed 50 characters.")]
        public string? StudentCode { get; set; }

        [MaxLength(255, ErrorMessage = "Full name must not exceed 255 characters.")]
        public string? FullName { get; set; }             

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        public AppRole? Role { get; set; }                  

        public UserStatus? Status { get; set; }             
    }

    public class UpdateMyProfileDto
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [MaxLength(20)]
        public string? Phone { get; set; }
    }
}