using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ExamQuestionRepository : IExamQuestionRepository
{
    private readonly AppDbContext _context;

    public ExamQuestionRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<ExamQuestion> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? institutionId = null, Guid? studentId = null)
    {
        var query = BaseQuery().AsNoTracking();

        if (examSlotId.HasValue)
            query = query.Where(q => _context.ExamSlots.Any(slot =>
                slot.Id == examSlotId.Value &&
                slot.Class.InstitutionId == q.InstitutionId &&
                slot.ExamQuestionName.ToLower() == q.ExamQuestionName.ToLower()));
        if (institutionId.HasValue)
            query = query.Where(q => q.InstitutionId == institutionId.Value);
        if (studentId.HasValue)
        {
            var now = DateTime.UtcNow;
            query = query.Where(q => _context.ExamSlots.Any(slot =>
                slot.Class.InstitutionId == q.InstitutionId &&
                slot.ExamQuestionName.ToLower() == q.ExamQuestionName.ToLower() &&
                (!examSlotId.HasValue || slot.Id == examSlotId.Value) &&
                slot.Status != ExamSlotStatus.Cancelled &&
                slot.Status != ExamSlotStatus.Completed &&
                slot.StartTime <= now &&
                slot.EndTime >= now &&
                slot.ExamParticipations.Any(p =>
                    p.StudentId == studentId.Value &&
                    p.Status == ParticipationStatus.Joined)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(q =>
                q.QuestionContent.ToLower().Contains(s) ||
                q.QuestionType.ToLower().Contains(s) ||
                q.ExamQuestionName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var items = await ApplySort(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<ExamQuestion?> GetByIdAsync(Guid id) =>
        BaseQuery().FirstOrDefaultAsync(q => q.Id == id);

    public Task<QuestionOption?> GetOptionByIdAsync(Guid id) =>
        _context.QuestionOptions
            .Include(o => o.Question)
            .ThenInclude(q => q.QuestionOptions)
            .Include(o => o.Question)
            .ThenInclude(q => q.Institution)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task AddAsync(ExamQuestion entity)
    {
        await _context.ExamQuestions.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<ExamQuestion> entities, CancellationToken cancellationToken = default)
    {
        await _context.ExamQuestions.AddRangeAsync(entities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExamQuestion entity)
    {
        _context.ExamQuestions.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ExamQuestion entity)
    {
        _context.ExamQuestions.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task AddOptionAsync(QuestionOption entity)
    {
        await _context.QuestionOptions.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateOptionAsync(QuestionOption entity)
    {
        _context.QuestionOptions.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteOptionAsync(QuestionOption entity)
    {
        _context.QuestionOptions.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private IQueryable<ExamQuestion> BaseQuery() =>
        _context.ExamQuestions
            .Include(q => q.QuestionOptions)
            .Include(q => q.Passage)
            .Include(q => q.Institution);

    private static IQueryable<ExamQuestion> ApplySort(IQueryable<ExamQuestion> query, string? sort) =>
        (sort ?? "displayorder").ToLower() switch
        {
            "createdat" => query.OrderBy(q => q.CreatedAt),
            "-createdat" => query.OrderByDescending(q => q.CreatedAt),
            "points" => query.OrderBy(q => q.Points),
            "-points" => query.OrderByDescending(q => q.Points),
            "type" => query.OrderBy(q => q.QuestionType),
            "-type" => query.OrderByDescending(q => q.QuestionType),
            "-displayorder" => query.OrderByDescending(q => q.DisplayOrder),
            _ => query.OrderBy(q => q.DisplayOrder).ThenBy(q => q.CreatedAt)
        };
}

