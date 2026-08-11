using System.Security.Claims;
using System.Text.Json;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace EduGuardProject.Services;

public class CurrentUserService : ICurrentUserService
{
    private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromSeconds(60);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _context;
    private readonly IDistributedCache _cache;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext context, IDistributedCache cache)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _cache = cache;
    }

    public static string ProfileCacheKey(Guid userId) => $"user-profile:{userId}";

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
        if (UserId is not Guid userId) return null;

        var cacheKey = ProfileCacheKey(userId);
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
            return cached.Length == 0 ? null : JsonSerializer.Deserialize<CachedUser>(cached)!.ToUser();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active);

        await _cache.SetStringAsync(
            cacheKey,
            user == null ? string.Empty : JsonSerializer.Serialize(CachedUser.FromUser(user)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ProfileCacheTtl });

        return user;
    }

    private sealed record CachedUser(
        Guid Id, Guid? InstitutionId, string? StudentCode, string Email, string FullName,
        string? Phone, DateTime CreatedAt, DateTime UpdatedAt, UserStatus Status, AppRole Role)
    {
        public static CachedUser FromUser(User u) => new(
            u.Id, u.InstitutionId, u.StudentCode, u.Email, u.FullName,
            u.Phone, u.CreatedAt, u.UpdatedAt, u.Status, u.Role);

        public User ToUser() => new()
        {
            Id = Id, InstitutionId = InstitutionId, StudentCode = StudentCode, Email = Email, FullName = FullName,
            Phone = Phone, CreatedAt = CreatedAt, UpdatedAt = UpdatedAt, Status = Status, Role = Role
        };
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
        if (!user.Role.IsInRoles(allowedRoles))
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");
    }

    public async Task EnsureInstitutionAccessAsync(Guid institutionId)
    {
        var user = await GetRequiredUserAsync();
        if (user.Role.ToCanonical() == AppRole.SuperAdmin)
            return;

        if (user.InstitutionId != institutionId)
            throw new UnauthorizedAccessException("You do not have access to this institution's data.");
    }
}
