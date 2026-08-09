
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ExamslotServices : IExamSlotServices
{
    private readonly IExamslotRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly AppDbContext _context;

    public ExamslotServices(
        IExamslotRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
    }

    public async Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllExamSlotsAsync(string? search, string? sort, int page, int pageSize)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        if (role is not AppRole.SuperAdmin and not AppRole.SchoolAdmin and not AppRole.Lecturer and not AppRole.Student)
            throw new UnauthorizedAccessException("Access denied.");

        var institutionId = role == AppRole.SuperAdmin
            ? (Guid?)null
            : user.InstitutionId ?? throw new UnauthorizedAccessException("User is not assigned to an institution.");
        var lecturerId = role == AppRole.Lecturer ? user.Id : (Guid?)null;
        var studentId = role == AppRole.Student ? user.Id : (Guid?)null;

        return await _repo.GetAllAsync(search, sort, page, pageSize, institutionId, lecturerId, studentId);
    }

    public async Task<ExamslotReponseDto?> GetByIdAsync(Guid ExamId)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return null;

        await EnsureExamSlotReadAccessAsync(entity);
        return MapToResponseDto(entity);
    }

    public async Task<ExamslotReponseDto?> GetByIdForStudentAsync(Guid ExamId, Guid studentId)
    {
        var participation = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .ThenInclude(c => c.Lecturer)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.ProctorNavigation)
            .FirstOrDefaultAsync(p =>
                p.ExamSlotId == ExamId &&
                p.StudentId == studentId);

        var entity = participation?.ExamSlot;
        if (entity == null) return null;
        if (entity.Status != ExamSlotStatus.Scheduled) return null;

        await EnsureStudentExamSlotAccessAsync(entity, studentId);
        return MapToResponseDto(entity);
    }

    public async Task<IEnumerable<ExamslotReponseDto>> GetByClassIdAsync(Guid classId)
    {
        var cls = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId && c.DeletedAt == null)
            ?? throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SchoolAdmin)
        {
            await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);
        }
        else if (user.Role == AppRole.Student)
        {
            var enrolled = await _context.ClassEnrollments.AsNoTracking().AnyAsync(e =>
                e.ClassId == classId &&
                e.StudentId == user.Id &&
                e.Status == EnrollmentStatus.Active);
            if (!enrolled)
                throw new UnauthorizedAccessException("Access denied.");
        }
        else if (user.Role == AppRole.Lecturer)   // ← THÊM MỚI: Lecturer chỉ thấy exam slot của lớp mình dạy
        {
            if (cls.LecturerId != user.Id)
                throw new UnauthorizedAccessException("You can only view exam slots for your own classes.");
        }
        else if (user.Role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var items = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .ThenInclude(c => c.Lecturer)
            .Include(e => e.ProctorNavigation)
            .Where(e => e.ClassId == classId)
            .OrderBy(e => e.StartTime)
            .ToListAsync();

        return items.Select(e => MapToResponseDto(e));
    }

    public async Task<IEnumerable<ExamslotReponseDto>> GetByStudentIdAsync(Guid studentId)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.SchoolAdmin && user.Role != AppRole.SuperAdmin)
            throw new UnauthorizedAccessException("Access denied.");

        var query = _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .ThenInclude(c => c.Lecturer)
            .Include(e => e.ProctorNavigation)
            .Where(e =>
                e.Class.DeletedAt == null &&
                e.ExamParticipations.Any(p => p.StudentId == studentId));

        if (user.Role == AppRole.SchoolAdmin)
        {
            var institutionId = user.InstitutionId
                ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.");
            query = query.Where(e => e.Class.InstitutionId == institutionId);
        }

        var items = await query
            .OrderBy(e => e.StartTime)
            .ToListAsync();

        return items.Select(e => MapToResponseDto(e));
    }

    public async Task<IEnumerable<ExamslotReponseDto>> GetMyExamHistoryAsync()
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.Student)
            throw new UnauthorizedAccessException("Access denied.");

        var items = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .ThenInclude(c => c.Lecturer)
            .Include(e => e.ProctorNavigation)
            .Where(e =>
                e.Class.DeletedAt == null &&
                e.ExamParticipations.Any(p => p.StudentId == user.Id))
            .OrderByDescending(e => e.StartTime)
            .ToListAsync();

        return items.Select(e => MapToResponseDto(e));
    }

    public async Task<ExamslotReponseDto> CreateAsync(CreateExamSlotDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();
        if (string.IsNullOrWhiteSpace(dto.ExamName))
            throw new InvalidOperationException("Exam name is required.");
        if (dto.ExpectedDurationMinutes <= 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");
        if (!Enum.IsDefined(dto.Status) || dto.Status != ExamSlotStatus.Scheduled)
            throw new InvalidOperationException("New exam slots must start as SCHEDULED.");

        ValidateNewExamTimes(dto.StartTime, dto.EndTime);
        ValidateExpectedDuration(dto.ExpectedDurationMinutes, dto.StartTime, dto.EndTime);

        var cls = await _context.Classes
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.Id == dto.ClassId && c.DeletedAt == null)
            ?? throw new InvalidOperationException("Class not found.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);
        await EnsureClassHasNoOverlappingExamAsync(dto.ClassId, dto.StartTime, dto.EndTime);
        if (dto.LecturerId is Guid lecturerId && lecturerId != Guid.Empty && lecturerId != cls.LecturerId)
        {
            cls.Lecturer = await GetClassLecturerAsync(lecturerId, cls.InstitutionId);
            cls.LecturerId = lecturerId;
            cls.UpdatedBy = user.Id;
            cls.UpdatedAt = DateTime.UtcNow;
        }

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        User? proctor = null;
        if (dto.ProctorId is Guid proctorId && proctorId != Guid.Empty)
            proctor = await GetClassLecturerAsync(proctorId, cls.InstitutionId);

        var entity = new ExamSlot
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            CreatedBy = user.Id,
            ExamName = dto.ExamName.Trim(),
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            ExpectedDurationMinutes = dto.ExpectedDurationMinutes,
            Status = dto.Status,
            ProctorId = proctor?.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await _notifications.SendToClassStudentsAsync(
            entity.ClassId,
            "Bạn có lịch thi mới",
            $"Lịch thi {entity.ExamName} bắt đầu lúc {entity.StartTime:yyyy-MM-dd HH:mm}.",
            NotificationType.ExamReminder,
            ReferenceTypeEnum.ExamSlot,
            entity.Id);
        await PublishExamSlotChangedAsync(entity, "created");
        return MapToResponseDto(entity, cls.Lecturer, proctor);
    }

    public async Task<ExamslotReponseDto?> UpdateAsync(Guid ExamId, UpdateExamSlotDto dto)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return null;

        await EnsureExamSlotAdminAccessAsync(entity);

        if (!string.IsNullOrWhiteSpace(dto.ExamName))
            entity.ExamName = dto.ExamName.Trim();

        if (dto.ExpectedDurationMinutes < 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");
        entity.ExpectedDurationMinutes = dto.ExpectedDurationMinutes != 0
            ? dto.ExpectedDurationMinutes
            : entity.ExpectedDurationMinutes;

        var startTime = dto.StartTime ?? entity.StartTime;
        var endTime = dto.EndTime ?? entity.EndTime;
        if (dto.StartTime.HasValue || dto.EndTime.HasValue)
        {
            ValidateUpdatedExamTimes(startTime, endTime, dto.StartTime.HasValue, dto.EndTime.HasValue);
            await EnsureClassHasNoOverlappingExamAsync(entity.ClassId, startTime, endTime, entity.Id);
            await EnsureParticipantsHaveNoOverlappingExamAsync(entity.Id, startTime, endTime);
            entity.StartTime = startTime;
            entity.EndTime = endTime;
        }
        ValidateExpectedDuration(entity.ExpectedDurationMinutes, startTime, endTime);

        if (dto.LecturerId is Guid lecturerId && lecturerId != Guid.Empty && lecturerId != entity.Class.LecturerId)
        {
            entity.Class.Lecturer = await GetClassLecturerAsync(lecturerId, entity.Class.InstitutionId);
            entity.Class.LecturerId = lecturerId;
            entity.Class.UpdatedBy = _currentUser.UserId;
            entity.Class.UpdatedAt = DateTime.UtcNow;
        }

        if (dto.ProctorId is Guid proctorId && proctorId != Guid.Empty && proctorId != entity.ProctorId)
        {
            entity.ProctorNavigation = await GetClassLecturerAsync(proctorId, entity.Class.InstitutionId);
            entity.ProctorId = proctorId;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await PublishExamSlotChangedAsync(entity, "updated");
        return MapToResponseDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid ExamId)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return false;

        await EnsureExamSlotAdminAccessAsync(entity);
        entity.Status = ExamSlotStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity);
        await PublishExamSlotChangedAsync(entity, "updated");
        return true;
    }

    private async Task EnsureExamSlotAdminAccessAsync(ExamSlot entity)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);

        var institutionId = await _context.Classes
            .AsNoTracking()
            .Where(c => c.Id == entity.ClassId && c.DeletedAt == null)
            .Select(c => (Guid?)c.InstitutionId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Class not found.");

        await _currentUser.EnsureInstitutionAccessAsync(institutionId);
    }

    private async Task EnsureExamSlotReadAccessAsync(ExamSlot entity)
    {
        if (entity.Class.DeletedAt != null)
            throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin) return;
        if (user.InstitutionId != entity.Class.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");
        if (role == AppRole.SchoolAdmin) return;
        if (role == AppRole.Lecturer && entity.Class.LecturerId == user.Id) return;
        if (role == AppRole.Student && await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
                p.ExamSlotId == entity.Id && p.StudentId == user.Id))
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureStudentExamSlotAccessAsync(ExamSlot entity, Guid studentId)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (entity.Class.DeletedAt != null)
            throw new InvalidOperationException("Class not found.");

        if (user.Role == AppRole.SchoolAdmin)
        {
            await _currentUser.EnsureInstitutionAccessAsync(entity.Class.InstitutionId);
            return;
        }

        if (user.Role == AppRole.Student && studentId == user.Id)
        {
            return;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task PublishExamSlotChangedAsync(ExamSlot entity, string action)
    {
        var cls = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == entity.ClassId && c.DeletedAt == null);

        await _realtime.PublishDataChangedAsync(
            "exam-slots",
            action,
            institutionId: cls?.InstitutionId,
            lecturerId: cls?.LecturerId,
            data: new
            {
                examSlotId = entity.Id,
                entity.ClassId,
                entity.ExamName,
                entity.StartTime,
                entity.EndTime,
                entity.Status
            });
    }

    private async Task<User> GetClassLecturerAsync(Guid lecturerId, Guid institutionId)
    {
        var lecturer = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == lecturerId &&
                u.Role == AppRole.Lecturer &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active)
            ?? throw new InvalidOperationException("Lecturer not found.");

        if (lecturer.InstitutionId != institutionId)
            throw new UnauthorizedAccessException("Lecturer does not belong to this institution.");

        return lecturer;
    }

    private async Task EnsureClassHasNoOverlappingExamAsync(
        Guid classId,
        DateTime startTime,
        DateTime endTime,
        Guid? ignoredExamSlotId = null)
    {
        var hasOverlap = await _context.ExamSlots.AsNoTracking().AnyAsync(e =>
            e.ClassId == classId &&
            e.Status != ExamSlotStatus.Cancelled &&
            (!ignoredExamSlotId.HasValue || e.Id != ignoredExamSlotId.Value) &&
            e.StartTime < endTime &&
            e.EndTime > startTime);

        if (hasOverlap)
            throw new InvalidOperationException("Class already has an exam in this time range.");
    }

    private async Task EnsureParticipantsHaveNoOverlappingExamAsync(Guid examSlotId, DateTime startTime, DateTime endTime)
    {
        var hasOverlap = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
            p.ExamSlotId == examSlotId &&
            _context.ExamParticipations.Any(other =>
                other.StudentId == p.StudentId &&
                other.ExamSlotId != examSlotId &&
                other.ExamSlot.Status != ExamSlotStatus.Cancelled &&
                other.ExamSlot.StartTime < endTime &&
                other.ExamSlot.EndTime > startTime));

        if (hasOverlap)
            throw new InvalidOperationException("One or more students already have another exam in this time range.");
    }

    private static void ValidateNewExamTimes(DateTime startTime, DateTime endTime)
    {
        var now = DateTime.UtcNow;
        if (startTime == default)
            throw new InvalidOperationException("Exam start time is required.");
        if (endTime == default)
            throw new InvalidOperationException("Exam end time is required.");
        if (startTime < now)
            throw new InvalidOperationException("Exam start time cannot be in the past.");
        if (endTime < now)
            throw new InvalidOperationException("Exam end time cannot be in the past.");
        if (endTime <= startTime)
            throw new InvalidOperationException("Exam end time must be after start time.");
    }

    private static void ValidateUpdatedExamTimes(DateTime startTime, DateTime endTime, bool startChanged, bool endChanged)
    {
        var now = DateTime.UtcNow;
        if (startChanged && startTime < now)
            throw new InvalidOperationException("Exam start time cannot be in the past.");
        if (endChanged && endTime < now)
            throw new InvalidOperationException("Exam end time cannot be in the past.");
        if (endTime <= startTime)
            throw new InvalidOperationException("Exam end time must be after start time.");
    }

    private static void ValidateExpectedDuration(int expectedDurationMinutes, DateTime startTime, DateTime endTime)
    {
        if (expectedDurationMinutes <= 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");
        if (expectedDurationMinutes > (endTime - startTime).TotalMinutes)
            throw new InvalidOperationException("Expected duration cannot exceed the exam slot time range.");
    }

    private static ExamslotReponseDto MapToResponseDto(ExamSlot entity, User? lecturer = null, User? proctor = null) => new()
    {
        Id = entity.Id,
        ClassId = entity.ClassId,
        ExamName = entity.ExamName,
        StartTime = entity.StartTime,
        EndTime = entity.EndTime,
        ExpectedDurationMinutes = entity.ExpectedDurationMinutes,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Lecturer = ToUserSummary(lecturer ?? entity.Lecturer),
        Proctor = ToUserSummary(proctor ?? entity.ProctorNavigation)
    };

    private static UserSummaryDto? ToUserSummary(User? user) =>
        user == null ? null : new UserSummaryDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            StudentCode = user.StudentCode
        };

}


