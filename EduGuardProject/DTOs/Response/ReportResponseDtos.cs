using EduGuardProject.Models;
using System.Text.Json.Serialization;

namespace EduGuardProject.DTOs.Response;

public sealed class AttendanceReportResponseDto
{
    public AttendanceReportFilterDto Filters { get; set; } = new();
    public AttendanceReportSummaryDto Summary { get; set; } = new();
    public List<AttendanceReportItemDto> Items { get; set; } = [];
}

public sealed class AttendanceReportFilterDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ClassId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class AttendanceReportSummaryDto
{
    public int Sessions { get; set; }
    public int Completed { get; set; }
    public int TotalRecognized { get; set; }
    public double AverageRecognitionRate { get; set; }
}

public sealed class AttendanceReportItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    public string InstitutionName { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public string? CourseCode { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; }
    public int TotalRecognized { get; set; }
    public int TotalStudents { get; set; }
    public int Records { get; set; }
}

public sealed class ViolationReportResponseDto
{
    public ViolationReportFilterDto Filters { get; set; } = new();
    public ViolationReportSummaryDto Summary { get; set; } = new();
    public List<ViolationReportItemDto> Items { get; set; } = [];
}

public sealed class ViolationReportFilterDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ExamSlotId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class ViolationReportSummaryDto
{
    public int Total { get; set; }
    public int Reviewed { get; set; }
    public List<ViolationTypeCountDto> ByType { get; set; } = [];
    public List<ViolationSeverityCountDto> BySeverity { get; set; } = [];
}

public sealed class ViolationTypeCountDto
{
    public string Type { get; set; } = null!;
    public int Count { get; set; }
}

public sealed class ViolationSeverityCountDto
{
    public string Severity { get; set; } = null!;
    public int Count { get; set; }
}

public sealed class ViolationReportItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ViolationId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ParticipationId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    public string InstitutionName { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ExamSlotId { get; set; }
    public string ExamName { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public ViolationType Type { get; set; }
    public ViolationSeverity Severity { get; set; }
    public double? AiConfidence { get; set; }
    public string? EvidencePath { get; set; }
    public bool IsReviewed { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class WalletReportResponseDto
{
    public WalletReportFilterDto Filters { get; set; } = new();
    public WalletReportSummaryDto Summary { get; set; } = new();
    public List<WalletReportItemDto> Items { get; set; } = [];
}

public sealed class WalletReportFilterDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? WalletId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class WalletReportSummaryDto
{
    public int TotalTransactions { get; set; }
    public decimal SuccessAmount { get; set; }
    public decimal TopUpAmount { get; set; }
    public decimal FeeAmount { get; set; }
}

public sealed class WalletReportItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? InstitutionId { get; set; }
    public string InstitutionName { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? WalletId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
