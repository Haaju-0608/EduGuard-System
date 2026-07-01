using EduGuardProject.Models;
using Microsoft.AspNetCore.SignalR;

namespace EduGuardProject.Hubs;

public class NotificationHub : EduGuardHubBase
{
    public NotificationHub(AppDbContext dbContext) : base(dbContext)
    {
    }

    public override async Task OnConnectedAsync()
    {
        var user = await GetRequiredCurrentUserAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(user.Id));
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Role(user.Role));

        if (user.InstitutionId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Institution(user.InstitutionId.Value));

            if (user.Role == AppRole.SchoolAdmin)
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.InstitutionAdmins(user.InstitutionId.Value));
        }

        await Clients.Caller.SendAsync(HubEvents.NotificationHubConnected, new
        {
            userId = user.Id,
            role = user.Role.ToString(),
            institutionId = user.InstitutionId
        });

        await base.OnConnectedAsync();
    }

    public async Task JoinInstitutionNotifications(Guid institutionId)
    {
        var user = await GetRequiredCurrentUserAsync();
        if (!IsAdminForInstitution(user, institutionId))
            throw new HubException("You do not have access to this institution notification group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Institution(institutionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.InstitutionAdmins(institutionId));
        await Clients.Caller.SendAsync(HubEvents.InstitutionNotificationsJoined, new { institutionId });
    }

    public async Task JoinSuperAdminNotifications()
    {
        var user = await GetRequiredCurrentUserAsync();
        if (user.Role != AppRole.SuperAdmin)
            throw new HubException("Only Super Admin can join system notification group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Role(AppRole.SuperAdmin));
        await Clients.Caller.SendAsync(HubEvents.SuperAdminNotificationsJoined);
    }
}
