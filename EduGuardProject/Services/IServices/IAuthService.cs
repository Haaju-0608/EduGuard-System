using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices
{
    public interface IAuthService
    {
        //Task<User> RegisterAsync(string email, string password, string fullName, string? phone, Guid? institutionId, string? studentCode);
        //Task<Supabase.Gotrue.Session?> LoginAsync(string email, string password);

        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
        Task<bool> SaveContactRequestAsync(ContactRequestDto request);
    }
}
