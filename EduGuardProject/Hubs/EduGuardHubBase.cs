using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using EduGuardProject.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Hubs;

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
        if (!TryGetUserIdFromClaimsOrToken(out var userId))
            throw new HubException("User is not authenticated.");

        var user = await DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null && u.Status == UserStatus.Active);

        if (user == null)
            throw new HubException("User profile was not found or is inactive.");

        return user;
    }

    private bool TryGetUserIdFromClaimsOrToken(out Guid userId)
    {
        var userIdText = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        if (Guid.TryParse(userIdText, out userId))
            return true;

        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["access_token"].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = httpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return false;

        var jwtToken = handler.ReadJwtToken(token);
        var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        return Guid.TryParse(sub, out userId);
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
