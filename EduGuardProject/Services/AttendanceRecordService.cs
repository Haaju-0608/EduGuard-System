using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class AttendanceRecordService : IAttendanceRecordService
{
    private readonly IAttendanceRecordRepository _repo;
    private readonly IAttendanceSessionRepository _sessionRepo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public AttendanceRecordService(
        IAttendanceRecordRepository repo,
        IAttendanceSessionRepository sessionRepo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _sessionRepo = sessionRepo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<AttendanceRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? sessionId = null, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            studentId = user.Id;

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, sessionId, studentId);
        var dtos = new List<AttendanceRecordResponseDto>();
        foreach (var item in items)
        {
            await EnsureRecordAccessAsync(item);
            dtos.Add(await AcademicMapper.MapRecordAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<AttendanceRecordResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureRecordAccessAsync(entity);
        return await AcademicMapper.MapRecordAsync(_context, entity, expand);
    }

    public async Task<AttendanceRecordResponseDto> CreateAsync(CreateAttendanceRecordDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(dto.SessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");

        await EnsureSessionAccessAsync(session);

        var existing = await _repo.GetBySessionAndStudentAsync(dto.SessionId, dto.StudentId);
        if (existing != null)
            throw new InvalidOperationException("Attendance record already exists for this student in this session.");

        await EnsureStudentEnrolledAsync(session.ClassId, dto.StudentId);

        var user = await _currentUser.GetRequiredUserAsync();
        var entity = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            SessionId = dto.SessionId,
            StudentId = dto.StudentId,
            Status = dto.Status,
            Method = dto.Method,
            ConfidenceScore = dto.ConfidenceScore,
            SnapshotPath = dto.SnapshotPath,
            CheckinAt = dto.CheckinAt ?? DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await UpdateSessionRecognizedCountAsync(dto.SessionId);
        return await AcademicMapper.MapRecordAsync(_context, entity, null);
    }

    public async Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkManualAsync(
        Guid sessionId, BulkManualAttendanceDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");
        await EnsureSessionAccessAsync(session);

        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Can only mark attendance for in-progress sessions.");

        var now = DateTime.UtcNow;
        var records = new List<AttendanceRecord>();
        var results = new List<AttendanceRecordResponseDto>();

        foreach (var studentId in dto.PresentStudentIds.Distinct())
        {
            await EnsureStudentEnrolledAsync(session.ClassId, studentId);

            var existing = await _repo.GetBySessionAndStudentAsync(sessionId, studentId);
            if (existing != null)
            {
                existing.Status = dto.Status;
                existing.Method = dto.Method;
                existing.CheckinAt = now;
                await _repo.UpdateAsync(existing);
                results.Add(await AcademicMapper.MapRecordAsync(_context, existing, null));
                continue;
            }

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = studentId,
                Status = dto.Status,
                Method = dto.Method,
                CheckinAt = now
            };
            records.Add(record);
        }

        if (records.Count > 0)
            await _repo.AddRangeAsync(records);

        foreach (var record in records)
            results.Add(await AcademicMapper.MapRecordAsync(_context, record, null));

        await UpdateSessionRecognizedCountAsync(sessionId);
        return results;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAttendanceRecordDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);

        var user = await _currentUser.GetRequiredUserAsync();
        entity.Status = dto.Status;
        entity.Method = dto.Method;
        entity.ConfidenceScore = dto.ConfidenceScore;
        entity.SnapshotPath = dto.SnapshotPath;
        entity.CheckinAt = dto.CheckinAt;
        entity.AdjustedBy = user.Id;
        entity.AdjustedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await UpdateSessionRecognizedCountAsync(entity.SessionId);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);

        var user = await _currentUser.GetRequiredUserAsync();
        await _repo.SoftDeleteAsync(entity, user.Id);
        await UpdateSessionRecognizedCountAsync(entity.SessionId);
        return true;
    }

    private async Task EnsureStudentEnrolledAsync(Guid classId, Guid studentId)
    {
        var enrolled = await _context.ClassEnrollments.AsNoTracking()
            .AnyAsync(e => e.ClassId == classId && e.StudentId == studentId && e.Status == EnrollmentStatus.Active);
        if (!enrolled)
            throw new InvalidOperationException("Student is not actively enrolled in this class.");
    }

    private async Task UpdateSessionRecognizedCountAsync(Guid sessionId)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId);
        if (session == null) return;

        var count = await _context.AttendanceRecords.CountAsync(r =>
            r.SessionId == sessionId &&
            r.Status == AttendanceStatus.Present &&
            !(r.Status == AttendanceStatus.Absent && r.AdjustedAt != null && r.CheckinAt == null));

        session.TotalRecognized = count;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session);
    }

    private async Task EnsureRecordAccessAsync(AttendanceRecord entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
        {
            if (entity.StudentId != user.Id)
                throw new UnauthorizedAccessException("Access denied.");
            return;
        }

        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);
    }

    private async Task EnsureSessionAccessAsync(AttendanceSession session)
    {
        var cls = await _classRepo.GetByIdAsync(session.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
