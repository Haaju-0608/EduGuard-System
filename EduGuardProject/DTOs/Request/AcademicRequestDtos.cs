using System.ComponentModel.DataAnnotations;
using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request;

public class CreateClassDto
{
    [Required]
    public Guid InstitutionId { get; set; }

    [Required]
    public Guid LecturerId { get; set; }

    [Required, MaxLength(255)]
    public string CourseName { get; set; } = null!;

    [MaxLength(50)]
    public string? CourseCode { get; set; }

    [Required, MaxLength(50)]
    public string Semester { get; set; } = null!;

    [Required, MaxLength(20)]
    public string AcademicYear { get; set; } = null!;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class UpdateClassDto
{
    [Required, MaxLength(255)]
    public string CourseName { get; set; } = null!;

    [MaxLength(50)]
    public string? CourseCode { get; set; }

    [Required, MaxLength(50)]
    public string Semester { get; set; } = null!;

    [Required, MaxLength(20)]
    public string AcademicYear { get; set; } = null!;

    public Guid LecturerId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class CreateClassEnrollmentDto
{
    [Required]
    public Guid ClassId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
}

public class UpdateClassEnrollmentDto
{
    public EnrollmentStatus Status { get; set; }
}

public class CreateAttendanceSessionDto
{
    [Required]
    public Guid ClassId { get; set; }

    public Guid? ExamSlotId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public string? VideoPath { get; set; }
    // Vì video sẽ được upload riêng ở endpoint /ai-video sau khi phiên đã tạo xong.
}

public class UpdateAttendanceSessionDto
{
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; }
    public string? VideoPath { get; set; }
    public int? TotalRecognized { get; set; }
}

public class CreateAttendanceRecordDto
{
    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public AttendanceMethod Method { get; set; } = AttendanceMethod.Manual;
    public double? ConfidenceScore { get; set; }
    public string? SnapshotPath { get; set; }
    public DateTime? CheckinAt { get; set; }
}

public class BulkManualAttendanceDto
{
    [Required]
    public List<Guid> PresentStudentIds { get; set; } = [];

    public AttendanceMethod Method { get; set; } = AttendanceMethod.Manual;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
}

public class UpdateAttendanceRecordDto
{
    public AttendanceStatus Status { get; set; }
    public AttendanceMethod Method { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? SnapshotPath { get; set; }
    public DateTime? CheckinAt { get; set; }
}

public class AiVideoAttendanceDto
{
    public IFormFile VideoFile { get; set; }
}

public class CreateBiometricRequestDto
{
    [Required]
    public string Reason { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng upload ảnh góc thẳng")]
    public IFormFile FrontFile { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng upload ảnh nghiêng trái")]
    public IFormFile LeftFile { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng upload ảnh nghiêng phải")]
    public IFormFile RightFile { get; set; } = null!;
}

public class ReviewBiometricRequestDto
{
    public string? Reason { get; set; }
}

public class CreateBiometricDatumDto
{
    [Required]
    public Guid UserId { get; set; }

    public Guid? BioRequestId { get; set; }

    [Required, MaxLength(50)]
    public string ModelVersion { get; set; } = null!;

    [MaxLength(500)]
    public string? FaceImageUrl { get; set; }
}

public class UpdateBiometricDatumDto
{
    [MaxLength(50)]
    public string? ModelVersion { get; set; }

    [MaxLength(500)]
    public string? FaceImageUrl { get; set; }

    public bool? IsActive { get; set; }
}

public class CreateExamParticipationDto
{
    [Required]
    public Guid ExamSlotId { get; set; }
    [Required]
    public Guid StudentId { get; set; }
    public Guid? BillingTransId { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public ParticipationStatus Status { get; set; } = ParticipationStatus.Absent;
    public string? DisqualifiedReason { get; set; }
    public string? RecordingVideoPath { get; set; }
    public string? IdentitySnapshotPath { get; set; }
}

public class UpdateExamParticipationDto
{

    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public ParticipationStatus Status { get; set; }
    public string? DisqualifiedReason { get; set; }
    public string? RecordingVideoPath { get; set; }
    public string? IdentitySnapshotPath { get; set; }
}
public class UpdateExamParticipationStatusDto
{

    public ParticipationStatus Status { get; set; }
}

public class CreateStudentExamRecordDto
{
    [Required]
    public Guid ExamSlotId { get; set; }

    [Required]
    public Guid StudentId { get; set; }

    public DateTime? EndedAt { get; set; }
    public string? ExamRecord { get; set; }
    public decimal? FinalScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public StudentExamRecordStatus Status { get; set; } = StudentExamRecordStatus.Marked;
}

public class UpdateStudentExamRecordDto
{
    public DateTime? EndedAt { get; set; }
    public string? ExamRecord { get; set; }
    public decimal? FinalScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public StudentExamRecordStatus Status { get; set; }
}

public class GradeStudentExamRecordDto
{
    [Required]
    public List<GradeStudentAnswerDto> Grades { get; set; } = [];
}

public class GradeStudentAnswerDto
{
    [Required]
    public Guid QuestionId { get; set; }

    public decimal AwardedPoints { get; set; }
}

public class SubmitStudentExamRecordDto
{
    [Required]
    public Guid ExamSlotId { get; set; }

    [Required]
    public List<SubmitStudentAnswerDto> Answers { get; set; } = [];

    public int? DurationSeconds { get; set; }
}

public class SubmitStudentAnswerDto
{
    [Required]
    public Guid QuestionId { get; set; }

    public Guid? OptionId { get; set; }

    [MaxLength(10)]
    public string? SelectedOption { get; set; }

    public string? AnswerText { get; set; }
}

public class CreateExamQuestionDto
{
    [Required]
    public Guid ExamSlotId { get; set; }

    [Required, MaxLength(30)]
    public string QuestionType { get; set; } = null!;

    [Required]
    public string QuestionContent { get; set; } = null!;

    [MaxLength(500)]
    public string? AudioUrl { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public decimal Points { get; set; } = 1;

    public int DisplayOrder { get; set; }

    public List<CreateQuestionOptionDto>? Options { get; set; }
}

public class UpdateExamQuestionDto
{
    [Required, MaxLength(30)]
    public string QuestionType { get; set; } = null!;

    [Required]
    public string QuestionContent { get; set; } = null!;

    [MaxLength(500)]
    public string? AudioUrl { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public decimal Points { get; set; } = 1;

    public int DisplayOrder { get; set; }
}

public class CreateQuestionOptionDto
{
    [Required, MaxLength(10)]
    public string OptionLabel { get; set; } = null!;

    [Required]
    public string OptionContent { get; set; } = null!;

    public bool IsCorrect { get; set; }
}

public class UpdateQuestionOptionDto
{
    [Required, MaxLength(10)]
    public string OptionLabel { get; set; } = null!;

    [Required]
    public string OptionContent { get; set; } = null!;

    public bool IsCorrect { get; set; }
}


public class CreateExamSlotDto
{
    [Required]
    public Guid ClassId { get; set; }

    public Guid? LecturerId { get; set; }

    /// <summary>Lecturer assigned to proctor this exam, if different from the class's own lecturer.</summary>
    public Guid? ProctorId { get; set; }

    [Required]
    [MaxLength(255)]
    public string ExamName { get; set; } = string.Empty;

    public ExamSlotStatus Status { get; set; }
    public int ExpectedDurationMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime StartTime { get; set; }
    [Required]
    public DateTime EndTime { get; set; }
}

public class UpdateExamSlotDto
{
    [MaxLength(255)]
    public string? ExamName { get; set; }

    public Guid? LecturerId { get; set; }

    /// <summary>Lecturer assigned to proctor this exam, if different from the class's own lecturer.</summary>
    public Guid? ProctorId { get; set; }

    public int ExpectedDurationMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class CreateViolationLogDto
{
    [Required]
    public Guid ParticipationId { get; set; }
    public ViolationSeverity severity { get; set; }
    public ViolationType violationType { get; set; }
    [MaxLength(500)]
    public string? EvidencePath { get; set; }
    public double? AiConfidence { get; set; }
    public Guid? ReviewedBy { get; set; }

    public DateTime RecordedAt { get; set; }

}

public class UpdateViolationLogDto
{
    public bool IsReviewed { get; set; }
    public Guid? ReviewedBy { get; set; }
}

public class BrowserViolationRequestDto
{
    [Required]
    public Guid ParticipationId { get; set; }

    [Required]
    public ViolationType ViolationType { get; set; }
}
