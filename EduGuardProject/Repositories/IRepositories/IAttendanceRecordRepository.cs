using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IAttendanceRecordRepository
{
    Task<(IEnumerable<AttendanceRecord> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? sessionId = null, Guid? studentId = null, AttendanceStatus? status = null);
    Task<AttendanceRecord?> GetByIdAsync(Guid id);
    Task<AttendanceRecord?> GetBySessionAndStudentAsync(Guid sessionId, Guid studentId);
    Task AddAsync(AttendanceRecord entity);
    Task AddRangeAsync(IEnumerable<AttendanceRecord> entities);
    Task UpdateAsync(AttendanceRecord entity);
    Task SoftDeleteAsync(AttendanceRecord entity, Guid adjustedBy);
}
