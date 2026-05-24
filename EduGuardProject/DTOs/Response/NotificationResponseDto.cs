namespace EduGuardProject.DTOs.Response
{
    public class NotificationResponseDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!; 
        public bool IsRead { get; set; }
        public string Type { get; set; } = null!;
        public string SentVia { get; set; } = null!;
        public Guid? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
