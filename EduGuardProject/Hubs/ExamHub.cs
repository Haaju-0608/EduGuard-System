using System.Collections.Concurrent;
using EduGuardProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

public class ExamHub : EduGuardHubBase
{
    private static readonly ConcurrentDictionary<string, ExamConnectionState> Connections = new();

    public ExamHub(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task JoinExam(Guid examSlotId)
    {
        var access = await GetExamAccessAsync(examSlotId);

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Exam(examSlotId));

        if (access.IsStaff)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ExamLecturers(examSlotId));
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ExamStudent(examSlotId, access.User.Id));
            Connections[Context.ConnectionId] = new ExamConnectionState(
                examSlotId,
                access.User.Id,
                access.User.FullName,
                DateTime.UtcNow);

            var onlineAt = DateTime.UtcNow;
            await Clients.Group(HubGroups.ExamLecturers(examSlotId)).SendAsync(HubEvents.StudentOnline, new
            {
                examSlotId,
                studentId = access.User.Id,
                fullName = access.User.FullName,
                onlineAt
            });
        }

        await Clients.Caller.SendAsync(HubEvents.ExamJoined, new
        {
            examSlotId,
            group = HubGroups.Exam(examSlotId),
            isStaff = access.IsStaff
        });
    }

    public async Task JoinLecturerDashboard(Guid examSlotId)
    {
        var access = await GetExamAccessAsync(examSlotId);
        if (!access.IsStaff)
            throw new HubException("Only lecturer/admin users can join exam dashboard group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Exam(examSlotId));
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ExamLecturers(examSlotId));

        await Clients.Caller.SendAsync(HubEvents.ExamDashboardJoined, new
        {
            examSlotId,
            group = HubGroups.ExamLecturers(examSlotId)
        });
    }

    public async Task LeaveExam(Guid examSlotId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Exam(examSlotId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.ExamLecturers(examSlotId));

        if (Connections.TryRemove(Context.ConnectionId, out var state))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.ExamStudent(state.ExamSlotId, state.StudentId));
            await NotifyStudentOfflineAsync(state, "LeaveExam");
        }

        await Clients.Caller.SendAsync(HubEvents.ExamLeft, new { examSlotId });
    }

    public async Task Heartbeat(Guid examSlotId)
    {
        var access = await GetExamAccessAsync(examSlotId);
        if (access.IsStaff)
            return;

        Connections[Context.ConnectionId] = new ExamConnectionState(
            examSlotId,
            access.User.Id,
            access.User.FullName,
            DateTime.UtcNow);

        await Clients.Group(HubGroups.ExamLecturers(examSlotId)).SendAsync(HubEvents.StudentHeartbeat, new
        {
            examSlotId,
            studentId = access.User.Id,
            fullName = access.User.FullName,
            lastSeenAt = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var state))
            await NotifyStudentOfflineAsync(state, "Disconnected");

        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyStudentOfflineAsync(ExamConnectionState state, string reason)
    {
        await Clients.Group(HubGroups.ExamLecturers(state.ExamSlotId)).SendAsync(HubEvents.StudentOffline, new
        {
            examSlotId = state.ExamSlotId,
            studentId = state.StudentId,
            fullName = state.FullName,
            disconnectedAt = DateTime.UtcNow,
            reason
        });
    }

    private async Task<ExamAccess> GetExamAccessAsync(Guid examSlotId)
    {
        var user = await GetRequiredCurrentUserAsync();
        var exam = await DbContext.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == examSlotId);

        if (exam == null)
            throw new HubException("Exam slot was not found.");

        if (CanAccessClassAsStaff(user, exam.Class))
            return new ExamAccess(user, true);

        if (user.Role == AppRole.Student)
        {
            var hasParticipation = await DbContext.ExamParticipations
                .AsNoTracking()
                .AnyAsync(p => p.ExamSlotId == examSlotId && p.StudentId == user.Id);

            if (hasParticipation)
                return new ExamAccess(user, false);
        }

        throw new HubException("You do not have access to this exam group.");
    }

    private sealed record ExamAccess(User User, bool IsStaff);

    private sealed record ExamConnectionState(Guid ExamSlotId, Guid StudentId, string FullName, DateTime LastSeenAt);
}
