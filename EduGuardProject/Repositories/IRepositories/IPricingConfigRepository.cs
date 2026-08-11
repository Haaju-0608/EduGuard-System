using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IPricingConfigRepository
    {
        Task<IEnumerable<PricingConfig>> GetAllAsync();
        Task<PricingConfig?> GetByIdAsync(Guid id);
        Task<PricingConfig?> GetActiveConfigByServiceTypeAsync(PricingServiceType serviceType);
        Task AddAsync(PricingConfig config);
        Task UpdateAsync(PricingConfig config);

        Task<bool> HasReferencingTransactionsAsync(Guid pricingConfigId);
    }
}
