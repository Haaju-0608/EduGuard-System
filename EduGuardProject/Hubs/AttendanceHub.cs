using EduGuardProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

public class AttendanceHub : EduGuardHubBase
{
    public AttendanceHub(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task JoinAttendanceSession(Guid sessionId)
    {
        var access = await GetAttendanceAccessAsync(sessionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Attendance(sessionId));

        if (access.IsStaff)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.AttendanceLecturers(sessionId));
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ClassStudents(access.ClassId));
        }

        await Clients.Caller.SendAsync(HubEvents.AttendanceSessionJoined, new
        {
            sessionId,
            classId = access.ClassId,
            group = HubGroups.Attendance(sessionId),
            isStaff = access.IsStaff
        });
    }

    public async Task JoinClassAttendance(Guid classId)
    {
        var user = await GetRequiredCurrentUserAsync();
        var cls = await DbContext.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId && c.DeletedAt == null);

        if (cls == null)
            throw new HubException("Class was not found.");

        if (CanAccessClassAsStaff(user, cls))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ClassStudents(classId));
            await Clients.Caller.SendAsync(HubEvents.ClassAttendanceJoined, new { classId, isStaff = true });
            return;
        }

        if (user.Role == AppRole.Student)
        {
            var enrolled = await DbContext.ClassEnrollments
                .AsNoTracking()
                .AnyAsync(e => e.ClassId == classId && e.StudentId == user.Id && e.Status == EnrollmentStatus.Active);

            if (enrolled)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ClassStudents(classId));
                await Clients.Caller.SendAsync(HubEvents.ClassAttendanceJoined, new { classId, isStaff = false });
                return;
            }
        }

        throw new HubException("You do not have access to this class attendance group.");
    }

    public async Task LeaveAttendanceSession(Guid sessionId)
    {
        var access = await GetAttendanceAccessAsync(sessionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Attendance(sessionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.AttendanceLecturers(sessionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.ClassStudents(access.ClassId));

        await Clients.Caller.SendAsync(HubEvents.AttendanceSessionLeft, new { sessionId });
    }

    private async Task<AttendanceAccess> GetAttendanceAccessAsync(Guid sessionId)
    {
        var user = await GetRequiredCurrentUserAsync();
        var session = await DbContext.AttendanceSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Status != SessionStatus.Cancelled);

        if (session == null)
            throw new HubException("Attendance session was not found.");

        if (CanAccessClassAsStaff(user, session.Class))
            return new AttendanceAccess(session.ClassId, true);

        if (user.Role == AppRole.Student)
        {
            var enrolled = await DbContext.ClassEnrollments
                .AsNoTracking()
                .AnyAsync(e => e.ClassId == session.ClassId && e.StudentId == user.Id && e.Status == EnrollmentStatus.Active);

            if (enrolled)
                return new AttendanceAccess(session.ClassId, false);
        }

        throw new HubException("You do not have access to this attendance session group.");
    }

    private sealed record AttendanceAccess(Guid ClassId, bool IsStaff);
}
