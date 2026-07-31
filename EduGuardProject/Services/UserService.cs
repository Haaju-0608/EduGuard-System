using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace EduGuardProject.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly Supabase.Client _supabaseClient;
        private readonly IConfiguration _config;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly IStorageService _storage;
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;

        //  3. Tiêm IConfiguration vào để lấy Key tự động
        public UserService(
            IUserRepository repo,
            Supabase.Client supabaseClient,
            IConfiguration config,
            IRealtimeEventDispatcher realtime,
            IStorageService storage,
            AppDbContext context,
            IDistributedCache cache)
        {
            _repo = repo;
            _supabaseClient = supabaseClient;
            _config = config;
            _realtime = realtime;
            _storage = storage;
            _context = context;
            _cache = cache;
        }

        public async Task<(IEnumerable<UserResponseDto> Items, int TotalCount)> GetUsersAsync(
    Guid? institutionId, AppRole? excludeRole, string? search, string? sort, int page, int pageSize)
        {
            var (entities, totalCount) = await _repo.GetAllAsync(institutionId, excludeRole, search, sort, page, pageSize);
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
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));
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
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));
            await PublishUserChangedAsync(entity, "profile-updated");
            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await DeleteStudentStorageAsync(entity);
            await _repo.DeleteAsync(entity);
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));

            var serviceKey = _config["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);
            await adminAuth.DeleteUser(id.ToString());

            await PublishUserChangedAsync(entity, "deleted");
            return true;
        }

        private async Task DeleteStudentStorageAsync(EduGuardProject.Models.User user)
        {
            if (user.Role != AppRole.Student)
                return;

            var storageDeletes = new List<(string Bucket, string? Path)>();

            storageDeletes.AddRange(await _context.BiometricData
                .Where(b => b.UserId == user.Id && b.FaceImageUrl != null)
                .Select(b => new ValueTuple<string, string?>(StorageService.BiometricFacesBucket, b.FaceImageUrl))
                .ToListAsync());

            storageDeletes.AddRange(await _context.AttendanceRecords
                .Where(r => r.StudentId == user.Id && r.SnapshotPath != null)
                .Select(r => new ValueTuple<string, string?>(StorageService.AttendanceSnapshotsBucket, r.SnapshotPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ExamParticipations
                .Where(p => p.StudentId == user.Id && p.IdentitySnapshotPath != null)
                .Select(p => new ValueTuple<string, string?>(StorageService.ExamIdentityBucket, p.IdentitySnapshotPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ExamParticipations
                .Where(p => p.StudentId == user.Id && p.RecordingVideoPath != null)
                .Select(p => new ValueTuple<string, string?>(StorageService.ExamRecordingsBucket, p.RecordingVideoPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ViolationLogs
                .Where(v => v.Participation.StudentId == user.Id && v.EvidencePath != null)
                .Select(v => new ValueTuple<string, string?>(StorageService.ExamEvidenceBucket, v.EvidencePath))
                .ToListAsync());

            foreach (var (bucket, path) in storageDeletes
                .Where(x => !string.IsNullOrWhiteSpace(x.Path))
                .Distinct())
            {
                await _storage.DeleteAsync(bucket, path!);
            }
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
