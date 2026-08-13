namespace EduGuardProject.Services.Email;

public interface IEmailTemplateService
{
    string BuildExamReminderTemplate(string studentName, string examName, DateTime examTime);
    string BuildExamCreatedTemplate(string studentName, string examName, DateTime examTime);
    string BuildBiometricApprovedTemplate(string studentName);
    string BuildBiometricRejectedTemplate(string studentName, string reason);
    string BuildAttendanceStartedTemplate(string studentName, string className);
    string BuildSubscriptionTemplate(string institutionName, DateTime expiryDate);
}
