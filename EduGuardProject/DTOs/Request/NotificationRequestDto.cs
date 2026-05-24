using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    public class CreateNotificationDto
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public NotificationType Type { get; set; }
        public NotificationChannel SentVia { get; set; } = NotificationChannel.Dashboard;

        public Guid? ReferenceId { get; set; }
        public ReferenceTypeEnum? ReferenceType { get; set; }
    }
}
