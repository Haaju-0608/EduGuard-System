namespace EduGuardProject.DTOs.Request
{
    public class RegisterRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!; // Password chỉ dùng để gửi lên Supabase, không lưu vào DB
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }

        // Nếu tạo School_Admin, Lecturer hoặc Student thì truyền InstitutionId vào
        public Guid? InstitutionId { get; set; }

        // Chỉ bắt buộc nếu tạo sinh viên
        public string? StudentCode { get; set; }
    }
}
