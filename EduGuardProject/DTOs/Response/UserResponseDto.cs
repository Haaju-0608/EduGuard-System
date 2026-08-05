using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class BulkImportUsersResponseDto
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<BulkImportUserRowResultDto> Results { get; set; } = [];
    }

    public class BulkImportUserRowResultDto
    {
        public int Row { get; set; }
        public string? Email { get; set; }
        public bool Success { get; set; }
        public Guid? UserId { get; set; }
        public string? Error { get; set; }
    }

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
