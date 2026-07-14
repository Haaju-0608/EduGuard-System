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

    public Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? participationId = null, bool? isReviewed = null)
    {
        return _repo.GetAllAsync(search, sort, page, pageSize, participationId, isReviewed);
    }
    public async Task<ViolationlogResponeDto?> GetByIdAsync(Guid id)
    {
        var violation = await GetViolationWithAccessDataAsync(id);
        if (violation == null)
            return null;

        await EnsureViolationAccessAsync(violation.Participation.ExamSlot.Class);
        return MapToResponseDto(violation);
    }

    public async Task<ViolationlogResponeDto> CreateAsync(CreateViolationLogDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var participation = await _context.ExamParticipations
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == dto.ParticipationId)
            ?? throw new InvalidOperationException("Exam participation not found.");

        await EnsureViolationAccessAsync(participation.ExamSlot.Class);

        var entity = new ViolationLog
        {
            Id = Guid.NewGuid(),
            severity = dto.severity,
            violationType = dto.violationType,
            ParticipationId = dto.ParticipationId,
            EvidencePath = dto.EvidencePath,
            AiConfidence = dto.AiConfidence,
            ReviewedBy = dto.ReviewedBy,
            RecordedAt = dto.RecordedAt == default ? DateTime.UtcNow : dto.RecordedAt
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
        await EnsureViolationAccessAsync(existing.Participation.ExamSlot.Class);

        // update allowed fields
        existing.IsReviewed = dto.IsReviewed;
        existing.ReviewedBy = dto.ReviewedBy;

        await _repo.UpdateAsync(id, dto);
        await PublishViolationChangedAsync(id, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await GetViolationWithAccessDataAsync(id);
        if (existing == null) return false;
        await EnsureViolationAccessAsync(existing.Participation.ExamSlot.Class);

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

    private async Task EnsureViolationAccessAsync(Class cls)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId == cls.InstitutionId)
            return;

        if (user.Role == AppRole.Lecturer &&
            user.InstitutionId == cls.InstitutionId &&
            user.Id == cls.LecturerId)
        {
            return;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

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
