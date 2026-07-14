using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class AttendanceRecordRepository : IAttendanceRecordRepository
{
    private readonly AppDbContext _context;

    public AttendanceRecordRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<AttendanceRecord> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? sessionId = null, Guid? studentId = null, AttendanceStatus? status = null)
    {
        //var query = _context.AttendanceRecords
        //    .AsNoTracking()
        //    .Where(r => !(r.Status == AttendanceStatus.Absent && r.AdjustedAt != null && r.CheckinAt == null));

        // SỬA: Loại bỏ câu lệnh loại trừ Absent nguy hiểm để danh sách hiển thị đầy đủ học sinh vắng/hiện diện
        var query = _context.AttendanceRecords.AsNoTracking().AsQueryable();

        if (sessionId.HasValue)
            query = query.Where(r => r.SessionId == sessionId.Value);
        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                _context.Users.Any(u => u.Id == r.StudentId && u.FullName.ToLower().Contains(search.ToLower())));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<AttendanceRecord?> GetByIdAsync(Guid id) =>
        _context.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == id);

    public Task<AttendanceRecord?> GetBySessionAndStudentAsync(Guid sessionId, Guid studentId) =>
        _context.AttendanceRecords.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.StudentId == studentId);

    public async Task AddAsync(AttendanceRecord entity)
    {
        await _context.AttendanceRecords.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<AttendanceRecord> entities)
    {
        await _context.AttendanceRecords.AddRangeAsync(entities);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AttendanceRecord entity)
    {
        _context.AttendanceRecords.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(AttendanceRecord entity, Guid adjustedBy)
    {
        entity.Status = AttendanceStatus.Absent;
        entity.CheckinAt = null;
        entity.AdjustedBy = adjustedBy;
        entity.AdjustedAt = DateTime.UtcNow;
        _context.AttendanceRecords.Update(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<AttendanceRecord> ApplySort(IQueryable<AttendanceRecord> query, string? sort) =>
        (sort ?? "-checkinAt").ToLower() switch
        {
            "checkinat" => query.OrderBy(r => r.CheckinAt),
            "-checkinat" => query.OrderByDescending(r => r.CheckinAt),
            "status" => query.OrderBy(r => r.Status),
            "-status" => query.OrderByDescending(r => r.Status),
            _ => query.OrderByDescending(r => r.CheckinAt)
        };
}
