using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IClassRepository
{
    Task<(IEnumerable<Class> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, Guid? institutionId = null, Guid? lecturerId = null);
    Task<Class?> GetByIdAsync(Guid id, bool includeDeleted = false);
    Task AddAsync(Class entity);
    Task UpdateAsync(Class entity);
    Task SoftDeleteAsync(Class entity);
}
