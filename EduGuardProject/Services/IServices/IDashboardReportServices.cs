namespace EduGuardProject.Services.IServices;

public interface IDashboardStatsService
{
    Task<object> GetSystemDashboardAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<object> GetInstitutionDashboardAsync(Guid institutionId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<object> GetLecturerDashboardAsync(Guid? lecturerId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<object> GetAttendanceReportAsync(Guid? institutionId, Guid? classId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<object> GetViolationReportAsync(Guid? institutionId, Guid? examSlotId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<object> GetWalletReportAsync(Guid? institutionId, Guid? walletId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<object> GetRevenueReportAsync(DateTime? from, DateTime? to, string groupBy = "day", CancellationToken cancellationToken = default);
}
