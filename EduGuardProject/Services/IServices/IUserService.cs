using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices
{
    public interface IUserService
    {
        Task<(IEnumerable<UserResponseDto> Items, int TotalCount)> GetUsersAsync(Guid? institutionId, AppRole? excludeRole, string? search, string? sort, int page, int pageSize);
        Task<UserResponseDto?> GetUserByIdAsync(Guid id);
        Task<UserResponseDto> CreateUserAsync(CreateUserDto dto);
        Task<BulkImportUsersResponseDto> BulkImportUsersAsync(IFormFile file, Guid? forcedInstitutionId = null, CancellationToken cancellationToken = default);
        Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto);
        Task<bool> UpdateMyProfileAsync(Guid id, UpdateMyProfileDto dto);
        Task<bool> DeleteUserAsync(Guid id);
    }
}
