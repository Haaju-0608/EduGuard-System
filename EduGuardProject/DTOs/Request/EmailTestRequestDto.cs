using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request;

public class BiometricApprovedEmailTestRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string StudentName { get; set; } = string.Empty;
}

public class BiometricRejectedEmailTestRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string StudentName { get; set; } = string.Empty;

    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ExamEmailTestRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string StudentName { get; set; } = string.Empty;

    [Required]
    public string ExamName { get; set; } = string.Empty;

    [Required]
    public DateTime ExamTime { get; set; }
}

public class AttendanceStartedEmailTestRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string StudentName { get; set; } = string.Empty;

    [Required]
    public string ClassName { get; set; } = string.Empty;
}

public class SubscriptionExpiringEmailTestRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string InstitutionName { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiryDate { get; set; }
}
