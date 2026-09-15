using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class BrowserViolationService : IBrowserViolationService
{
    private static readonly ViolationType[] BrowserViolationTypes =
    {
        ViolationType.TabSwitch,
        ViolationType.WindowBlur,
        ViolationType.ExitFullscreen
    };

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly INotificationDispatcher _notifications;
    private readonly IProctoringSettingsService _proctoringSettings;
    private readonly ILogger<BrowserViolationService> _logger;

    public BrowserViolationService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        INotificationDispatcher notifications,
        IProctoringSettingsService proctoringSettings,
        ILogger<BrowserViolationService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _notifications = notifications;
        _proctoringSettings = proctoringSettings;
        _logger = logger;
    }

    public async Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
        Guid participationId, int page, int pageSize)
    {
        var participation = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId)
            ?? throw new InvalidOperationException("Exam participation not found.");

        await EnsureAccessAsync(participation);

        var query = _context.ViolationLogs
            .AsNoTracking()
            .Where(v => v.ParticipationId == participationId && BrowserViolationTypes.Contains(v.violationType));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new ViolationlogResponeDto
            {
                Id = v.Id,
                ParticipationId = v.ParticipationId,
                EvidencePath = v.EvidencePath,
                Severity = v.severity,
                ViolationType = v.violationType,
                AiConfidence = v.AiConfidence,
                IsReviewed = v.IsReviewed,
                ReviewedBy = v.ReviewedBy,
                RecordedAt = v.RecordedAt
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<BrowserViolationResponseDto> RecordAsync(BrowserViolationRequestDto dto)
    {
        if (dto.ParticipationId == Guid.Empty)
            throw new InvalidOperationException("Participation id is required.");
        if (!Enum.IsDefined(dto.ViolationType) || !BrowserViolationTypes.Contains(dto.ViolationType))
            throw new InvalidOperationException("Violation type must be TabSwitch, WindowBlur, or ExitFullscreen.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.Student)
            throw new UnauthorizedAccessException("Only students can report browser violations.");

        var participation = await _context.ExamParticipations
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == dto.ParticipationId)
            ?? throw new InvalidOperationException("Exam participation not found.");

        if (participation.StudentId != user.Id)
            throw new UnauthorizedAccessException("You do not own this exam participation.");

        if (participation.Status != ParticipationStatus.Joined)
            throw new InvalidOperationException("This exam participation is not active.");

        var now = DateTime.UtcNow;
        if (participation.ExamSlot.Status == ExamSlotStatus.Cancelled)
            throw new InvalidOperationException("Cannot record a violation for a cancelled exam slot.");
        if (now < participation.ExamSlot.StartTime || now > participation.ExamSlot.EndTime)
            throw new InvalidOperationException("Browser violations can only be recorded during the exam slot.");
        if (!participation.ActualStart.HasValue || now < participation.ActualStart.Value)
            throw new InvalidOperationException("Cannot record a violation before the participation starts.");

        var log = new ViolationLog
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            violationType = dto.ViolationType,
            severity = ViolationSeverity.Warning,
            EvidencePath = null,
            AiConfidence = null,
            ReviewedBy = null,
            IsReviewed = false,
            RecordedAt = now
        };

        var cls = participation.ExamSlot.Class;

        // Use the threshold snapshotted at exam start (ExamParticipationServices.CreateAsync) so a
        // SchoolAdmin changing settings mid-exam only affects students who join afterward. Fall back
        // to the currently-effective settings only for participations created before this snapshot
        // feature existed (snapshot columns NULL).
        var browserNotifyThreshold = participation.BrowserNotifyThresholdSnapshot
            ?? (await _proctoringSettings.GetEffectiveAsync(cls.InstitutionId)).BrowserNotifyThreshold;

        _context.ViolationLogs.Add(log);
        await _context.SaveChangesAsync();

        var currentCount = await _context.ViolationLogs
            .CountAsync(v => v.ParticipationId == participation.Id && BrowserViolationTypes.Contains(v.violationType));

        // No auto-disqualify: reaching the notify threshold only alerts the lecturer, who decides.
        var thresholdReached = currentCount >= browserNotifyThreshold;

        _logger.LogInformation(
            "BrowserViolation recorded. StudentId={StudentId} ParticipationId={ParticipationId} ViolationType={ViolationType} CurrentCount={CurrentCount} ThresholdReached={ThresholdReached}",
            user.Id, participation.Id, dto.ViolationType, currentCount, thresholdReached);

        var payload = new
        {
            participationId = participation.Id,
            violationType = dto.ViolationType,
            currentViolationCount = currentCount,
            examTerminated = false,
            recordedAt = now
        };

        await _realtime.PushExamStudentAsync(participation.ExamSlotId, participation.StudentId, HubEvents.BrowserViolationDetected, payload);
        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.BrowserViolationDetected, payload);
        await _realtime.PublishDataChangedAsync(
            "violations",
            "browser-violation",
            institutionId: cls.InstitutionId,
            lecturerId: cls.LecturerId,
            userId: participation.StudentId,
            data: payload);

        // Fire exactly once, when the count first reaches the threshold.
        if (currentCount == browserNotifyThreshold)
        {
            var thresholdPayload = new
            {
                participationId = participation.Id,
                participation.ExamSlotId,
                participation.StudentId,
                participation.Student.FullName,
                currentBrowserViolationCount = currentCount,
                threshold = browserNotifyThreshold,
                kind = "browser"
            };
            await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.ViolationThresholdReached, thresholdPayload);
            await _notifications.SendToUserAsync(
                cls.LecturerId,
                "Sinh viên đạt ngưỡng cảnh báo vi phạm trình duyệt",
                $"Sinh viên {participation.Student.FullName} đã đạt {currentCount} vi phạm trình duyệt (chuyển tab/thoát fullscreen/mất focus). Vui lòng xem xét và quyết định có đánh dấu vi phạm quy chế (disqualify) hay không.",
                NotificationType.ViolationDetected,
                ReferenceTypeEnum.ExamParticipation,
                participation.Id);
        }

        return new BrowserViolationResponseDto
        {
            Success = true,
            CurrentViolationCount = currentCount,
            ExamTerminated = false
        };
    }

    private async Task EnsureAccessAsync(ExamParticipation participation)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var cls = participation.ExamSlot.Class;
        if (user.Role == AppRole.SuperAdmin ||
            (user.Role == AppRole.SchoolAdmin && user.InstitutionId == cls.InstitutionId) ||
            (user.Role == AppRole.Student && user.Id == participation.StudentId) ||
            (user.Role == AppRole.Lecturer && user.InstitutionId == cls.InstitutionId && user.Id == cls.LecturerId))
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }
}
