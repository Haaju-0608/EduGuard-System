using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IViolationLogRepository
    {
        Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
       string? search, string? sort, int page, int pageSize,
       Guid? participationId = null, bool? isReviewed = null,
       Guid? institutionId = null, Guid? lecturerId = null, Guid? studentId = null, Guid? examSlotId = null);

        Task<ViolationLog?> GetByIdAsync(Guid id);

        Task CreateAsync(ViolationLog entity);
        Task UpdateAsync(Guid id, UpdateViolationLogDto dto);
        Task DeleteAsync(Guid id);
    }
}
