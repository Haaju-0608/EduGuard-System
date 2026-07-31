using System.Text.Json;
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.Extensions.Caching.Distributed;

namespace EduGuardProject.Services
{
    public class PricingConfigService : IPricingConfigService
    {
        private static readonly TimeSpan ActiveConfigCacheTtl = TimeSpan.FromMinutes(30);

        private readonly IPricingConfigRepository _repo;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly IDistributedCache _cache;

        public PricingConfigService(IPricingConfigRepository repo, IRealtimeEventDispatcher realtime, IDistributedCache cache)
        {
            _repo = repo;
            _realtime = realtime;
            _cache = cache;
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
            var cacheKey = ActiveConfigCacheKey(serviceType);
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<PricingConfigResponseDto>(cached);

            var config = await _repo.GetActiveConfigByServiceTypeAsync(serviceType);
            var dto = config == null ? null : MapToDto(config);

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ActiveConfigCacheTtl });

            return dto;
        }

        private static string ActiveConfigCacheKey(PricingServiceType serviceType) => $"pricing-config:active:{serviceType}";

        public async Task<PricingConfigResponseDto> CreateConfigAsync(CreatePricingConfigDto dto, Guid adminId)
        {
            if (dto.UnitPrice <= 0)
                throw new InvalidOperationException("Đơn giá phải lớn hơn 0.");

            // LOGIC: Tắt cấu hình giá đang hoạt động cũ của dịch vụ này đi
            var currentActive = await _repo.GetActiveConfigByServiceTypeAsync(dto.ServiceType);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                currentActive.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(currentActive);
                await PublishPricingChangedAsync(currentActive, "deactivated");
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
            await _cache.RemoveAsync(ActiveConfigCacheKey(dto.ServiceType));
            await PublishPricingChangedAsync(newConfig, "created");
            return MapToDto(newConfig);
        }

        private Task PublishPricingChangedAsync(PricingConfig config, string action) =>
            _realtime.PublishDataChangedAsync(
                "pricing-configs",
                action,
                data: new
                {
                    pricingConfigId = config.Id,
                    config.ServiceType,
                    config.UnitPrice,
                    config.EffectiveDate,
                    config.IsActive
                });

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
