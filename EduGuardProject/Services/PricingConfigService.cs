using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services
{
    public class PricingConfigService : IPricingConfigService
    {
        private readonly IPricingConfigRepository _repo;

        public PricingConfigService(IPricingConfigRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<PricingConfigResponseDto>> GetAllConfigsAsync()
        {
            var configs = await _repo.GetAllAsync();
            return configs.Select(MapToDto);
        }

        public async Task<PricingConfigResponseDto?> GetConfigByIdAsync(Guid id)
        {
            var config = await _repo.GetByIdAsync(id);
            return config == null ? null : MapToDto(config);
        }

        public async Task<PricingConfigResponseDto?> GetCurrentActiveConfigAsync(PricingServiceType serviceType)
        {
            var config = await _repo.GetActiveConfigByServiceTypeAsync(serviceType);
            return config == null ? null : MapToDto(config);
        }

        public async Task<PricingConfigResponseDto> CreateConfigAsync(CreatePricingConfigDto dto, Guid adminId)
        {
            // LOGIC: Tắt cấu hình giá đang hoạt động cũ của dịch vụ này đi
            var currentActive = await _repo.GetActiveConfigByServiceTypeAsync(dto.ServiceType);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                currentActive.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(currentActive);
            }

            // Tạo cấu hình giá mới
            var newConfig = new PricingConfig
            {
                Id = Guid.NewGuid(),
                ServiceType = dto.ServiceType,
                UnitPrice = dto.UnitPrice,
                EffectiveDate = dto.EffectiveDate.ToUniversalTime(),
                IsActive = true,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(newConfig);
            return MapToDto(newConfig);
        }

        private static PricingConfigResponseDto MapToDto(PricingConfig p) => new()
        {
            Id = p.Id,
            ServiceType = p.ServiceType,
            UnitPrice = p.UnitPrice,
            EffectiveDate = p.EffectiveDate,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }
}
