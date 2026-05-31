using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IBiometricRequestRepository
{
    Task<(IEnumerable<BiometricRequest> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? studentId = null, BiometricReqStatus? status = null);
    Task<BiometricRequest?> GetByIdAsync(Guid id);
    Task AddAsync(BiometricRequest entity);
    Task UpdateAsync(BiometricRequest entity);
    Task SoftDeleteAsync(BiometricRequest entity);
}
