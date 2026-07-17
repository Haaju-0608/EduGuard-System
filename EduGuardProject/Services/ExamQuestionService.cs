using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ExamQuestionService : IExamQuestionService
{
    private readonly IExamQuestionRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public ExamQuestionService(
        IExamQuestionRepository repo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<ExamQuestionResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, Guid? examSlotId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;
        var studentId = user.Role == AppRole.Student ? user.Id : (Guid?)null;

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, examSlotId, institutionId, lecturerId, studentId);
        var includeAnswers = user.Role != AppRole.Student;
        return (items.Select(item => MapToResponseDto(item, includeAnswers)), total);
    }

    public async Task<ExamQuestionResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        var user = await EnsureReadAccessAsync(entity);
        return MapToResponseDto(entity, includeAnswers: user.Role != AppRole.Student);
    }

    public async Task<ExamQuestionResponseDto> CreateAsync(CreateExamQuestionDto dto)
    {
        if (dto.Points < 0)
            throw new InvalidOperationException("Points must be greater than or equal to 0.");

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");
        await EnsureStaffAccessAsync(examSlot.Class);

        var entity = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            QuestionType = dto.QuestionType.Trim(),
            QuestionContent = dto.QuestionContent.Trim(),
            AudioUrl = string.IsNullOrWhiteSpace(dto.AudioUrl) ? null : dto.AudioUrl.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim(),
            Points = dto.Points,
            DisplayOrder = dto.DisplayOrder,
            CreatedAt = DateTime.UtcNow,
            QuestionOptions = (dto.Options ?? [])
                .Select(MapOption)
                .ToList()
        };

        await _repo.AddAsync(entity);
        entity.ExamSlot = examSlot;
        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<ExamQuestionResponseDto?> UpdateAsync(Guid id, UpdateExamQuestionDto dto)
    {
        if (dto.Points < 0)
            throw new InvalidOperationException("Points must be greater than or equal to 0.");

        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureStaffAccessAsync(entity.ExamSlot.Class);

        entity.QuestionType = dto.QuestionType.Trim();
        entity.QuestionContent = dto.QuestionContent.Trim();
        entity.AudioUrl = string.IsNullOrWhiteSpace(dto.AudioUrl) ? null : dto.AudioUrl.Trim();
        entity.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        entity.Points = dto.Points;
        entity.DisplayOrder = dto.DisplayOrder;

        await _repo.UpdateAsync(entity);
        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;
        await EnsureStaffAccessAsync(entity.ExamSlot.Class);
        await _repo.DeleteAsync(entity);
        return true;
    }

    public async Task<QuestionOptionResponseDto> CreateOptionAsync(Guid questionId, CreateQuestionOptionDto dto)
    {
        var question = await _repo.GetByIdAsync(questionId)
            ?? throw new InvalidOperationException("Exam question not found.");
        await EnsureStaffAccessAsync(question.ExamSlot.Class);

        var entity = MapOption(dto);
        entity.QuestionId = questionId;
        await _repo.AddOptionAsync(entity);
        return MapToOptionResponseDto(entity, includeAnswer: true);
    }

    public async Task<QuestionOptionResponseDto?> UpdateOptionAsync(Guid optionId, UpdateQuestionOptionDto dto)
    {
        var entity = await _repo.GetOptionByIdAsync(optionId);
        if (entity == null) return null;
        await EnsureStaffAccessAsync(entity.Question.ExamSlot.Class);

        entity.OptionLabel = dto.OptionLabel.Trim();
        entity.OptionContent = dto.OptionContent.Trim();
        entity.IsCorrect = dto.IsCorrect;
        await _repo.UpdateOptionAsync(entity);
        return MapToOptionResponseDto(entity, includeAnswer: true);
    }

    public async Task<bool> DeleteOptionAsync(Guid optionId)
    {
        var entity = await _repo.GetOptionByIdAsync(optionId);
        if (entity == null) return false;
        await EnsureStaffAccessAsync(entity.Question.ExamSlot.Class);
        await _repo.DeleteOptionAsync(entity);
        return true;
    }

    private async Task<User> EnsureReadAccessAsync(ExamQuestion entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (CanAccessAsStaff(user, entity.ExamSlot.Class))
            return user;

        if (user.Role == AppRole.Student)
        {
            var hasParticipation = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
                p.ExamSlotId == entity.ExamSlotId &&
                p.StudentId == user.Id);
            if (hasParticipation)
                return user;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureStaffAccessAsync(Class cls)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (CanAccessAsStaff(user, cls))
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private static bool CanAccessAsStaff(User user, Class cls)
    {
        if (user.Role == AppRole.SuperAdmin)
            return true;

        if (user.InstitutionId != cls.InstitutionId)
            return false;

        return user.Role == AppRole.SchoolAdmin ||
               (user.Role == AppRole.Lecturer && cls.LecturerId == user.Id);
    }

    private static ExamQuestionResponseDto MapToResponseDto(ExamQuestion entity, bool includeAnswers) => new()
    {
        Id = entity.Id,
        ExamSlotId = entity.ExamSlotId,
        ExamName = entity.ExamSlot?.ExamName,
        QuestionType = entity.QuestionType,
        QuestionContent = entity.QuestionContent,
        AudioUrl = entity.AudioUrl,
        ImageUrl = entity.ImageUrl,
        Points = entity.Points,
        DisplayOrder = entity.DisplayOrder,
        CreatedAt = entity.CreatedAt,
        Options = entity.QuestionOptions
            .OrderBy(o => o.OptionLabel)
            .Select(option => MapToOptionResponseDto(option, includeAnswers))
            .ToList()
    };

    private static QuestionOption MapOption(CreateQuestionOptionDto dto) => new()
    {
        Id = Guid.NewGuid(),
        OptionLabel = dto.OptionLabel.Trim(),
        OptionContent = dto.OptionContent.Trim(),
        IsCorrect = dto.IsCorrect
    };

    private static QuestionOptionResponseDto MapToOptionResponseDto(QuestionOption entity, bool includeAnswer) => new()
    {
        Id = entity.Id,
        QuestionId = entity.QuestionId,
        OptionLabel = entity.OptionLabel,
        OptionContent = entity.OptionContent,
        IsCorrect = includeAnswer ? entity.IsCorrect : null
    };
}
