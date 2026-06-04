using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IViolationLogRepository
    {
        Task<(IEnumerable<ViolationLog> Items, int TotalCount)> GetAllAsync(
       string? search, string? sort, int page, int pageSize,
       Guid? participationId = null);

        Task<ViolationLog?> GetByIdAsync(Guid id);

        Task CreateAsync(ViolationLog entity);
        Task <bool> UpdateAsync(Guid id, ViolationLog entity);
        Task <bool> DeleteAsync(ViolationLog entity);
    }
}
}
