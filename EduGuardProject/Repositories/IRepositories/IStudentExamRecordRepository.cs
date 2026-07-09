using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IStudentExamRecordRepository
{
    Task<(IEnumerable<StudentExamRecord> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? studentId = null, StudentExamRecordStatus? status = null,
        Guid? institutionId = null, Guid? lecturerId = null);

    Task<StudentExamRecord?> GetByIdAsync(Guid id);
    Task AddAsync(StudentExamRecord entity);
    Task UpdateAsync(StudentExamRecord entity);
}
