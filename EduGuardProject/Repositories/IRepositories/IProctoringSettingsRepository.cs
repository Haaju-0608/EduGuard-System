using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IProctoringSettingsRepository
    {
        Task<IEnumerable<ProctoringSettings>> GetAllAsync();
        Task<ProctoringSettings?> GetByIdAsync(Guid id);

        /// <summary>Active config for the given institution, or null if none configured yet.</summary>
        Task<ProctoringSettings?> GetActiveByInstitutionAsync(Guid? institutionId);

        Task AddAsync(ProctoringSettings config);
        Task UpdateAsync(ProctoringSettings config);
        Task ReplaceViolationTypeThresholdsAsync(Guid proctoringSettingsId, IEnumerable<ProctoringViolationTypeSetting> thresholds);
    }
}
