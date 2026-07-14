using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }

        // Thông tin phân quyền cực kỳ quan trọng cho Frontend
        public string FullName { get; set; } = string.Empty;
        public AppRole Role { get; set; }
        public Guid? InstitutionId { get; set; } // Nếu là Super Admin thì cái này sẽ nhận NULL
    }
}
