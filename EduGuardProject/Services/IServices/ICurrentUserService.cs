using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Task<User?> GetCurrentUserAsync();
    Task<User> GetRequiredUserAsync();
    Task EnsureRoleAsync(params AppRole[] allowedRoles);
    Task EnsureInstitutionAccessAsync(Guid institutionId);
}
