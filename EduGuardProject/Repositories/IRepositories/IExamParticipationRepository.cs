using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories
{
    public interface IExamParticipationRepository
    {
        Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize);

        Task<ExamParticipation?> GetByIdAsync(Guid examSlotId);
        Task<ExamParticipation?> GetByExamSlotAndStudentAsync(Guid examSlotId, Guid studentId);

        Task AddAsync(ExamParticipation entity);
        Task AddRangeAsync(IEnumerable<ExamParticipation> entities);
        Task UpdateAsync(ExamParticipation entity);
        Task DeleteAsync(ExamParticipation entity);
    }
}
