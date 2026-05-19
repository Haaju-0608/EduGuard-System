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

        public AuthService(AppDbContext context, Supabase.Client supabaseClient)
        {
            _context = context;
            _supabaseClient = supabaseClient;
        }

        // CHỨC NĂNG ĐĂNG NHẬP (LOGIN)
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            try
            {
                // Bước 1: Gọi lên Supabase Auth để xác thực tài khoản
                var session = await _supabaseClient.Auth.SignIn(request.Email, request.Password);

                if (session == null || string.IsNullOrEmpty(session.AccessToken))
                {
                    return null;
                }

                // Bước 2: Tìm thông tin chi tiết (Role, InstitutionId) của User đó trong Database local của mình
                var userDetail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (userDetail == null)
                {
                    throw new Exception("Tài khoản tồn tại trên Auth hệ thống nhưng không tìm thấy dữ liệu phân quyền trong Database.");
                }

                // Bước 3: Đóng gói toàn bộ thông tin trả về cho Controller
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
            catch (Exception ex)
            {
                // Có thể bổ sung Log lỗi ở đây
                throw new Exception(ex.Message);
            }
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

            return result > 0;
        }
    }
}