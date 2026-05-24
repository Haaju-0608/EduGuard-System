using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.Extensions.Configuration; 

namespace EduGuardProject.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly Supabase.Client _supabaseClient;
        private readonly IConfiguration _config; 

        //  3. Tiêm IConfiguration vào để lấy Key tự động
        public UserService(IUserRepository repo, Supabase.Client supabaseClient, IConfiguration config)
        {
            _repo = repo;
            _supabaseClient = supabaseClient;
            _config = config;
        }

        public async Task<(IEnumerable<UserResponseDto> Items, int TotalCount)> GetUsersAsync(string? search, string? sort, int page, int pageSize)
        {
            var (entities, totalCount) = await _repo.GetAllAsync(search, sort, page, pageSize);
            var dtos = entities.Select(MapToResponseDto);
            return (dtos, totalCount);
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto)
        {
            var adminAttrs = new Supabase.Gotrue.AdminUserAttributes
            {
                Email = dto.Email,
                Password = dto.Password,
                EmailConfirm = true
            };

            //  Lấy Service Role Key từ appsettings và gọi hàm AdminAuth chuẩn của Supabase C#
            var serviceKey = _config["Supabase:ServiceRoleKey"];
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);

            // Supabase C# trả về thẳng User luôn, không lồng ghép rườm rà
            var authUser = await adminAuth.CreateUser(adminAttrs);

            if (authUser?.Id == null)
                throw new Exception("Lỗi: Supabase không trả về ID người dùng.");

            var realUserId = Guid.Parse(authUser.Id); // Lấy thẳng ID

            // Vẫn chỉ định tên đầy đủ để không bị lú với Gotrue.User
            var entity = new EduGuardProject.Models.User
            {
                Id = realUserId,
                InstitutionId = dto.InstitutionId,
                StudentCode = dto.StudentCode,
                Email = dto.Email,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = dto.Role,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await Task.Delay(500);

            // 2. Kiểm tra xem Trigger dưới DB có "nhanh tay" tạo sẵn chưa
            var existingUser = await _repo.GetByIdAsync(realUserId);

            if (existingUser != null)
            {
                // Nếu Trigger đã tạo rồi -> Mình gọi lệnh Update đè thông tin lên
                existingUser.InstitutionId = dto.InstitutionId;
                existingUser.StudentCode = dto.StudentCode;
                existingUser.FullName = dto.FullName;
                existingUser.Phone = dto.Phone;
                existingUser.Role = dto.Role;
                existingUser.Status = dto.Status;
                existingUser.UpdatedAt = DateTime.UtcNow;

                await _repo.UpdateAsync(existingUser);
            }
            else
            {
                // Nếu chưa có (Tức là bạn đã tắt Trigger thành công) -> Mình tự Add mới
                await _repo.AddAsync(entity);
            }

            return MapToResponseDto(entity);
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            entity.InstitutionId = dto.InstitutionId;
            entity.StudentCode = dto.StudentCode;
            entity.FullName = dto.FullName;
            entity.Phone = dto.Phone;
            entity.Role = dto.Role;
            entity.Status = dto.Status;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await _repo.DeleteAsync(entity);

            // Gọi y hệt như hàm Create ở trên
            var serviceKey = _config["Supabase:ServiceRoleKey"];
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);
            await adminAuth.DeleteUser(id.ToString());

            return true;
        }

        private static UserResponseDto MapToResponseDto(EduGuardProject.Models.User e) => new()
        {
            Id = e.Id,
            InstitutionId = e.InstitutionId,
            StudentCode = e.StudentCode,
            Email = e.Email,
            FullName = e.FullName,
            Phone = e.Phone,
            Role = e.Role,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}