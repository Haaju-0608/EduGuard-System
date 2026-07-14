using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request;

public class ExamHeartbeatRequestDto
{
    public DateTime? ClientTime { get; set; }
}

public class ExamSubmitRequestDto
{
    [MaxLength(500)]
    public string? RecordingVideoPath { get; set; }
}

public class ExamLeaveRequestDto
{
    [MaxLength(255)]
    public string? Reason { get; set; }
}

public class ExamDisqualifyRequestDto
{
    [Required, MaxLength(500)]
    public string Reason { get; set; } = null!;
}
