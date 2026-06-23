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

    public Task PushUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _notificationHub.Clients.Group(HubGroups.User(userId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushExamLecturersAsync(Guid examSlotId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _examHub.Clients.Group(HubGroups.ExamLecturers(examSlotId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushExamStudentAsync(Guid examSlotId, Guid studentId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _examHub.Clients.Group(HubGroups.ExamStudent(examSlotId, studentId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushAttendanceSessionAsync(Guid sessionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _attendanceHub.Clients.Group(HubGroups.Attendance(sessionId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushClassStudentsAsync(Guid classId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _attendanceHub.Clients.Group(HubGroups.ClassStudents(classId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushInstitutionAdminsAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _notificationHub.Clients.Group(HubGroups.InstitutionAdmins(institutionId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushSuperAdminsAsync(string eventName, object payload, CancellationToken cancellationToken = default) =>
        _notificationHub.Clients.Group(HubGroups.Role(AppRole.SuperAdmin)).SendAsync(eventName, payload, cancellationToken);

    public Task PushDashboardSystemAsync(string eventName, object payload, CancellationToken cancellationToken = default) =>
        _dashboardHub.Clients.Group(HubGroups.DashboardSystem()).SendAsync(eventName, payload, cancellationToken);

    public Task PushDashboardInstitutionAsync(Guid institutionId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _dashboardHub.Clients.Group(HubGroups.DashboardInstitution(institutionId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushDashboardLecturerAsync(Guid lecturerId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        _dashboardHub.Clients.Group(HubGroups.DashboardLecturer(lecturerId)).SendAsync(eventName, payload, cancellationToken);

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
        tasks.Add(_dashboardHub.Clients.Group(group).SendAsync(HubEvents.ResourceChanged, payload, cancellationToken));
        tasks.Add(_dashboardHub.Clients.Group(group).SendAsync(HubEvents.DashboardStatsChanged, payload, cancellationToken));
        tasks.Add(_dashboardHub.Clients.Group(group).SendAsync(HubEvents.ReportDataChanged, payload, cancellationToken));
    }
}
