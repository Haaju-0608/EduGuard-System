using System.Security.Claims;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _context;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public Guid? UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            var sub = context?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context?.User?.FindFirstValue("sub");

            if (Guid.TryParse(sub, out var id))
                return id;

            if (context?.Items.TryGetValue("UserId", out var userIdItem) == true)
            {
                if (userIdItem is Guid userId)
                    return userId;

                if (Guid.TryParse(userIdItem?.ToString(), out id))
                    return id;
            }

            return null;
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (UserId == null) return null;

        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == UserId && u.DeletedAt == null);
    }

    public async Task<User> GetRequiredUserAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            throw new UnauthorizedAccessException("User is not authenticated or profile not found.");
        return user;
    }

    public async Task EnsureRoleAsync(params AppRole[] allowedRoles)
    {
        var user = await GetRequiredUserAsync();
        if (!allowedRoles.Contains(user.Role))
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");
    }

    public async Task EnsureInstitutionAccessAsync(Guid institutionId)
    {
        var user = await GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin)
            return;

        if (user.InstitutionId != institutionId)
            throw new UnauthorizedAccessException("You do not have access to this institution's data.");
    }
}
