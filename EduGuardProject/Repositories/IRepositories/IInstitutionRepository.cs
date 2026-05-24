using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IInstitutionRepository
    {
        Task<(IEnumerable<Institution> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize);
        Task<Institution?> GetByIdAsync(Guid id);
        Task AddAsync(Institution institution);
        Task UpdateAsync(Institution institution);
        Task DeleteAsync(Institution institution);
    }
}
