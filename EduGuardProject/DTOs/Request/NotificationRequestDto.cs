using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class CreateNotificationDto : IValidatableObject
    {
        [Required(ErrorMessage = "UserId is required.")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(255, ErrorMessage = "Title must not exceed 255 characters.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Body is required.")]
        public string Body { get; set; } = null!;

        [Required(ErrorMessage = "Notification type is required.")]
        public NotificationType Type { get; set; }

        public NotificationChannel SentVia { get; set; } = NotificationChannel.Dashboard;

        public Guid? ReferenceId { get; set; }
        public ReferenceTypeEnum? ReferenceType { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ReferenceId.HasValue && ReferenceType is null)
            {
                yield return new ValidationResult(
                    "ReferenceType is required when ReferenceId is provided.",
                    new[] { nameof(ReferenceType) });
            }
            if (!ReferenceId.HasValue && ReferenceType.HasValue)
            {
                yield return new ValidationResult(
                    "ReferenceId is required when ReferenceType is provided.",
                    new[] { nameof(ReferenceId) });
            }
        }
    }
}