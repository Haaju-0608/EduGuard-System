using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class ProctoringSettingsResponseDto
    {
        public Guid Id { get; set; }
        public Guid? InstitutionId { get; set; }
        public int MaxAiViolationCount { get; set; }
        public int CooldownSeconds { get; set; }
        public bool AllowConsecutiveSameType { get; set; }
        public int AiNotifyThreshold { get; set; }
        public int BrowserNotifyThreshold { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ViolationTypeThresholdResponseDto> ViolationTypeThresholds { get; set; } = new();
    }

    public class ViolationTypeThresholdResponseDto
    {
        public ViolationType ViolationType { get; set; }
        public double DetectionThresholdSeconds { get; set; }
    }
}
