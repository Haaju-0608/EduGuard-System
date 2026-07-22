using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IUserRepository
    {
        Task<User> CreateUserAsync(User user);
        Task<User?> GetUserByEmailAsync(string email);

        //Cho User
        Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(Guid? institutionId, AppRole? excludeRole, string? search, string? sort, int page, int pageSize);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(User user);
    }
}
