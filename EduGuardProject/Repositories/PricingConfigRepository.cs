using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories
{
    public class PricingConfigRepository : IPricingConfigRepository
    {
        private readonly AppDbContext _context;

        public PricingConfigRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PricingConfig>> GetAllAsync()
        {
            return await _context.PricingConfigs
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<PricingConfig?> GetByIdAsync(Guid id)
        {
            return await _context.PricingConfigs.FindAsync(id);
        }

        // Hàm cực kỳ quan trọng: Lấy cấu hình giá đang có hiệu lực (Active) của một loại dịch vụ để tính tiền
        public async Task<PricingConfig?> GetActiveConfigByServiceTypeAsync(PricingServiceType serviceType)
        {
            return await _context.PricingConfigs
                .Where(p => p.ServiceType == serviceType && p.IsActive == true)
                .OrderByDescending(p => p.EffectiveDate)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(PricingConfig config)
        {
            await _context.PricingConfigs.AddAsync(config);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PricingConfig config)
        {
            _context.PricingConfigs.Update(config);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasReferencingTransactionsAsync(Guid pricingConfigId)
        {
            return await _context.Transactions.AnyAsync(t => t.PricingConfigId == pricingConfigId);
        }
    }
}
