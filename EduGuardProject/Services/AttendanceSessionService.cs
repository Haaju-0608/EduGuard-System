using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class AttendanceSessionService : IAttendanceSessionService
{
    private readonly IAttendanceSessionRepository _repo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;
    private readonly AppDbContext _context;

    public AttendanceSessionService(
        IAttendanceSessionRepository repo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        AppDbContext context)
    {
        _repo = repo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
        _storage = storage;
        _context = context;
    }

    public async Task<(IEnumerable<AttendanceSessionResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? classId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();

        Guid? institutionId = role == AppRole.SuperAdmin ? null : user.InstitutionId;
        Guid? lecturerId = role == AppRole.Lecturer ? user.Id : null;
        Guid? studentId = role == AppRole.Student ? user.Id : null;

        if (role == AppRole.Lecturer)
        {
            var hasClasses = await _context.Classes.AsNoTracking()
                .AnyAsync(c => c.LecturerId == user.Id && c.DeletedAt == null);
            if (!hasClasses)
                return ([], 0);
        }

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, classId, institutionId, lecturerId, studentId: studentId);
        var dtos = new List<AttendanceSessionResponseDto>();
        foreach (var item in items)
        {
            await EnsureSessionAccessAsync(item);
            dtos.Add(await AcademicMapper.MapSessionAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<AttendanceSessionResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureSessionAccessAsync(entity);
        return await AcademicMapper.MapSessionAsync(_context, entity, expand);
    }

    public async Task<AttendanceSessionResponseDto> CreateAsync(CreateAttendanceSessionDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();

        var cls = await _classRepo.GetByIdAsync(dto.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        // Validate examSlotId (if provided) before using it for the proctor check below.
        ExamSlot? examSlot = null;
        if (dto.ExamSlotId.HasValue)
        {
            examSlot = await _context.ExamSlots.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId.Value);
            if (examSlot == null)
                throw new InvalidOperationException("Exam slot not found.");
            if (examSlot.ClassId != dto.ClassId)
                throw new InvalidOperationException("Exam slot does not belong to the specified class.");
        }

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
        {
            var isProctor = examSlot?.ProctorId == user.Id;
            if (!isProctor)
                throw new UnauthorizedAccessException("You can only open sessions for your own classes or exams you are proctoring.");
        }

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        if (dto.StartTime == default)
            throw new InvalidOperationException("Start time is required.");
        if (!string.IsNullOrWhiteSpace(dto.VideoPath))
            throw new InvalidOperationException("Video must be uploaded through the AI video endpoint after the session is created.");

        var startTimeUtc = ToUtcTimestamp(dto.StartTime);
        if (examSlot != null && (startTimeUtc < examSlot.StartTime || startTimeUtc > examSlot.EndTime))
            throw new InvalidOperationException("Attendance session start time must be within the exam slot's time range.");

        var hasOpenSession = await _context.AttendanceSessions.AsNoTracking().AnyAsync(s =>
            s.ClassId == dto.ClassId && s.Status == SessionStatus.InProgress);
        if (hasOpenSession)
            throw new InvalidOperationException("This class already has an in-progress attendance session.");

        var entity = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            ExamSlotId = dto.ExamSlotId,
            CreatedBy = user.Id,
            VideoPath = null,
            StartTime = ToUtcTimestamp(dto.StartTime),
            Status = SessionStatus.InProgress,
            TotalRecognized = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        var payload = new
        {
            sessionId = entity.Id,
            entity.ClassId,
            entity.StartTime,
            entity.Status,
            openedAt = DateTime.UtcNow
        };
        await _realtime.PushClassStudentsAsync(entity.ClassId, HubEvents.AttendanceSessionOpened, payload);
        await _realtime.PushAttendanceSessionAsync(entity.Id, HubEvents.AttendanceSessionOpened, payload);
        await _notifications.SendToClassStudentsAsync(
            entity.ClassId,
            "Điểm danh đã bắt đầu",
            "Giảng viên đã mở ca điểm danh cho lớp của bạn.",
            NotificationType.AttendanceSessionStarted,
            ReferenceTypeEnum.AttendanceSession,
            entity.Id);
        await PublishSessionChangedAsync(entity, "created");
        return await AcademicMapper.MapSessionAsync(_context, entity, null);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAttendanceSessionDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureSessionAccessAsync(entity);

        if (!Enum.IsDefined(dto.Status))
            throw new InvalidOperationException("Invalid session status.");
        if (entity.Status is SessionStatus.Completed or SessionStatus.Cancelled)
            throw new InvalidOperationException("This attendance session is already closed and cannot be updated.");
        if (dto.EndTime.HasValue)
        {
            var endTime = ToUtcTimestamp(dto.EndTime.Value);
            if (endTime < entity.StartTime)
                throw new InvalidOperationException("End time cannot be before start time.");
            if (endTime > DateTime.UtcNow)
                throw new InvalidOperationException("End time cannot be in the future.");

            if (entity.ExamSlotId.HasValue)
            {
                var examSlot = await _context.ExamSlots.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == entity.ExamSlotId.Value);
                if (examSlot != null && endTime > examSlot.EndTime)
                    throw new InvalidOperationException("Attendance session end time must be within the exam slot's time range.");
            }
        }
        if (dto.Status == SessionStatus.Completed && !dto.EndTime.HasValue && !entity.EndTime.HasValue)
            throw new InvalidOperationException("End time is required to complete a session.");
        if (dto.TotalRecognized.HasValue && dto.TotalRecognized.Value < 0)
            throw new InvalidOperationException("Total recognized cannot be negative.");
        if (!string.IsNullOrWhiteSpace(dto.VideoPath))
            throw new InvalidOperationException("Video must be uploaded through the AI video endpoint.");

        var oldStatus = entity.Status;
        if (dto.EndTime.HasValue) entity.EndTime = ToUtcTimestamp(dto.EndTime.Value);
        entity.Status = dto.Status;
        if (dto.TotalRecognized.HasValue) entity.TotalRecognized = dto.TotalRecognized.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        if (oldStatus != SessionStatus.Completed && entity.Status == SessionStatus.Completed)
        {
            var totalStudents = await _context.ClassEnrollments.CountAsync(e =>
                e.ClassId == entity.ClassId && e.Status == EnrollmentStatus.Active);

            await _realtime.PushAttendanceSessionAsync(entity.Id, HubEvents.AttendanceCompleted, new
            {
                sessionId = entity.Id,
                entity.ClassId,
                entity.TotalRecognized,
                totalStudents,
                completedAt = entity.EndTime ?? DateTime.UtcNow
            });
        }
        await PublishSessionChangedAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureSessionAccessAsync(entity);
        var videoPath = entity.VideoPath;
        await _repo.SoftDeleteAsync(entity);
        if (!string.IsNullOrWhiteSpace(videoPath))
            await _storage.DeleteAsync(StorageService.AttendanceVideosBucket, videoPath);
        await PublishSessionChangedAsync(entity, "deleted");
        return true;
    }

    private async Task PublishSessionChangedAsync(AttendanceSession entity, string action)
    {
        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        await _realtime.PublishDataChangedAsync(
            "attendance-sessions",
            action,
            institutionId: cls?.InstitutionId,
            lecturerId: cls?.LecturerId,
            data: new
            {
                sessionId = entity.Id,
                entity.ClassId,
                entity.Status,
                entity.StartTime,
                entity.EndTime,
                entity.TotalRecognized
            });
    }

    private static DateTime ToUtcTimestamp(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private async Task EnsureSessionAccessAsync(AttendanceSession entity)
    {
        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        if (cls == null) return;

        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (role == AppRole.Lecturer && cls.LecturerId != user.Id)
        {
            var isProctor = entity.ExamSlotId.HasValue &&
                await _context.ExamSlots.AnyAsync(e => e.Id == entity.ExamSlotId.Value && e.ProctorId == user.Id);
            if (!isProctor)
                throw new UnauthorizedAccessException("Access denied.");
        }
    }
}
