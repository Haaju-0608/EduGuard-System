using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface IUserService
    {
        Task<(IEnumerable<UserResponseDto> Items, int TotalCount)> GetUsersAsync(string? search, string? sort, int page, int pageSize);
        Task<UserResponseDto?> GetUserByIdAsync(Guid id);
        Task<UserResponseDto> CreateUserAsync(CreateUserDto dto);
        Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto);
        Task<bool> DeleteUserAsync(Guid id);
    }
}
