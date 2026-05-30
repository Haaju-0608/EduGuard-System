using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IAttendanceSessionRepository
{
    Task<(IEnumerable<AttendanceSession> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? classId = null, SessionStatus? status = null);
    Task<AttendanceSession?> GetByIdAsync(Guid id);
    Task AddAsync(AttendanceSession entity);
    Task UpdateAsync(AttendanceSession entity);
    Task SoftDeleteAsync(AttendanceSession entity);
}
