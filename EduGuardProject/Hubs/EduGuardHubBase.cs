using System.Security.Claims;
using EduGuardProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

[Authorize]
public abstract class EduGuardHubBase : Hub
{
    protected readonly AppDbContext DbContext;

    protected EduGuardHubBase(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public override async Task OnConnectedAsync()
    {
        await GetRequiredCurrentUserAsync();
        await base.OnConnectedAsync();
    }

    protected async Task<User> GetRequiredCurrentUserAsync()
    {
        var userIdText = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        if (Context.User?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(userIdText, out var userId))
        {
            throw new HubException("User is not authenticated.");
        }

        var user = await DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null && u.Status == UserStatus.Active);

        if (user == null)
            throw new HubException("User profile was not found or is inactive.");

        return user;
    }

    protected static bool IsAdminForInstitution(User user, Guid institutionId)
    {
        if (user.Role == AppRole.SuperAdmin)
            return true;

        return user.Role == AppRole.SchoolAdmin && user.InstitutionId == institutionId;
    }

    protected static bool CanAccessClassAsStaff(User user, Class cls)
    {
        if (IsAdminForInstitution(user, cls.InstitutionId))
            return true;

        return user.Role == AppRole.Lecturer && cls.LecturerId == user.Id;
    }
}
