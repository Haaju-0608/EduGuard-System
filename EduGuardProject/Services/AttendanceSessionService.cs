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
        if (role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot list attendance sessions.");

        Guid? institutionId = role == AppRole.SuperAdmin ? null : user.InstitutionId;
        Guid? lecturerId = role == AppRole.Lecturer ? user.Id : null;

        if (role == AppRole.Lecturer)
        {
            var hasClasses = await _context.Classes.AsNoTracking()
                .AnyAsync(c => c.LecturerId == user.Id && c.DeletedAt == null);
            if (!hasClasses)
                return ([], 0);
        }

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, classId, institutionId, lecturerId);
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
        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("You can only open sessions for your own classes.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        var entity = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            CreatedBy = user.Id,
            VideoPath = dto.VideoPath,
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

        var oldStatus = entity.Status;
        if (dto.EndTime.HasValue) entity.EndTime = ToUtcTimestamp(dto.EndTime.Value);
        entity.Status = dto.Status;
        if (dto.VideoPath != null) entity.VideoPath = dto.VideoPath;
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
            throw new UnauthorizedAccessException("Access denied.");
    }
}
