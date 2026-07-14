using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace EduGuardProject.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailTemplateService _templateService;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        EmailTemplateService templateService,
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger)
    {
        _templateService = templateService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string html)
    {
        ValidateEmailRequest(to, subject, html);
        ValidateSettings();

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject.Trim(),
            Body = html,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(to.Trim()));

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.AppPassword)
        };

        try
        {
            _logger.LogInformation("Sending email to {Recipient} with subject {Subject}.", to, subject);
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {Recipient} with subject {Subject}.", to, subject);
        }
        catch (EmailSendException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject {Subject}.", to, subject);
            throw new EmailSendException("Failed to send email.", ex);
        }
    }

    public Task SendExamReminderAsync(string email, string studentName, string examName, DateTime examTime)
    {
        var html = _templateService.BuildExamReminderTemplate(studentName, examName, examTime);
        return SendAsync(email, $"Exam reminder: {examName}", html);
    }

    public Task SendExamCreatedAsync(string email, string studentName, string examName, DateTime examTime)
    {
        var html = _templateService.BuildExamCreatedTemplate(studentName, examName, examTime);
        return SendAsync(email, $"New exam scheduled: {examName}", html);
    }

    public Task SendBiometricApprovedAsync(string email, string studentName)
    {
        var html = _templateService.BuildBiometricApprovedTemplate(studentName);
        return SendAsync(email, "Biometric registration approved", html);
    }

    public Task SendBiometricRejectedAsync(string email, string studentName, string reason)
    {
        var html = _templateService.BuildBiometricRejectedTemplate(studentName, reason);
        return SendAsync(email, "Biometric registration rejected", html);
    }

    public Task SendAttendanceStartedAsync(string email, string studentName, string className)
    {
        var html = _templateService.BuildAttendanceStartedTemplate(studentName, className);
        return SendAsync(email, $"Attendance started: {className}", html);
    }

    public Task SendSubscriptionExpiringAsync(string email, string institutionName, DateTime expiryDate)
    {
        var html = _templateService.BuildSubscriptionTemplate(institutionName, expiryDate);
        return SendAsync(email, "EduGuard subscription expiring soon", html);
    }

    private static void ValidateEmailRequest(string to, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Recipient email is required.", nameof(to));

        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Email subject is required.", nameof(subject));

        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("Email HTML content is required.", nameof(html));
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            throw new EmailSendException("EmailSettings:SmtpHost is not configured.");

        if (_settings.SmtpPort <= 0)
            throw new EmailSendException("EmailSettings:SmtpPort is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.Username))
            throw new EmailSendException("EmailSettings:Username is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.AppPassword))
            throw new EmailSendException("EmailSettings:AppPassword is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.From))
            throw new EmailSendException("EmailSettings:From is not configured.");
    }
}
