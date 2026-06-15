using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IBiometricDatumRepository
{
    Task<(IEnumerable<BiometricDatum> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? userId = null, bool? isActive = null);
    Task<BiometricDatum?> GetByIdAsync(Guid id);
    Task AddAsync(BiometricDatum entity);
    Task UpdateAsync(BiometricDatum entity);
    Task SoftDeleteAsync(BiometricDatum entity);

    Task<BiometricDatum?> FindClosestMatchAsync(Pgvector.Vector currentFaceVector, double threshold);
}
