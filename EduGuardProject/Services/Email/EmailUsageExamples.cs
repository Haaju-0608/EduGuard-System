namespace EduGuardProject.Services.Email;

public sealed class EmailUsageExamples
{
    private readonly IEmailService _emailService;

    public EmailUsageExamples(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendBiometricApprovedExampleAsync()
    {
        await _emailService.SendBiometricApprovedAsync(
            "student@example.com",
            "Nguyen Van A");
    }

    public async Task SendExamReminderExampleAsync()
    {
        await _emailService.SendExamReminderAsync(
            "student@example.com",
            "Nguyen Van A",
            "Final Mathematics Exam",
            DateTime.UtcNow.AddMinutes(15));
    }

    // Future background schedulers can call IEmailService for:
    // Exam Reminder, Subscription Reminder, and Wallet Reminder emails.
}
