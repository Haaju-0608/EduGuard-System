using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices
{
    public interface IPricingConfigService
    {
        Task<IEnumerable<PricingConfigResponseDto>> GetAllConfigsAsync();
        Task<PricingConfigResponseDto?> GetConfigByIdAsync(Guid id);
        Task<PricingConfigResponseDto?> GetCurrentActiveConfigAsync(PricingServiceType serviceType);
        Task<PricingConfigResponseDto> CreateConfigAsync(CreatePricingConfigDto dto, Guid adminId);

        Task<bool> UpdateConfigAsync(Guid id, UpdatePricingConfigDto dto);   

    }
}
