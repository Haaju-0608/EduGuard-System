using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.SignalR;

namespace EduGuardProject.Services;

public class RealtimeEventDispatcher : IRealtimeEventDispatcher
{
    private readonly IHubContext<ExamHub> _examHub;
    private readonly IHubContext<AttendanceHub> _attendanceHub;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IHubContext<DashboardHub> _dashboardHub;

    public RealtimeEventDispatcher(
        IHubContext<ExamHub> examHub,
        IHubContext<AttendanceHub> attendanceHub,
        IHubContext<NotificationHub> notificationHub,
        IHubContext<DashboardHub> dashboardHub)
    {
        _examHub = examHub;
        _attendanceHub = attendanceHub;
        _notificationHub = notificationHub;
        _dashboardHub = dashboardHub;
    }

    public Task SendGroupAsync(
        RealtimeHubKind hub,
        string group,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default) =>
        HubClients(hub).Group(group).SendAsync(eventName, payload, cancellationToken);

    public Task PushUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Notifications, HubGroups.User(userId), eventName, payload, cancellationToken);

    public Task PushExamLecturersAsync(Guid examSlotId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Exams, HubGroups.ExamLecturers(examSlotId), eventName, payload, cancellationToken);

    public Task PushExamStudentAsync(Guid examSlotId, Guid studentId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Exams, HubGroups.ExamStudent(examSlotId, studentId), eventName, payload, cancellationToken);

    public Task PushAttendanceSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Attendance, HubGroups.Attendance(sessionId), eventName, payload, cancellationToken);

    public Task PushClassStudentsAsync(Guid classId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Attendance, HubGroups.ClassStudents(classId), eventName, payload, cancellationToken);

    public Task PushInstitutionAdminsAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Notifications, HubGroups.InstitutionAdmins(institutionId), eventName, payload, cancellationToken);

    public Task PushSuperAdminsAsync(string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Notifications, HubGroups.Role(AppRole.SuperAdmin), eventName, payload, cancellationToken);

    public Task PushDashboardSystemAsync(string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Dashboard, HubGroups.DashboardSystem(), eventName, payload, cancellationToken);

    public Task PushDashboardInstitutionAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Dashboard, HubGroups.DashboardInstitution(institutionId), eventName, payload, cancellationToken);

    public Task PushDashboardLecturerAsync(Guid lecturerId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        SendGroupAsync(RealtimeHubKind.Dashboard, HubGroups.DashboardLecturer(lecturerId), eventName, payload, cancellationToken);

    public async Task PublishDataChangedAsync(
        string resource,
        string action,
        Guid? institutionId = null,
        Guid? lecturerId = null,
        Guid? userId = null,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            resource,
            action,
            institutionId,
            lecturerId,
            userId,
            data,
            changedAt = DateTime.UtcNow
        };

        var tasks = new List<Task>();
        AddDashboardScopeTasks(tasks, HubGroups.DashboardSystem(), payload, cancellationToken);

        if (institutionId.HasValue)
            AddDashboardScopeTasks(tasks, HubGroups.DashboardInstitution(institutionId.Value), payload, cancellationToken);

        if (lecturerId.HasValue)
            AddDashboardScopeTasks(tasks, HubGroups.DashboardLecturer(lecturerId.Value), payload, cancellationToken);

        if (userId.HasValue)
            tasks.Add(PushUserAsync(userId.Value, HubEvents.ResourceChanged, payload, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private void AddDashboardScopeTasks(
        ICollection<Task> tasks,
        string group,
        object payload,
        CancellationToken cancellationToken)
    {
        tasks.Add(SendGroupAsync(RealtimeHubKind.Dashboard, group, HubEvents.ResourceChanged, payload, cancellationToken));
        tasks.Add(SendGroupAsync(RealtimeHubKind.Dashboard, group, HubEvents.DashboardStatsChanged, payload, cancellationToken));
        tasks.Add(SendGroupAsync(RealtimeHubKind.Dashboard, group, HubEvents.ReportDataChanged, payload, cancellationToken));
    }

    private IHubClients HubClients(RealtimeHubKind hub) => hub switch
    {
        RealtimeHubKind.Notifications => _notificationHub.Clients,
        RealtimeHubKind.Exams => _examHub.Clients,
        RealtimeHubKind.Attendance => _attendanceHub.Clients,
        RealtimeHubKind.Dashboard => _dashboardHub.Clients,
        _ => throw new ArgumentOutOfRangeException(nameof(hub), hub, null)
    };
}
