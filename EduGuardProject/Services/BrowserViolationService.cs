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

    private const int TerminationThreshold = 3;

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly ILogger<BrowserViolationService> _logger;

    public BrowserViolationService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        ILogger<BrowserViolationService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<BrowserViolationResponseDto> RecordAsync(BrowserViolationRequestDto dto)
    {
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

        _context.ViolationLogs.Add(log);
        await _context.SaveChangesAsync();

        var currentCount = await _context.ViolationLogs
            .CountAsync(v => v.ParticipationId == participation.Id && BrowserViolationTypes.Contains(v.violationType));

        var examTerminated = currentCount >= TerminationThreshold;
        if (examTerminated)
        {
            participation.Status = ParticipationStatus.Disqualified;
            participation.DisqualifiedReason = $"Terminated automatically after {currentCount} browser violations.";
            participation.ActualEnd ??= now;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "BrowserViolation recorded. StudentId={StudentId} ParticipationId={ParticipationId} ViolationType={ViolationType} CurrentCount={CurrentCount} ExamTerminated={ExamTerminated}",
            user.Id, participation.Id, dto.ViolationType, currentCount, examTerminated);

        var payload = new
        {
            participationId = participation.Id,
            violationType = dto.ViolationType,
            currentViolationCount = currentCount,
            examTerminated,
            recordedAt = now
        };

        var cls = participation.ExamSlot.Class;
        await _realtime.PushExamStudentAsync(participation.ExamSlotId, participation.StudentId, HubEvents.BrowserViolationDetected, payload);
        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.BrowserViolationDetected, payload);
        await _realtime.PublishDataChangedAsync(
            "violations",
            "browser-violation",
            institutionId: cls.InstitutionId,
            lecturerId: cls.LecturerId,
            userId: participation.StudentId,
            data: payload);

        if (examTerminated)
        {
            var terminatedPayload = new
            {
                participationId = participation.Id,
                participation.ExamSlotId,
                reason = participation.DisqualifiedReason,
                terminatedAt = now
            };

            await _realtime.PushExamStudentAsync(participation.ExamSlotId, participation.StudentId, HubEvents.ExamTerminated, terminatedPayload);
        }

        return new BrowserViolationResponseDto
        {
            Success = true,
            CurrentViolationCount = currentCount,
            ExamTerminated = examTerminated
        };
    }
}
