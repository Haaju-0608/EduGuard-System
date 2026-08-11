using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IExamslotRepository
{
    Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? institutionId = null, Guid? lecturerId = null, Guid? studentId = null);

    Task<ExamSlot?> GetByIdAsync(Guid ExamId);

    Task AddAsync(ExamSlot entity);
    Task UpdateAsync(ExamSlot entity);
}
