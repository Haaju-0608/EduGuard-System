using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services
{
    public class InstitutionService : IInstitutionService
    {
        private readonly IInstitutionRepository _repo;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly ICurrentUserService _currentUser;   

        public InstitutionService(IInstitutionRepository repo, IRealtimeEventDispatcher realtime, ICurrentUserService currentUser)
        {
            _repo = repo;
            _realtime = realtime;
            _currentUser = currentUser;
        }

        public async Task<(IEnumerable<InstitutionResponseDto> Items, int TotalCount)> GetInstitutionsAsync(string? search, string? sort, int page, int pageSize)
        {
            var (entities, totalCount) = await _repo.GetAllAsync(search, sort, page, pageSize);

            // Map Entity sang Response DTO
            var dtos = entities.Select(e => MapToResponseDto(e));
            return (dtos, totalCount);
        }

        public async Task<InstitutionResponseDto?> GetInstitutionByIdAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<InstitutionResponseDto> CreateInstitutionAsync(CreateInstitutionDto dto)
        {
            var now = DateTime.UtcNow;
            var entity = new Institution
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                SubDomain = dto.SubDomain,
                ContactEmail = dto.ContactEmail,
                BillingModel = dto.BillingModel,
                Status = dto.Status,
                // THÊM MỚI: tự set hạn ban đầu theo đúng chu kỳ đã chọn
                SubscriptionExpiresAt = dto.BillingModel == BillingModel.Monthly
                    ? now.AddMonths(1)
                    : now.AddYears(1),
                CreatedAt = now,
                UpdatedAt = now
            };
            await _repo.AddAsync(entity);
            await PublishInstitutionChangedAsync(entity, "created");
            return MapToResponseDto(entity);
        }

        // THÊM MỚI: gia hạn thêm 1 chu kỳ kể từ hạn cũ (nếu còn hạn) hoặc từ hiện tại (nếu đã hết hạn)
        public async Task<bool> RenewSubscriptionAsync(Guid institutionId)
        {
            var entity = await _repo.GetByIdAsync(institutionId);
            if (entity == null) return false;

            var user = await _currentUser.GetRequiredUserAsync();
            if (user.Role != AppRole.SuperAdmin &&
                (!user.InstitutionId.HasValue || user.InstitutionId.Value != institutionId))
            {
                throw new UnauthorizedAccessException("You do not have permission to do this");
            }

            var now = DateTime.UtcNow;
            // Nếu vẫn còn hạn, cộng thêm từ ngày hết hạn cũ (không mất phần thời gian còn lại)
            // Nếu đã hết hạn rồi, tính từ thời điểm hiện tại
            var baseDate = (entity.SubscriptionExpiresAt.HasValue && entity.SubscriptionExpiresAt.Value > now)
                ? entity.SubscriptionExpiresAt.Value
                : now;

            entity.SubscriptionExpiresAt = entity.BillingModel == BillingModel.Monthly
                ? baseDate.AddMonths(1)
                : baseDate.AddYears(1);

            // Nếu trước đó bị khoá do hết hạn, tự mở khoá lại
            if (entity.Status == InstitutionStatus.Suspended)
                entity.Status = InstitutionStatus.Active;

            entity.UpdatedAt = now;
            await _repo.UpdateAsync(entity);
            await PublishInstitutionChangedAsync(entity, "renewed");
            return true;
        }

        public async Task<bool> UpdateInstitutionAsync(Guid id, UpdateInstitutionDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            entity.Name = dto.Name;
            entity.SubDomain = dto.SubDomain;
            entity.ContactEmail = dto.ContactEmail;
            entity.BillingModel = dto.BillingModel;
            entity.Status = dto.Status;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await PublishInstitutionChangedAsync(entity, "updated");
            return true;
        }

        public async Task<bool> DeleteInstitutionAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await _repo.DeleteAsync(entity);
            await PublishInstitutionChangedAsync(entity, "deleted");
            return true;
        }

        private Task PublishInstitutionChangedAsync(Institution entity, string action) =>
            _realtime.PublishDataChangedAsync(
                "institutions",
                action,
                institutionId: entity.Id,
                data: new
                {
                    institutionId = entity.Id,
                    entity.Name,
                    entity.SubDomain,
                    entity.ContactEmail,
                    entity.BillingModel,
                    entity.Status
                });

        // Hàm phụ để Map nhanh dữ liệu đỡ phải viết đi viết lại nhiều lần
        private static InstitutionResponseDto MapToResponseDto(Institution e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            SubDomain = e.SubDomain,
            ContactEmail = e.ContactEmail,
            SubscriptionExpiresAt = e.SubscriptionExpiresAt,
            BillingModel = e.BillingModel,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
