using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public Guid? InstitutionId { get; set; }
        public string? StudentCode { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }
        public AppRole Role { get; set; }
        public UserStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
