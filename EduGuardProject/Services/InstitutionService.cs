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
        public InstitutionService(IInstitutionRepository repo) => _repo = repo;

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
            var entity = new Institution
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                SubDomain = dto.SubDomain,
                ContactEmail = dto.ContactEmail,
                BillingModel = dto.BillingModel,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(entity);
            return MapToResponseDto(entity);
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
            return true;
        }

        public async Task<bool> DeleteInstitutionAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await _repo.DeleteAsync(entity);
            return true;
        }

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
