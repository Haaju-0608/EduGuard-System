namespace EduGuardProject.Services;

public interface IReportExportService
{
    ReportExportFile Export(string reportType, string format, object report);
}
