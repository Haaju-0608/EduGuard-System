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
        private readonly IRealtimeEventDispatcher _realtime;

        //  3. Tiêm IConfiguration vào để lấy Key tự động
        public UserService(
            IUserRepository repo,
            Supabase.Client supabaseClient,
            IConfiguration config,
            IRealtimeEventDispatcher realtime)
        {
            _repo = repo;
            _supabaseClient = supabaseClient;
            _config = config;
            _realtime = realtime;
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

            var serviceKey = _config["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);

            // Supabase C# trả về thẳng User luôn, không lồng ghép rườm rà
            var authUser = await adminAuth.CreateUser(adminAttrs);

            if (authUser?.Id == null)
                throw new InvalidOperationException("Lỗi: Supabase không trả về ID người dùng.");

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

            await PublishUserChangedAsync(entity, "created");
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
            await PublishUserChangedAsync(entity, "updated");
            return true;
        }

        public async Task<bool> UpdateMyProfileAsync(Guid id, UpdateMyProfileDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            entity.FullName = dto.FullName.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await PublishUserChangedAsync(entity, "profile-updated");
            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await _repo.DeleteAsync(entity);

            var serviceKey = _config["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);
            await adminAuth.DeleteUser(id.ToString());

            await PublishUserChangedAsync(entity, "deleted");
            return true;
        }

        private Task PublishUserChangedAsync(EduGuardProject.Models.User entity, string action) =>
            _realtime.PublishDataChangedAsync(
                "users",
                action,
                institutionId: entity.InstitutionId,
                userId: entity.Id,
                data: new
                {
                    userId = entity.Id,
                    entity.InstitutionId,
                    entity.Email,
                    entity.FullName,
                    entity.Role,
                    entity.Status
                });

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
