using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response;

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? StudentCode { get; set; }
}

public class InstitutionSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public class ClassSummaryDto
{
    public Guid Id { get; set; }
    public string CourseName { get; set; } = null!;
    public string? CourseCode { get; set; }
}

public class ClassResponseDto
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid LecturerId { get; set; }
    public string CourseName { get; set; } = null!;
    public string? CourseCode { get; set; }
    public string Semester { get; set; } = null!;
    public string AcademicYear { get; set; } = null!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public InstitutionSummaryDto? Institution { get; set; }
    public UserSummaryDto? Lecturer { get; set; }
    public List<ClassEnrollmentResponseDto>? Enrollments { get; set; }
}

public class ClassEnrollmentResponseDto
{
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTime EnrolledAt { get; set; }

    public ClassSummaryDto? Class { get; set; }
    public UserSummaryDto? Student { get; set; }
}

public class AttendanceSessionResponseDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? BillingTransId { get; set; }
    public string? VideoPath { get; set; }
    public int TotalRecognized { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ClassSummaryDto? Class { get; set; }
    public UserSummaryDto? Creator { get; set; }
    public List<AttendanceRecordResponseDto>? Records { get; set; }
}

public class AttendanceRecordResponseDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? SnapshotPath { get; set; }
    public DateTime? CheckinAt { get; set; }
    public Guid? AdjustedBy { get; set; }
    public DateTime? AdjustedAt { get; set; }
    public AttendanceMethod Method { get; set; }
    public AttendanceStatus Status { get; set; }

    public UserSummaryDto? Student { get; set; }
    public AttendanceSessionSummaryDto? Session { get; set; }
}

public class AttendanceSessionSummaryDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime StartTime { get; set; }
}

public class BiometricRequestResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string Reason { get; set; } = null!;
    public BiometricReqStatus Status { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserSummaryDto? Student { get; set; }
    public UserSummaryDto? Approver { get; set; }
}

public class BiometricDatumResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BioRequestId { get; set; }
    public string ModelVersion { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? FaceImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public UserSummaryDto? User { get; set; }
}

public class ExamslotReponseDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string ExamName { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExpectedDurationMinutes { get; set; }
    public ExamSlotStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ExamParticipationResponseDto
{
    public Guid Id { get; set; }
    public Guid ExamSlotId { get; set; }
    public Guid? BillingTransId { get; set; }
    public Guid StudentId { get; set; }

    public DateTime? ActualStart { get; set; }

    public DateTime? ActualEnd { get; set; }
    public ParticipationStatus Status { get; set; }
    public string? DisqualifiedReason { get; set; }
    public string? RecordingVideoPath { get; set; }

    public string? IdentitySnapshotPath { get; set; }

}

public class ViolationlogResponeDto
{
    public Guid Id { get; set; }

    public Guid ParticipationId { get; set; }

    public string? EvidencePath { get; set; }

    public ViolationSeverity Severity { get; set; }

    public ViolationType ViolationType { get; set; }

    public double? AiConfidence { get; set; }

    public bool IsReviewed { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime RecordedAt { get; set; }
}
