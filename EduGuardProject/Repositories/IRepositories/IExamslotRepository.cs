using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IExamslotRepository
{
    Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize);

    Task<ExamSlot?> GetByIdAsync(Guid ExamId);

    Task AddAsync(ExamSlot entity);
    Task UpdateAsync(ExamSlot entity);
}
