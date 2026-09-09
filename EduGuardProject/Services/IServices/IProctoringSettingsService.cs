using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices
{
    public interface IProctoringSettingsService
    {
        Task<IEnumerable<ProctoringSettingsResponseDto>> GetAllAsync();
        Task<ProctoringSettingsResponseDto?> GetByIdAsync(Guid id);

        /// <summary>
        /// Config that actually applies for an institution: its own active override,
        /// falling back to the system-wide default (institutionId == null) if it has none,
        /// falling back to hard-coded defaults if even the system-wide default is missing.
        /// Used internally by violation-recording services — no access check.
        /// </summary>
        Task<ProctoringSettingsResponseDto> GetEffectiveAsync(Guid? institutionId);

        Task<ProctoringSettingsResponseDto> CreateAsync(CreateProctoringSettingsDto dto, Guid adminId, AppRole role, Guid? actorInstitutionId);
        Task<bool> UpdateAsync(Guid id, UpdateProctoringSettingsDto dto, Guid adminId, AppRole role, Guid? actorInstitutionId);
    }
}
