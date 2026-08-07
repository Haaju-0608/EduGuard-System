using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ViolationLogServices : IViolationLogService
{
    private readonly IViolationLogRepository _repo;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly INotificationDispatcher _notifications;
    private readonly IStorageService _storage;

    public ViolationLogServices(
        IViolationLogRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        INotificationDispatcher notifications,
        IStorageService storage)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _notifications = notifications;
        _storage = storage;
    }

    public async Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? participationId = null, bool? isReviewed = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;
        var studentId = user.Role == AppRole.Student ? user.Id : (Guid?)null;

        return await _repo.GetAllAsync(
            search, sort, page, pageSize, participationId, isReviewed, institutionId, lecturerId, studentId);
    }

    public async Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByExamSlotAsync(
        Guid examSlotId, string? search, string? sort, int page, int pageSize)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var examSlot = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == examSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId != examSlot.Class.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer &&
            (user.InstitutionId != examSlot.Class.InstitutionId || user.Id != examSlot.Class.LecturerId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (user.Role != AppRole.SuperAdmin && user.Role != AppRole.SchoolAdmin && user.Role != AppRole.Lecturer)
            throw new UnauthorizedAccessException("Access denied.");

        return await _repo.GetAllAsync(
            search, sort, page, pageSize,
            examSlotId: examSlotId);
    }

    public async Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByExamSlotAndStudentAsync(
        Guid examSlotId, Guid studentId, string? search, string? sort, int page, int pageSize)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var examSlot = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == examSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");

        if (user.Role == AppRole.Student)
        {
            if (studentId != user.Id)
                throw new UnauthorizedAccessException("Students can only view their own violation logs.");
        }
        else if (user.Role == AppRole.SchoolAdmin)
        {
            if (user.InstitutionId != examSlot.Class.InstitutionId)
                throw new UnauthorizedAccessException("Access denied.");
        }
        else if (user.Role == AppRole.Lecturer)
        {
            if (user.InstitutionId != examSlot.Class.InstitutionId || user.Id != examSlot.Class.LecturerId)
                throw new UnauthorizedAccessException("Access denied.");
        }
        else if (user.Role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return await _repo.GetAllAsync(
            search, sort, page, pageSize,
            studentId: studentId,
            examSlotId: examSlotId);
    }

    public async Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByStudentIdAsync(
        Guid studentId, string? search, string? sort, int page, int pageSize)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && studentId != user.Id)
            throw new UnauthorizedAccessException("Students can only view their own violation logs.");

        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;

        return await _repo.GetAllAsync(
            search, sort, page, pageSize,
            institutionId: institutionId,
            lecturerId: lecturerId,
            studentId: studentId);
    }

    public async Task<ViolationlogResponeDto?> GetByIdAsync(Guid id)
    {
        var violation = await GetViolationWithAccessDataAsync(id);
        if (violation == null)
            return null;

        await EnsureViolationAccessAsync(violation.Participation);
        return MapToResponseDto(violation);
    }

    public async Task<ViolationlogResponeDto> CreateAsync(CreateViolationLogDto dto)
    {
        if (dto.ParticipationId == Guid.Empty)
            throw new InvalidOperationException("Participation id is required.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (!user.Role.IsInRoles(AppRole.Student, AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin))
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");

        var participation = await _context.ExamParticipations
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == dto.ParticipationId)
            ?? throw new InvalidOperationException("Exam participation not found.");

        await EnsureViolationAccessAsync(participation);
        var recordedAt = ValidateCreateInput(dto, participation, user);

        var entity = new ViolationLog
        {
            Id = Guid.NewGuid(),
            severity = dto.severity,
            violationType = dto.violationType,
            ParticipationId = dto.ParticipationId,
            EvidencePath = null,
            AiConfidence = dto.AiConfidence,
            IsReviewed = false,
            ReviewedBy = null,
            RecordedAt = recordedAt
        };

        await _repo.CreateAsync(entity);
        var payload = new
        {
            violationId = entity.Id,
            entity.ParticipationId,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            type = entity.violationType,
            severity = entity.severity,
            confidence = entity.AiConfidence,
            entity.EvidencePath,
            entity.RecordedAt
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.ViolationDetected, payload);
        await _realtime.PublishDataChangedAsync(
            "violations",
            "created",
            institutionId: participation.ExamSlot.Class.InstitutionId,
            lecturerId: participation.ExamSlot.Class.LecturerId,
            userId: participation.StudentId,
            data: payload);
        await _notifications.SendToUserAsync(
            participation.ExamSlot.Class.LecturerId,
            "Phát hiện vi phạm trong kỳ thi",
            $"Sinh viên {participation.Student.FullName}. {DescribeViolation(entity.violationType)}.",
            NotificationType.ViolationDetected,
            ReferenceTypeEnum.ExamSlot,
            participation.ExamSlotId);
        return MapToResponseDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateViolationLogDto dto)
    {
        var existing = await GetViolationWithAccessDataAsync(id);
        if (existing == null) return false;
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureViolationAccessAsync(existing.Participation);
        var user = await _currentUser.GetRequiredUserAsync();
        var requestedReviewer = dto.ReviewedBy;
        if (!dto.IsReviewed && requestedReviewer.HasValue && requestedReviewer.Value != Guid.Empty)
            throw new InvalidOperationException("Reviewed by must be empty when the violation is not reviewed.");
        if (dto.IsReviewed && requestedReviewer.HasValue &&
            requestedReviewer.Value != Guid.Empty && requestedReviewer.Value != user.Id)
        {
            throw new UnauthorizedAccessException("Reviewed by must be the current user.");
        }

        dto.ReviewedBy = dto.IsReviewed ? user.Id : null;

        await _repo.UpdateAsync(id, dto);
        await PublishViolationChangedAsync(id, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await GetViolationWithAccessDataAsync(id);
        if (existing == null) return false;
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureViolationAccessAsync(existing.Participation);

        await PublishViolationChangedAsync(id, "deleted");
        if (!string.IsNullOrWhiteSpace(existing.EvidencePath))
            await _storage.DeleteAsync(StorageService.ExamEvidenceBucket, existing.EvidencePath);
        await _repo.DeleteAsync(id);
        return true;
    }

    private async Task PublishViolationChangedAsync(Guid violationId, string action)
    {
        var violation = await _context.ViolationLogs
            .AsNoTracking()
            .Include(v => v.Participation)
            .ThenInclude(p => p.Student)
            .Include(v => v.Participation)
            .ThenInclude(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(v => v.Id == violationId);

        if (violation == null)
            return;

        await _realtime.PublishDataChangedAsync(
            "violations",
            action,
            institutionId: violation.Participation.ExamSlot.Class.InstitutionId,
            lecturerId: violation.Participation.ExamSlot.Class.LecturerId,
            userId: violation.Participation.StudentId,
            data: new
            {
                violationId = violation.Id,
                violation.ParticipationId,
                violation.Participation.ExamSlotId,
                examName = violation.Participation.ExamSlot.ExamName,
                violation.Participation.StudentId,
                violation.Participation.Student.FullName,
                type = violation.violationType,
                severity = violation.severity,
                violation.IsReviewed,
                violation.ReviewedBy,
                violation.RecordedAt
            });
    }

    private Task<ViolationLog?> GetViolationWithAccessDataAsync(Guid violationId) =>
        _context.ViolationLogs
            .Include(v => v.Participation)
            .ThenInclude(p => p.Student)
            .Include(v => v.Participation)
            .ThenInclude(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(v => v.Id == violationId);

    private async Task EnsureViolationAccessAsync(ExamParticipation participation)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var cls = participation.ExamSlot.Class;
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId == cls.InstitutionId)
            return;

        if (user.Role == AppRole.Student && user.Id == participation.StudentId)
            return;

        if (user.Role == AppRole.Lecturer &&
            user.InstitutionId == cls.InstitutionId &&
            user.Id == cls.LecturerId)
        {
            return;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private static DateTime ValidateCreateInput(
        CreateViolationLogDto dto,
        ExamParticipation participation,
        User user)
    {
        if (!Enum.IsDefined(dto.severity))
            throw new InvalidOperationException("Invalid violation severity.");
        if (!Enum.IsDefined(dto.violationType))
            throw new InvalidOperationException("Invalid violation type.");
        if (IsBrowserViolation(dto.violationType))
            throw new InvalidOperationException("Browser violations must be reported through the browser violation endpoint.");
        if (dto.AiConfidence.HasValue &&
            (!double.IsFinite(dto.AiConfidence.Value) || dto.AiConfidence.Value is < 0 or > 1))
        {
            throw new InvalidOperationException("AI confidence must be between 0 and 1.");
        }
        if (!string.IsNullOrWhiteSpace(dto.EvidencePath))
            throw new InvalidOperationException("Evidence must be uploaded through the violation evidence endpoint.");
        if (dto.ReviewedBy.HasValue && dto.ReviewedBy.Value != Guid.Empty)
            throw new InvalidOperationException("A new violation cannot already have a reviewer.");
        if (user.Role == AppRole.Student && participation.Status != ParticipationStatus.Joined)
            throw new InvalidOperationException("Students can only report violations while participating in the exam.");
        if (participation.ExamSlot.Status == ExamSlotStatus.Cancelled)
            throw new InvalidOperationException("Cannot record a violation for a cancelled exam slot.");

        var recordedAt = dto.RecordedAt == default ? DateTime.UtcNow : dto.RecordedAt;
        if (recordedAt > DateTime.UtcNow)
            throw new InvalidOperationException("Recorded time cannot be in the future.");
        if (recordedAt < participation.ExamSlot.StartTime || recordedAt > participation.ExamSlot.EndTime)
            throw new InvalidOperationException("Recorded time must be within the exam slot time range.");
        if (!participation.ActualStart.HasValue)
            throw new InvalidOperationException("Cannot record a violation before the participation starts.");
        if (recordedAt < participation.ActualStart.Value)
            throw new InvalidOperationException("Recorded time cannot be before the participation starts.");
        if (participation.ActualEnd.HasValue && recordedAt > participation.ActualEnd.Value)
            throw new InvalidOperationException("Recorded time cannot be after the participation ends.");

        return recordedAt;
    }

    private static bool IsBrowserViolation(ViolationType type) =>
        type is ViolationType.TabSwitch or ViolationType.WindowBlur or ViolationType.ExitFullscreen;

    private static string DescribeViolation(ViolationType type) => type switch
    {
        ViolationType.GazeDiversion => "Phát hiện quay đầu nhiều lần",
        ViolationType.MultipleFaces => "Phát hiện nhiều khuôn mặt",
        ViolationType.Absence => "Không phát hiện khuôn mặt",
        ViolationType.Impersonation => "Nghi ngờ mạo danh",
        _ => $"Phát hiện vi phạm {type}"
    };

    private static ViolationlogResponeDto MapToResponseDto(ViolationLog entity) => new()
    {
        Id = entity.Id,
        ParticipationId = entity.ParticipationId,
        EvidencePath = entity.EvidencePath,
        Severity = entity.severity,
        ViolationType = entity.violationType,
        AiConfidence = entity.AiConfidence,
        IsReviewed = entity.IsReviewed,
        ReviewedBy = entity.ReviewedBy,
        RecordedAt = entity.RecordedAt
    };
}
