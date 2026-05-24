using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    public class UpdateUserDto
    {
        public Guid? InstitutionId { get; set; }
        public string? StudentCode { get; set; }
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }
        public AppRole Role { get; set; }
        public UserStatus Status { get; set; }
    }
}
