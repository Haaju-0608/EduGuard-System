using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

public class ExamHub : EduGuardHubBase
{
    private readonly IExamPresenceTracker _presence;

    public ExamHub(AppDbContext dbContext, IExamPresenceTracker presence) : base(dbContext)
    {
        _presence = presence;
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
            if (access.ParticipationStatus == ParticipationStatus.Joined)
            {
                var onlineAt = DateTime.UtcNow;
                var becameOnline = _presence.Connect(
                    Context.ConnectionId,
                    access.ParticipationId!.Value,
                    examSlotId,
                    access.User.Id,
                    access.User.FullName,
                    onlineAt);

                if (becameOnline)
                {
                    await Clients.Group(HubGroups.ExamLecturers(examSlotId)).SendAsync(HubEvents.StudentOnline, new
                    {
                        participationId = access.ParticipationId,
                        examSlotId,
                        studentId = access.User.Id,
                        fullName = access.User.FullName,
                        onlineAt
                    });
                }
            }
        }

        await Clients.Caller.SendAsync(HubEvents.ExamJoined, new
        {
            examSlotId,
            group = HubGroups.Exam(examSlotId),
            isStaff = access.IsStaff,
            participationStatus = access.ParticipationStatus
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

        var disconnected = _presence.Disconnect(Context.ConnectionId);
        if (disconnected != null)
        {
            var state = disconnected.Connection;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.ExamStudent(state.ExamSlotId, state.StudentId));
            if (disconnected.BecameOffline)
                await NotifyStudentOfflineAsync(state, "LeaveExam");
        }

        await Clients.Caller.SendAsync(HubEvents.ExamLeft, new { examSlotId });
    }

    public async Task Heartbeat(Guid examSlotId)
    {
        var access = await GetExamAccessAsync(examSlotId);
        if (access.IsStaff)
            return;
        if (access.ParticipationStatus != ParticipationStatus.Joined)
            throw new HubException("Heartbeat is only accepted while the participation is JOINED.");

        var lastSeenAt = DateTime.UtcNow;
        _presence.Heartbeat(access.ParticipationId!.Value, lastSeenAt);

        await Clients.Group(HubGroups.ExamLecturers(examSlotId)).SendAsync(HubEvents.StudentHeartbeat, new
        {
            participationId = access.ParticipationId,
            examSlotId,
            studentId = access.User.Id,
            fullName = access.User.FullName,
            lastSeenAt
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var disconnected = _presence.Disconnect(Context.ConnectionId);
        if (disconnected?.BecameOffline == true)
            await NotifyStudentOfflineAsync(disconnected.Connection, "Disconnected");

        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyStudentOfflineAsync(ExamPresenceConnection state, string reason)
    {
        await Clients.Group(HubGroups.ExamLecturers(state.ExamSlotId)).SendAsync(HubEvents.StudentOffline, new
        {
            participationId = state.ParticipationId,
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
            return new ExamAccess(user, true, null, null);

        if (user.Role == AppRole.Student)
        {
            var participation = await DbContext.ExamParticipations
                .AsNoTracking()
                .Where(p => p.ExamSlotId == examSlotId && p.StudentId == user.Id)
                .Select(p => new { p.Id, p.Status })
                .FirstOrDefaultAsync();

            if (participation != null)
                return new ExamAccess(user, false, participation.Id, participation.Status);
        }

        throw new HubException("You do not have access to this exam group.");
    }

    private sealed record ExamAccess(
        User User,
        bool IsStaff,
        Guid? ParticipationId,
        ParticipationStatus? ParticipationStatus);
}
