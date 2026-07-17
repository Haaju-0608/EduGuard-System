using EduGuardProject.Models;

namespace EduGuardProject.Repositories.IRepositories;

public interface IExamQuestionRepository
{
    Task<(IEnumerable<ExamQuestion> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? institutionId = null, Guid? lecturerId = null, Guid? studentId = null);

    Task<ExamQuestion?> GetByIdAsync(Guid id);
    Task<QuestionOption?> GetOptionByIdAsync(Guid id);
    Task AddAsync(ExamQuestion entity);
    Task UpdateAsync(ExamQuestion entity);
    Task DeleteAsync(ExamQuestion entity);
    Task AddOptionAsync(QuestionOption entity);
    Task UpdateOptionAsync(QuestionOption entity);
    Task DeleteOptionAsync(QuestionOption entity);
}

