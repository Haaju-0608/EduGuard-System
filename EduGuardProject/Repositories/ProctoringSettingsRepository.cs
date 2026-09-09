using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories
{
    public class ProctoringSettingsRepository : IProctoringSettingsRepository
    {
        private readonly AppDbContext _context;

        public ProctoringSettingsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProctoringSettings>> GetAllAsync()
        {
            return await _context.ProctoringSettings
                .Include(s => s.ViolationTypeSettings)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProctoringSettings?> GetByIdAsync(Guid id)
        {
            return await _context.ProctoringSettings
                .Include(s => s.ViolationTypeSettings)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ProctoringSettings?> GetActiveByInstitutionAsync(Guid? institutionId)
        {
            return await _context.ProctoringSettings
                .Include(s => s.ViolationTypeSettings)
                .Where(s => s.InstitutionId == institutionId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(ProctoringSettings config)
        {
            await _context.ProctoringSettings.AddAsync(config);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ProctoringSettings config)
        {
            _context.ProctoringSettings.Update(config);
            await _context.SaveChangesAsync();
        }

        public async Task ReplaceViolationTypeThresholdsAsync(Guid proctoringSettingsId, IEnumerable<ProctoringViolationTypeSetting> thresholds)
        {
            var existing = await _context.ProctoringViolationTypeSettings
                .Where(t => t.ProctoringSettingsId == proctoringSettingsId)
                .ToListAsync();
            _context.ProctoringViolationTypeSettings.RemoveRange(existing);

            foreach (var threshold in thresholds)
                threshold.ProctoringSettingsId = proctoringSettingsId;

            await _context.ProctoringViolationTypeSettings.AddRangeAsync(thresholds);
            await _context.SaveChangesAsync();
        }
    }
}
