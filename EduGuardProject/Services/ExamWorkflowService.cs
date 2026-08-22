using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ExamWorkflowService : IExamWorkflowService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly INotificationDispatcher _notifications;
    private readonly IExamPresenceTracker _presence;

    public ExamWorkflowService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        INotificationDispatcher notifications,
        IExamPresenceTracker presence)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _notifications = notifications;
        _presence = presence;
    }

    public async Task<object> JoinAsync(Guid participationId, CancellationToken cancellationToken = default)
    {
        if (participationId == Guid.Empty)
            throw new InvalidOperationException("Participation id is required.");

        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStudentOwnerAsync(participation);
        var now = DateTime.UtcNow;
        if (participation.ExamSlot.Status is ExamSlotStatus.Cancelled or ExamSlotStatus.Completed)
            throw new InvalidOperationException("This exam slot is not active.");
        if (now < participation.ExamSlot.StartTime)
            throw new InvalidOperationException("The exam has not started yet.");
        if (now > participation.ExamSlot.EndTime)
            throw new InvalidOperationException("The exam has already ended.");
        if (participation.Status is not ParticipationStatus.Absent and not ParticipationStatus.Joined)
            throw new InvalidOperationException("This exam participation cannot join again.");

        var hasAttendance = await _context.AttendanceRecords
     .AsNoTracking()
     .AnyAsync(r =>
         r.StudentId == participation.StudentId &&
         r.Session.ExamSlotId == participation.ExamSlotId &&
         r.Session.Status != SessionStatus.Cancelled &&
         (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late),
         cancellationToken);
        if (!hasAttendance)
            throw new InvalidOperationException("You must be marked present or late before you can start the exam.");

        if (!participation.IdentityVerifiedAt.HasValue)
            throw new InvalidOperationException(
                "You need to verify your face before entering the exam. If the AI ​​fails to recognize you," +
                "Please contact the supervisor for manual approval.");

        participation.ExamSlot.Status = ExamSlotStatus.InProgress;
        participation.Status = ParticipationStatus.Joined;
        participation.ActualStart ??= now;
        await _context.SaveChangesAsync(cancellationToken);

        _presence.Heartbeat(participation.Id, now);
        var payload = await BuildStudentPayloadAsync(participation, now, cancellationToken);
        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.StudentJoinedExam, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "joined", payload, cancellationToken);
        await _notifications.SendToUserAsync(
            participation.ExamSlot.Class.LecturerId,
            "Sinh viên đã tham gia kỳ thi",
            $"{participation.Student.FullName} đã tham gia kỳ thi.",
            NotificationType.ExamReminder,
            ReferenceTypeEnum.ExamSlot,
            participation.ExamSlotId,
            cancellationToken);

        return payload;
    }

    public async Task<object> HeartbeatAsync(Guid participationId, DateTime? clientTime = null, CancellationToken cancellationToken = default)
    {
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStudentOwnerAsync(participation);
        EnsureJoined(participation);

        var now = DateTime.UtcNow;
        _presence.Heartbeat(participation.Id, now);

        var payload = new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            lastSeenAt = now,
            clientTime
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.StudentHeartbeat, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "heartbeat", payload, cancellationToken);
        return payload;
    }

    public async Task<object> SubmitAsync(Guid participationId, string? recordingVideoPath = null, CancellationToken cancellationToken = default)
    {
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStudentOwnerAsync(participation);
        EnsureJoined(participation);

        var now = DateTime.UtcNow;
        participation.Status = ParticipationStatus.Submitted;
        participation.ActualEnd = now;
        if (!string.IsNullOrWhiteSpace(recordingVideoPath))
            participation.RecordingVideoPath = recordingVideoPath;

        _presence.MarkOffline(participation.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var submittedCount = await CountByStatusAsync(participation.ExamSlotId, ParticipationStatus.Submitted, cancellationToken);
        var totalStudents = await _context.ExamParticipations.CountAsync(p => p.ExamSlotId == participation.ExamSlotId, cancellationToken);

        var payload = new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            submittedAt = now,
            submittedCount,
            totalStudents
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.ExamSubmitted, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "submitted", payload, cancellationToken);
        return payload;
    }

    public async Task<object> LeaveAsync(Guid participationId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStudentOwnerAsync(participation);
        EnsureJoined(participation);

        var now = DateTime.UtcNow;
        participation.Status = ParticipationStatus.Left;
        participation.ActualEnd ??= now;
        _presence.MarkOffline(participation.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            disconnectedAt = now,
            reason = string.IsNullOrWhiteSpace(reason) ? "Leave" : reason
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.StudentOffline, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "left", payload, cancellationToken);
        return payload;
    }

    public async Task<object> DisqualifyAsync(Guid participationId, string reason, CancellationToken cancellationToken = default)
    {
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStaffAccessAsync(participation.ExamSlot.Class);
        EnsureJoined(participation);

        var now = DateTime.UtcNow;
        participation.Status = ParticipationStatus.Disqualified;
        participation.DisqualifiedReason = reason;
        participation.ActualEnd ??= now;
        _presence.MarkOffline(participation.Id);
        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            reason,
            disqualifiedAt = now
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.Disqualified, payload, cancellationToken);
        await _realtime.PushExamStudentAsync(participation.ExamSlotId, participation.StudentId, HubEvents.Disqualified, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "disqualified", payload, cancellationToken);
        await _notifications.SendToUserAsync(
            participation.StudentId,
            "Bạn đã bị loại khỏi kỳ thi",
            reason,
            NotificationType.ViolationDetected,
            ReferenceTypeEnum.ExamSlot,
            participation.ExamSlotId,
            cancellationToken);

        return payload;
    }

    public async Task<object> VoidAsync(Guid participationId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Void reason is required.");

        var participation = await GetParticipationAsync(participationId, cancellationToken);
        await EnsureStaffAccessAsync(participation.ExamSlot.Class);
        if (participation.Status != ParticipationStatus.Submitted)
            throw new InvalidOperationException("Only a SUBMITTED exam participation can be voided.");

        var now = DateTime.UtcNow;
        var records = await _context.StudentExamRecords
            .Where(r =>
                r.ExamSlotId == participation.ExamSlotId &&
                r.StudentId == participation.StudentId &&
                r.Status != StudentExamRecordStatus.Deleted)
            .ToListAsync(cancellationToken);

        participation.Status = ParticipationStatus.Disqualified;
        participation.DisqualifiedReason = reason.Trim();
        participation.ActualEnd ??= now;
        foreach (var record in records)
            record.Status = StudentExamRecordStatus.Deleted;

        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            reason = participation.DisqualifiedReason,
            voidedAt = now,
            invalidatedRecordCount = records.Count
        };

        await _realtime.PushExamLecturersAsync(participation.ExamSlotId, HubEvents.Disqualified, payload, cancellationToken);
        await _realtime.PushExamStudentAsync(participation.ExamSlotId, participation.StudentId, HubEvents.Disqualified, payload, cancellationToken);
        await PublishParticipationChangedAsync(participation, "voided", payload, cancellationToken);
        await _notifications.SendToUserAsync(
            participation.StudentId,
            "Kết quả bài thi đã bị hủy",
            participation.DisqualifiedReason,
            NotificationType.ViolationDetected,
            ReferenceTypeEnum.ExamSlot,
            participation.ExamSlotId,
            cancellationToken);

        return payload;
    }

    public async Task<object> GetRealtimeStateAsync(Guid examSlotId, CancellationToken cancellationToken = default)
    {
        var exam = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == examSlotId, cancellationToken)
            ?? throw new InvalidOperationException("Exam slot not found.");

        await EnsureStaffAccessAsync(exam.Class);

        var now = DateTime.UtcNow;
        var onlineThreshold = now.AddSeconds(-60);

        var participants = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.Student)
            .Where(p => p.ExamSlotId == examSlotId)
            .OrderBy(p => p.Student.FullName)
            .ToListAsync(cancellationToken);

        var violationCounts = await _context.ViolationLogs
            .AsNoTracking()
            .Where(v => v.Participation.ExamSlotId == examSlotId)
            .GroupBy(v => v.ParticipationId)
            .Select(g => new { ParticipationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParticipationId, x => x.Count, cancellationToken);

        var students = participants.Select(p =>
        {
            var lastSeenAt = _presence.GetLastSeen(p.Id);
            var isOnline = _presence.IsOnline(p.Id, onlineThreshold) && p.Status == ParticipationStatus.Joined;
            return new
            {
                participationId = p.Id,
                p.StudentId,
                p.Student.FullName,
                p.Status,
                p.ActualStart,
                p.ActualEnd,
                lastSeenAt,
                isOnline,
                violationCount = violationCounts.GetValueOrDefault(p.Id)
            };
        }).ToList();

        return new
        {
            examSlotId,
            exam.ExamName,
            exam.StartTime,
            exam.EndTime,
            totalStudents = participants.Count,
            onlineCount = students.Count(s => s.isOnline),
            offlineCount = students.Count(s => !s.isOnline && s.Status == ParticipationStatus.Joined),
            submittedCount = students.Count(s => s.Status == ParticipationStatus.Submitted),
            disqualifiedCount = students.Count(s => s.Status == ParticipationStatus.Disqualified),
            violationCount = violationCounts.Values.Sum(),
            students
        };
    }

    private async Task<ExamParticipation> GetParticipationAsync(Guid participationId, CancellationToken cancellationToken)
    {
        return await _context.ExamParticipations
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId, cancellationToken)
            ?? throw new InvalidOperationException("Exam participation not found.");
    }

    private async Task<object> BuildStudentPayloadAsync(ExamParticipation participation, DateTime joinedAt, CancellationToken cancellationToken)
    {
        var submittedCount = await CountByStatusAsync(participation.ExamSlotId, ParticipationStatus.Submitted, cancellationToken);
        var onlineThreshold = DateTime.UtcNow.AddSeconds(-60);
        var onlineCount = await _context.ExamParticipations
            .AsNoTracking()
            .Where(p => p.ExamSlotId == participation.ExamSlotId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return new
        {
            participationId = participation.Id,
            participation.ExamSlotId,
            examName = participation.ExamSlot.ExamName,
            participation.StudentId,
            participation.Student.FullName,
            joinedAt,
            submittedCount,
            onlineCount = _presence.CountOnline(onlineCount, onlineThreshold)
        };
    }

    private async Task<int> CountByStatusAsync(Guid examSlotId, ParticipationStatus status, CancellationToken cancellationToken) =>
        await _context.ExamParticipations.CountAsync(p => p.ExamSlotId == examSlotId && p.Status == status, cancellationToken);

    private async Task EnsureStudentOwnerAsync(ExamParticipation participation)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.Student || user.Id != participation.StudentId)
            throw new UnauthorizedAccessException("Only the owner student can perform this exam action.");
    }

    private static void EnsureJoined(ExamParticipation participation)
    {
        if (participation.Status != ParticipationStatus.Joined)
            throw new InvalidOperationException("This action is only allowed while the participation is JOINED.");
    }

    private async Task EnsureStaffAccessAsync(Class cls)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role != AppRole.Lecturer && user.Role != AppRole.SchoolAdmin)
            throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureStaffOrStudentOwnerAsync(ExamParticipation participation)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
        {
            if (user.Id == participation.StudentId)
                return;
            throw new UnauthorizedAccessException("Only the owner student can perform this exam action.");
        }

        await EnsureStaffAccessAsync(participation.ExamSlot.Class);
    }

    private Task PublishParticipationChangedAsync(
        ExamParticipation participation,
        string action,
        object payload,
        CancellationToken cancellationToken)
    {
        var cls = participation.ExamSlot.Class;
        return _realtime.PublishDataChangedAsync(
            "exam-participations",
            action,
            institutionId: cls.InstitutionId,
            lecturerId: cls.LecturerId,
            userId: participation.StudentId,
            data: payload,
            cancellationToken: cancellationToken);
    }
}
