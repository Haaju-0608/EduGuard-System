using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IUserRepository
    {
        Task<User> CreateUserAsync(User user);
        Task<User?> GetUserByEmailAsync(string email);
    }
}
