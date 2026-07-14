namespace EduGuardProject.Services.Email;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string html);

    Task SendExamReminderAsync(
        string email,
        string studentName,
        string examName,
        DateTime examTime);

    Task SendExamCreatedAsync(
        string email,
        string studentName,
        string examName,
        DateTime examTime);

    Task SendBiometricApprovedAsync(
        string email,
        string studentName);

    Task SendBiometricRejectedAsync(
        string email,
        string studentName,
        string reason);

    Task SendAttendanceStartedAsync(
        string email,
        string studentName,
        string className);

    Task SendSubscriptionExpiringAsync(
        string email,
        string institutionName,
        DateTime expiryDate);
}
