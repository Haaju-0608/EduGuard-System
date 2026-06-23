using EduGuardProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

public class DashboardHub : EduGuardHubBase
{
    public DashboardHub(AppDbContext dbContext) : base(dbContext)
    {
    }

    public override async Task OnConnectedAsync()
    {
        var user = await GetRequiredCurrentUserAsync();

        await Clients.Caller.SendAsync(HubEvents.DashboardHubConnected, new
        {
            userId = user.Id,
            role = user.Role.ToString(),
            institutionId = user.InstitutionId
        });

        await base.OnConnectedAsync();
    }

    public async Task JoinSystemDashboard()
    {
        var user = await GetRequiredCurrentUserAsync();
        if (user.Role != AppRole.SuperAdmin)
            throw new HubException("Only Super Admin can join system dashboard.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.DashboardSystem());
        await Clients.Caller.SendAsync(HubEvents.DashboardScopeJoined, new
        {
            scope = "system"
        });
    }

    public async Task JoinInstitutionDashboard(Guid institutionId)
    {
        var user = await GetRequiredCurrentUserAsync();
        if (!IsAdminForInstitution(user, institutionId))
            throw new HubException("You do not have access to this institution dashboard.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.DashboardInstitution(institutionId));
        await Clients.Caller.SendAsync(HubEvents.DashboardScopeJoined, new
        {
            scope = "institution",
            institutionId
        });
    }

    public async Task JoinLecturerDashboard(Guid lecturerId)
    {
        var user = await GetRequiredCurrentUserAsync();
        var lecturer = await DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Id == lecturerId &&
                u.Role == AppRole.Lecturer &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active);

        if (lecturer == null)
            throw new HubException("Lecturer was not found.");

        if (user.Role != AppRole.SuperAdmin &&
            user.Id != lecturerId &&
            (user.Role != AppRole.SchoolAdmin || user.InstitutionId != lecturer.InstitutionId))
        {
            throw new HubException("You do not have access to this lecturer dashboard.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.DashboardLecturer(lecturerId));
        await Clients.Caller.SendAsync(HubEvents.DashboardScopeJoined, new
        {
            scope = "lecturer",
            lecturerId
        });
    }
}
