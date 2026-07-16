using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class AttendanceSessionRepository : IAttendanceSessionRepository
{
    private readonly AppDbContext _context;

    public AttendanceSessionRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<AttendanceSession> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? classId = null, Guid? institutionId = null, Guid? lecturerId = null,
        SessionStatus? status = null)
    {
        var query = _context.AttendanceSessions
            .AsNoTracking()
            .Where(s =>
                s.Status != SessionStatus.Cancelled &&
                _context.Classes.Any(c => c.Id == s.ClassId && c.DeletedAt == null));

        if (classId.HasValue)
            query = query.Where(s => s.ClassId == classId.Value);
        if (institutionId.HasValue)
            query = query.Where(s => _context.Classes.Any(c => c.Id == s.ClassId && c.InstitutionId == institutionId.Value));
        if (lecturerId.HasValue)
            query = query.Where(s => _context.Classes.Any(c => c.Id == s.ClassId && c.LecturerId == lecturerId.Value));
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s =>
                _context.Classes.Any(c => c.Id == s.ClassId &&
                    c.CourseName.ToLower().Contains(search.ToLower())));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    //public Task<AttendanceSession?> GetByIdAsync(Guid id) =>
    //    _context.AttendanceSessions.FirstOrDefaultAsync(s => s.Id == id && s.Status != SessionStatus.Cancelled);

    //SỬA: Cho phép lấy thông tin phiên bằng Id kể cả khi nó đã bị hủy (phục vụ log/đối soát)
    public Task<AttendanceSession?> GetByIdAsync(Guid id) =>
        _context.AttendanceSessions.FirstOrDefaultAsync(s =>
            s.Id == id &&
            s.Status != SessionStatus.Cancelled &&
            _context.Classes.Any(c => c.Id == s.ClassId && c.DeletedAt == null));

    public async Task AddAsync(AttendanceSession entity)
    {
        await _context.AttendanceSessions.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AttendanceSession entity)
    {
        _context.AttendanceSessions.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(AttendanceSession entity)
    {
        entity.Status = SessionStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.AttendanceSessions.Update(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<AttendanceSession> ApplySort(IQueryable<AttendanceSession> query, string? sort) =>
        (sort ?? "-startTime").ToLower() switch
        {
            "starttime" => query.OrderBy(s => s.StartTime),
            "-starttime" => query.OrderByDescending(s => s.StartTime),
            "status" => query.OrderBy(s => s.Status),
            "-status" => query.OrderByDescending(s => s.Status),
            "createdat" => query.OrderBy(s => s.CreatedAt),
            "-createdat" => query.OrderByDescending(s => s.CreatedAt),
            _ => query.OrderByDescending(s => s.StartTime)
        };
}
