using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Supabase;

namespace EduGuardProject.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly Supabase.Client _supabaseClient; // Tiêm client Supabase vào đây
        private readonly INotificationDispatcher _notifications;
        private readonly IRealtimeEventDispatcher _realtime;

        public AuthService(
            AppDbContext context,
            Supabase.Client supabaseClient,
            INotificationDispatcher notifications,
            IRealtimeEventDispatcher realtime)
        {
            _context = context;
            _supabaseClient = supabaseClient;
            _notifications = notifications;
            _realtime = realtime;
        }

        // CHỨC NĂNG ĐĂNG NHẬP (LOGIN)
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var session = await _supabaseClient.Auth.SignIn(request.Email, request.Password);

            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                return null;
            }

            var normalizedEmail = request.Email.Trim().ToLower();
            var userDetail = await _context.Users
                .FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail);

            if (userDetail == null)
            {
                throw new InvalidOperationException(
                    "Tài khoản tồn tại trên Auth hệ thống nhưng không tìm thấy dữ liệu phân quyền trong Database.");
            }

            return new LoginResponseDto
            {
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken ?? string.Empty,
                ExpiresIn = (int)session.ExpiresIn,
                FullName = userDetail.FullName,
                Role = userDetail.Role,
                InstitutionId = userDetail.InstitutionId
            };
        }

        // CHỨC NĂNG TIẾP NHẬN ĐĂNG KÝ TRƯỜNG HỌC (CONTACT)
        public async Task<bool> SaveContactRequestAsync(ContactRequestDto request)
        {
            var newContact = new ContactRequest
            {
                SchoolName = request.SchoolName,
                ContactPersonName = request.ContactPersonName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Message = request.Message,
                CreatedAt = DateTime.UtcNow,
                Status = "PENDING"
            };

            _context.ContactRequests.Add(newContact);
            var result = await _context.SaveChangesAsync();
            if (result > 0)
            {
                await _notifications.PushContactRequestAsync(newContact);
                await _realtime.PublishDataChangedAsync(
                    "contact-requests",
                    "created",
                    data: new
                    {
                        newContact.Id,
                        newContact.SchoolName,
                        newContact.ContactPersonName,
                        newContact.Email,
                        newContact.PhoneNumber,
                        newContact.Status,
                        newContact.CreatedAt
                    });
            }

            return result > 0;
        }
    }
}
