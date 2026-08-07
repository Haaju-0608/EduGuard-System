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
        ValidateQuestion(dto.ExamSlotId, dto.QuestionType, dto.QuestionContent, dto.AudioUrl, dto.ImageUrl, dto.Points, dto.Options);

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");
        await EnsureStaffAccessAsync(examSlot.Class);
        EnsureExamQuestionsCanBeEdited(examSlot);

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
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureStaffAccessAsync(entity.ExamSlot.Class);
        EnsureExamQuestionsCanBeEdited(entity.ExamSlot);
        ValidateQuestion(entity.ExamSlotId, dto.QuestionType, dto.QuestionContent, dto.AudioUrl, dto.ImageUrl, dto.Points, entity.QuestionOptions);

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
        EnsureExamQuestionsCanBeEdited(entity.ExamSlot);
        await _repo.DeleteAsync(entity);
        return true;
    }

    public async Task<QuestionOptionResponseDto> CreateOptionAsync(Guid questionId, CreateQuestionOptionDto dto)
    {
        var question = await _repo.GetByIdAsync(questionId)
            ?? throw new InvalidOperationException("Exam question not found.");
        await EnsureStaffAccessAsync(question.ExamSlot.Class);
        EnsureExamQuestionsCanBeEdited(question.ExamSlot);
        EnsureQuestionAcceptsOptions(question);
        ValidateOption(dto);
        EnsureOptionLabelIsUnique(question.QuestionOptions, dto.OptionLabel);
        EnsureSingleCorrectOption(question.QuestionOptions, dto.IsCorrect);

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
        EnsureExamQuestionsCanBeEdited(entity.Question.ExamSlot);
        ValidateOption(dto);
        EnsureOptionLabelIsUnique(entity.Question.QuestionOptions.Where(o => o.Id != optionId), dto.OptionLabel);
        EnsureSingleCorrectOption(entity.Question.QuestionOptions.Where(o => o.Id != optionId), dto.IsCorrect);

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
        EnsureExamQuestionsCanBeEdited(entity.Question.ExamSlot);
        if (entity.IsCorrect && entity.Question.QuestionOptions.Any(o => o.Id != optionId))
            throw new InvalidOperationException("Choice questions must have exactly one correct option.");
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
            var now = DateTime.UtcNow;
            if (entity.ExamSlot.Status is ExamSlotStatus.Cancelled or ExamSlotStatus.Completed ||
                now < entity.ExamSlot.StartTime || now > entity.ExamSlot.EndTime)
            {
                throw new InvalidOperationException("Exam questions are only available during the exam slot.");
            }

            var hasParticipation = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
                p.ExamSlotId == entity.ExamSlotId &&
                p.StudentId == user.Id &&
                p.Status == ParticipationStatus.Joined);
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

    private static void ValidateQuestion(
        Guid examSlotId,
        string? questionType,
        string? questionContent,
        string? audioUrl,
        string? imageUrl,
        decimal points,
        IEnumerable<CreateQuestionOptionDto>? options)
    {
        if (examSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");
        ValidateQuestionCore(questionType, questionContent, audioUrl, imageUrl, points);
        ValidateOptions(questionType!, options);
    }

    private static void ValidateQuestion(
        Guid examSlotId,
        string? questionType,
        string? questionContent,
        string? audioUrl,
        string? imageUrl,
        decimal points,
        IEnumerable<QuestionOption> options)
    {
        if (examSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");
        ValidateQuestionCore(questionType, questionContent, audioUrl, imageUrl, points);
        ValidateOptions(questionType!, options.Select(o => new CreateQuestionOptionDto
        {
            OptionLabel = o.OptionLabel,
            OptionContent = o.OptionContent,
            IsCorrect = o.IsCorrect
        }));
    }

    private static void ValidateQuestionCore(
        string? questionType,
        string? questionContent,
        string? audioUrl,
        string? imageUrl,
        decimal points)
    {
        if (string.IsNullOrWhiteSpace(questionType))
            throw new InvalidOperationException("Question type is required.");
        if (questionType.Trim().Length > 30)
            throw new InvalidOperationException("Question type cannot exceed 30 characters.");
        if (string.IsNullOrWhiteSpace(questionContent))
            throw new InvalidOperationException("Question content is required.");
        if (points <= 0)
            throw new InvalidOperationException("Points must be greater than 0.");
        if (audioUrl?.Trim().Length > 500)
            throw new InvalidOperationException("Audio url cannot exceed 500 characters.");
        if (imageUrl?.Trim().Length > 500)
            throw new InvalidOperationException("Image url cannot exceed 500 characters.");
    }

    private static void ValidateOptions(string questionType, IEnumerable<CreateQuestionOptionDto>? options)
    {
        var optionList = (options ?? []).ToList();
        if (IsEssay(questionType))
        {
            if (optionList.Count > 0)
                throw new InvalidOperationException("Essay questions cannot have options.");
            return;
        }

        if (optionList.Count == 0)
            return;
        if (optionList.Count < 2)
            throw new InvalidOperationException("Questions with options must have at least two options.");
        if (optionList.Count(o => o.IsCorrect) != 1)
            throw new InvalidOperationException("Questions with options must have exactly one correct option.");

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in optionList)
        {
            ValidateOption(option);
            if (!labels.Add(option.OptionLabel.Trim()))
                throw new InvalidOperationException("Duplicate option labels are not allowed.");
        }
    }

    private static void ValidateOption(CreateQuestionOptionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OptionLabel))
            throw new InvalidOperationException("Option label is required.");
        if (dto.OptionLabel.Trim().Length > 10)
            throw new InvalidOperationException("Option label cannot exceed 10 characters.");
        if (string.IsNullOrWhiteSpace(dto.OptionContent))
            throw new InvalidOperationException("Option content is required.");
    }

    private static void ValidateOption(UpdateQuestionOptionDto dto) =>
        ValidateOption(new CreateQuestionOptionDto
        {
            OptionLabel = dto.OptionLabel,
            OptionContent = dto.OptionContent,
            IsCorrect = dto.IsCorrect
        });

    private static void EnsureQuestionAcceptsOptions(ExamQuestion question)
    {
        if (IsEssay(question.QuestionType))
            throw new InvalidOperationException("Essay questions cannot have options.");
    }

    private static void EnsureExamQuestionsCanBeEdited(ExamSlot examSlot)
    {
        if (examSlot.Status != ExamSlotStatus.Scheduled)
            throw new InvalidOperationException("Exam questions can only be edited while the exam slot is scheduled.");
        if (examSlot.StartTime <= DateTime.UtcNow)
            throw new InvalidOperationException("Exam questions cannot be edited after the exam has started.");
    }

    private static void EnsureOptionLabelIsUnique(IEnumerable<QuestionOption> options, string optionLabel)
    {
        var label = optionLabel.Trim();
        if (options.Any(o => string.Equals(o.OptionLabel, label, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Duplicate option labels are not allowed.");
    }

    private static void EnsureSingleCorrectOption(IEnumerable<QuestionOption> existingOptions, bool newIsCorrect)
    {
        var options = existingOptions.ToList();
        var correctCount = options.Count(o => o.IsCorrect) + (newIsCorrect ? 1 : 0);
        if (options.Count + 1 >= 2 && correctCount != 1)
            throw new InvalidOperationException("Questions with options must have exactly one correct option.");
    }

    private static bool IsEssay(string questionType) =>
        string.Equals(questionType.Trim(), "Essay", StringComparison.OrdinalIgnoreCase);

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
