using System.Net;

namespace EduGuardProject.Services.Email;

public class EmailTemplateService : IEmailTemplateService
{
    public string BuildExamReminderTemplate(string studentName, string examName, DateTime examTime)
    {
        var body = $"""
            <p>Hello {Encode(studentName)},</p>
            <p>This is a reminder that your exam is coming soon.</p>
            {BuildInfoTable([
                ("Exam", examName),
                ("Start time", FormatDateTime(examTime))
            ])}
            <p>Please prepare your device, internet connection, and biometric verification before the exam starts.</p>
            """;

        return BuildLayout("Exam reminder", "Your exam is coming soon", body);
    }

    public string BuildExamCreatedTemplate(string studentName, string examName, DateTime examTime)
    {
        var body = $"""
            <p>Hello {Encode(studentName)},</p>
            <p>A new exam has been scheduled for your class.</p>
            {BuildInfoTable([
                ("Exam", examName),
                ("Start time", FormatDateTime(examTime))
            ])}
            <p>You can review the exam information in EduGuard.</p>
            """;

        return BuildLayout("Exam created", "A new exam has been scheduled", body);
    }

    public string BuildBiometricApprovedTemplate(string studentName)
    {
        var body = $"""
            <p>Hello {Encode(studentName)},</p>
            <p>Your biometric registration has been approved.</p>
            <p>You can now use biometric verification for attendance and exam participation in EduGuard.</p>
            """;

        return BuildLayout("Biometric approved", "Your biometric verification is ready", body);
    }

    public string BuildBiometricRejectedTemplate(string studentName, string reason)
    {
        var body = $"""
            <p>Hello {Encode(studentName)},</p>
            <p>Your biometric registration was rejected.</p>
            {BuildInfoTable([
                ("Reason", reason)
            ])}
            <p>Please update your biometric information and submit it again.</p>
            """;

        return BuildLayout("Biometric rejected", "Please update your biometric information", body);
    }

    public string BuildAttendanceStartedTemplate(string studentName, string className)
    {
        var body = $"""
            <p>Hello {Encode(studentName)},</p>
            <p>Attendance has started for your class.</p>
            {BuildInfoTable([
                ("Class", className)
            ])}
            <p>Please check in before the attendance session closes.</p>
            """;

        return BuildLayout("Attendance started", "Attendance is now open", body);
    }

    public string BuildSubscriptionTemplate(string institutionName, DateTime expiryDate)
    {
        var body = $"""
            <p>Hello {Encode(institutionName)},</p>
            <p>Your EduGuard subscription is close to its expiry date.</p>
            {BuildInfoTable([
                ("Institution", institutionName),
                ("Expiry date", FormatDate(expiryDate))
            ])}
            <p>Please renew your subscription to keep EduGuard services active.</p>
            """;

        return BuildLayout("Subscription expiring", "Your subscription is expiring soon", body);
    }

    private static string BuildLayout(string previewTitle, string heading, string body)
    {
        return $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{Encode(previewTitle)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#172033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#ffffff;border:1px solid #dce4f2;border-radius:8px;overflow:hidden;">
                      <tr>
                        <td style="background:#10233f;color:#ffffff;padding:22px 24px;">
                          <div style="font-size:13px;letter-spacing:.4px;text-transform:uppercase;color:#a8d5ff;">EduGuard</div>
                          <h1 style="margin:8px 0 0;font-size:24px;line-height:1.3;">{Encode(heading)}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;font-size:15px;line-height:1.7;">
                          {body}
                        </td>
                      </tr>
                      <tr>
                        <td style="border-top:1px solid #e7edf6;padding:16px 24px;color:#68758a;font-size:12px;line-height:1.5;">
                          This is an automated email from EduGuard. Please do not reply directly to this message.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildInfoTable(IEnumerable<(string Label, string Value)> rows)
    {
        var tableRows = string.Join(
            string.Empty,
            rows.Select(row => $"""
                <tr>
                  <td style="padding:10px 12px;border-bottom:1px solid #e7edf6;color:#5d6b80;width:35%;">{Encode(row.Label)}</td>
                  <td style="padding:10px 12px;border-bottom:1px solid #e7edf6;font-weight:600;color:#172033;">{Encode(row.Value)}</td>
                </tr>
                """));

        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #e7edf6;border-radius:6px;border-collapse:separate;border-spacing:0;margin:18px 0;">
              {tableRows}
            </table>
            """;
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value?.Trim() ?? string.Empty);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd");
    }
}
