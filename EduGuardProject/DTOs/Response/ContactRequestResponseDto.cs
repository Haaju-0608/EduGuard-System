namespace EduGuardProject.DTOs.Response
{
    public class ContactRequestResponseDto
    {
        public Guid Id { get; set; }
        public string SchoolName { get; set; } = null!;
        public string ContactPersonName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Message { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
