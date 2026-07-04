using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[ApiController]
[Authorize]
[Route("api/email-test")]
public class EmailTestController : ControllerBase
{
    private readonly IEmailService _emailService;

    public EmailTestController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost("biometric-approved")]
    public async Task<IActionResult> SendBiometricApprovedAsync([FromBody] BiometricApprovedEmailTestRequestDto request)
    {
        await _emailService.SendBiometricApprovedAsync(request.Email, request.StudentName);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Biometric approval email sent successfully."));
    }

    [HttpPost("biometric-rejected")]
    public async Task<IActionResult> SendBiometricRejectedAsync([FromBody] BiometricRejectedEmailTestRequestDto request)
    {
        await _emailService.SendBiometricRejectedAsync(request.Email, request.StudentName, request.Reason);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Biometric rejection email sent successfully."));
    }

    [HttpPost("exam-reminder")]
    public async Task<IActionResult> SendExamReminderAsync([FromBody] ExamEmailTestRequestDto request)
    {
        await _emailService.SendExamReminderAsync(request.Email, request.StudentName, request.ExamName, request.ExamTime);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Exam reminder email sent successfully."));
    }

    [HttpPost("exam-created")]
    public async Task<IActionResult> SendExamCreatedAsync([FromBody] ExamEmailTestRequestDto request)
    {
        await _emailService.SendExamCreatedAsync(request.Email, request.StudentName, request.ExamName, request.ExamTime);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Exam created email sent successfully."));
    }

    [HttpPost("attendance-started")]
    public async Task<IActionResult> SendAttendanceStartedAsync([FromBody] AttendanceStartedEmailTestRequestDto request)
    {
        await _emailService.SendAttendanceStartedAsync(request.Email, request.StudentName, request.ClassName);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Attendance started email sent successfully."));
    }

    [HttpPost("subscription-expiring")]
    public async Task<IActionResult> SendSubscriptionExpiringAsync([FromBody] SubscriptionExpiringEmailTestRequestDto request)
    {
        await _emailService.SendSubscriptionExpiringAsync(request.Email, request.InstitutionName, request.ExpiryDate);

        return Ok(ApiResponse<string>.OnSuccess(request.Email, "Subscription expiring email sent successfully."));
    }
}
