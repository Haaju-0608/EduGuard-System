using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices;

public interface INotificationDispatcher
{
    Task<NotificationResponseDto> SendToUserAsync(
        Guid userId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponseDto>> SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponseDto>> SendToClassStudentsAsync(
        Guid classId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponseDto>> SendToInstitutionAdminsAsync(
        Guid institutionId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    Task PushContactRequestAsync(ContactRequest request, CancellationToken cancellationToken = default);
}

public interface IRealtimeEventDispatcher
{
    Task PushUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushExamLecturersAsync(Guid examSlotId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushExamStudentAsync(Guid examSlotId, Guid studentId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushAttendanceSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushClassStudentsAsync(Guid classId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushInstitutionAdminsAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushSuperAdminsAsync(string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushDashboardSystemAsync(string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushDashboardInstitutionAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PushDashboardLecturerAsync(Guid lecturerId, string eventName, object payload, CancellationToken cancellationToken = default);
    Task PublishDataChangedAsync(
        string resource,
        string action,
        Guid? institutionId = null,
        Guid? lecturerId = null,
        Guid? userId = null,
        object? data = null,
        CancellationToken cancellationToken = default);
}

public interface IExamWorkflowService
{
    Task<object> JoinAsync(Guid participationId, CancellationToken cancellationToken = default);
    Task<object> HeartbeatAsync(Guid participationId, DateTime? clientTime = null, CancellationToken cancellationToken = default);
    Task<object> SubmitAsync(Guid participationId, string? recordingVideoPath = null, CancellationToken cancellationToken = default);
    Task<object> LeaveAsync(Guid participationId, string? reason = null, CancellationToken cancellationToken = default);
    Task<object> DisqualifyAsync(Guid participationId, string reason, CancellationToken cancellationToken = default);
    Task<object> GetRealtimeStateAsync(Guid examSlotId, CancellationToken cancellationToken = default);
}

public sealed record ExamPresenceConnection(
    Guid ParticipationId,
    Guid ExamSlotId,
    Guid StudentId,
    string FullName,
    DateTime LastSeenAt);

public sealed record ExamPresenceDisconnect(
    ExamPresenceConnection Connection,
    bool BecameOffline);

public interface IExamPresenceTracker
{
    bool Connect(
        string connectionId,
        Guid participationId,
        Guid examSlotId,
        Guid studentId,
        string fullName,
        DateTime connectedAt);

    void Heartbeat(Guid participationId, DateTime seenAt);
    ExamPresenceDisconnect? Disconnect(string connectionId);
    void MarkOffline(Guid participationId);
    DateTime? GetLastSeen(Guid participationId);
    bool IsOnline(Guid participationId, DateTime onlineThreshold);
    int CountOnline(IEnumerable<Guid> participationIds, DateTime onlineThreshold);
}
