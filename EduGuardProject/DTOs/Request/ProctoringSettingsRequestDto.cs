using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class ViolationTypeThresholdDto
    {
        [Required]
        public ViolationType ViolationType { get; set; }

        [Range(1, 3600, ErrorMessage = "Detection threshold seconds must be between 1 and 3600.")]
        public int DetectionThresholdSeconds { get; set; }
    }

    public class CreateProctoringSettingsDto
    {
        /// <summary>Null = system-wide default config. SchoolAdmin can only create/update their own institution's.</summary>
        public Guid? InstitutionId { get; set; }

        [Range(1, 1000, ErrorMessage = "Max AI violation count must be greater than 0.")]
        public int MaxAiViolationCount { get; set; }

        [Range(0, 3600, ErrorMessage = "Cooldown seconds must be between 0 and 3600.")]
        public int CooldownSeconds { get; set; }

        public bool AllowConsecutiveSameType { get; set; }

        [Range(1, 1000, ErrorMessage = "AI notify threshold must be greater than 0.")]
        public int AiNotifyThreshold { get; set; }

        [Range(1, 1000, ErrorMessage = "Browser notify threshold must be greater than 0.")]
        public int BrowserNotifyThreshold { get; set; }

        public List<ViolationTypeThresholdDto> ViolationTypeThresholds { get; set; } = new();
    }

    public class UpdateProctoringSettingsDto
    {
        [Range(1, 1000, ErrorMessage = "Max AI violation count must be greater than 0.")]
        public int MaxAiViolationCount { get; set; }

        [Range(0, 3600, ErrorMessage = "Cooldown seconds must be between 0 and 3600.")]
        public int CooldownSeconds { get; set; }

        public bool AllowConsecutiveSameType { get; set; }

        [Range(1, 1000, ErrorMessage = "AI notify threshold must be greater than 0.")]
        public int AiNotifyThreshold { get; set; }

        [Range(1, 1000, ErrorMessage = "Browser notify threshold must be greater than 0.")]
        public int BrowserNotifyThreshold { get; set; }

        public bool IsActive { get; set; } = true;

        public List<ViolationTypeThresholdDto> ViolationTypeThresholds { get; set; } = new();
    }
}
