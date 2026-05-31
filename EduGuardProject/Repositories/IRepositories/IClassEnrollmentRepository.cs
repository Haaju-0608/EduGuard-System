using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IClassEnrollmentRepository
{
    Task<(IEnumerable<ClassEnrollment> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? classId = null, Guid? studentId = null, EnrollmentStatus? status = null);
    Task<ClassEnrollment?> GetByKeyAsync(Guid classId, Guid studentId);
    Task AddAsync(ClassEnrollment entity);
    Task UpdateAsync(ClassEnrollment entity);
    Task SoftDeleteAsync(ClassEnrollment entity);
}
