using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using ClosedXML.Excel;
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
        var role = user.Role.ToCanonical();
        var institutionId = role is AppRole.SchoolAdmin or AppRole.Lecturer
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("User is not assigned to an institution.")
            : (Guid?)null;
        var studentId = role == AppRole.Student ? user.Id : (Guid?)null;

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, examSlotId, institutionId, studentId);
        var includeAnswers = role != AppRole.Student;
        return (items.Select(item => AcademicMapper.ToExamQuestionResponseDto(item, includeAnswers)), total);
    }

    public async Task<ExamQuestionResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        var user = await EnsureReadAccessAsync(entity);
        return AcademicMapper.ToExamQuestionResponseDto(entity, includeAnswers: user.Role.ToCanonical() != AppRole.Student);
    }

    public async Task<ExamQuestionResponseDto> CreateAsync(CreateExamQuestionDto dto)
    {
        ValidateQuestion(dto.QuestionType, dto.QuestionContent, dto.AudioUrl, dto.ImageUrl, dto.Points, dto.Options);
        ValidateExamQuestionName(dto.ExamQuestionName);
        if (dto.DisplayOrder <= 0)
            throw new InvalidOperationException("Display order must be greater than zero.");
        await EnsureQuestionBankAccessAsync(dto.InstitutionId);
        await EnsureQuestionSetCanBeEditedAsync(dto.InstitutionId, dto.ExamQuestionName);
        if (await _context.ExamQuestions.AsNoTracking().AnyAsync(question =>
                question.InstitutionId == dto.InstitutionId &&
                question.ExamQuestionName.ToLower() == dto.ExamQuestionName.Trim().ToLower() &&
                question.DisplayOrder == dto.DisplayOrder))
        {
            throw new InvalidOperationException($"Display order {dto.DisplayOrder} already exists in this question set.");
        }

        var entity = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            InstitutionId = dto.InstitutionId,
            ExamQuestionName = dto.ExamQuestionName.Trim(),
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
        return AcademicMapper.ToExamQuestionResponseDto(entity, includeAnswers: true);
    }

    public async Task<ImportExamQuestionsResponseDto> ImportFromExcelAsync(
        Guid institutionId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty)
            throw new InvalidOperationException("Institution id is required.");
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Excel file is required.");
        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Excel file must not exceed 5 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .xlsx files are supported.");
        var examQuestionName = GetImportedExamQuestionName(file.FileName);
        await EnsureQuestionBankAccessAsync(institutionId);
        await EnsureQuestionSetCanBeEditedAsync(institutionId, examQuestionName, cancellationToken);

        await using var stream = file.OpenReadStream();
        var rows = ReadExcelQuestions(stream);
        if (rows.Count == 0)
            throw new InvalidOperationException("The Excel file does not contain any question rows.");
        if (rows.Count > 500)
            throw new InvalidOperationException("A single import is limited to 500 questions.");

        var importedOrders = rows.Select(row => row.Stt).ToList();
        var duplicatedOrder = await _context.ExamQuestions.AsNoTracking()
            .Where(question =>
                question.InstitutionId == institutionId &&
                question.ExamQuestionName.ToLower() == examQuestionName.ToLower() &&
                importedOrders.Contains(question.DisplayOrder))
            .Select(question => (int?)question.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicatedOrder.HasValue)
            throw new InvalidOperationException($"Display order {duplicatedOrder.Value} already exists in this question set.");

        var questions = rows.Select(row => MapImportedQuestion(row, institutionId, examQuestionName)).ToList();
        await _repo.AddRangeAsync(questions, cancellationToken);
        return new ImportExamQuestionsResponseDto
        {
            InstitutionId = institutionId,
            ExamQuestionName = examQuestionName,
            ImportedCount = questions.Count
        };
    }

    public async Task<ExamQuestionResponseDto?> UpdateAsync(Guid id, UpdateExamQuestionDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureQuestionWriteAccessAsync(entity);
        ValidateQuestion(dto.QuestionType, dto.QuestionContent, dto.AudioUrl, dto.ImageUrl, dto.Points, entity.QuestionOptions);
        var passage = await GetPassageAsync(dto.PassageId, entity);

        entity.PassageId = dto.PassageId;
        entity.Passage = passage;
        entity.QuestionType = dto.QuestionType.Trim();
        entity.QuestionContent = dto.QuestionContent.Trim();
        entity.AudioUrl = string.IsNullOrWhiteSpace(dto.AudioUrl) ? null : dto.AudioUrl.Trim();
        entity.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        entity.Points = dto.Points;
        entity.DisplayOrder = dto.DisplayOrder;

        await _repo.UpdateAsync(entity);
        return AcademicMapper.ToExamQuestionResponseDto(entity, includeAnswers: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;
        await EnsureQuestionWriteAccessAsync(entity);
        await _repo.DeleteAsync(entity);
        return true;
    }

    public async Task<QuestionOptionResponseDto> CreateOptionAsync(Guid questionId, CreateQuestionOptionDto dto)
    {
        var question = await _repo.GetByIdAsync(questionId)
            ?? throw new InvalidOperationException("Exam question not found.");
        await EnsureQuestionWriteAccessAsync(question);
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
        await EnsureQuestionWriteAccessAsync(entity.Question);
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
        await EnsureQuestionWriteAccessAsync(entity.Question);
        if (entity.IsCorrect && entity.Question.QuestionOptions.Any(o => o.Id != optionId))
            throw new InvalidOperationException("Choice questions must have exactly one correct option.");
        await _repo.DeleteOptionAsync(entity);
        return true;
    }

    private async Task<User> EnsureReadAccessAsync(ExamQuestion entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (CanAccessAsStaff(user, entity))
            return user;

        if (user.Role.ToCanonical() == AppRole.Student)
        {
            var now = DateTime.UtcNow;
            var normalizedName = entity.ExamQuestionName.ToLower();
            var hasParticipation = await _context.ExamSlots.AsNoTracking().AnyAsync(slot =>
                slot.Class.InstitutionId == entity.InstitutionId &&
                slot.ExamQuestionName.ToLower() == normalizedName &&
                slot.Status != ExamSlotStatus.Cancelled &&
                slot.Status != ExamSlotStatus.Completed &&
                slot.StartTime <= now &&
                slot.EndTime >= now &&
                slot.ExamParticipations.Any(participation =>
                    participation.StudentId == user.Id &&
                    participation.Status == ParticipationStatus.Joined));
            if (hasParticipation)
                return user;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureQuestionWriteAccessAsync(ExamQuestion question)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (!CanAccessAsStaff(user, question))
            throw new UnauthorizedAccessException("Access denied.");
        await EnsureQuestionSetCanBeEditedAsync(question.InstitutionId, question.ExamQuestionName);
    }

    private static bool CanAccessAsStaff(User user, ExamQuestion question)
    {
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin)
            return true;

        if (user.InstitutionId != question.InstitutionId)
            return false;

        if (role == AppRole.SchoolAdmin)
            return true;

        return role == AppRole.Lecturer;
    }

    private async Task EnsureQuestionBankAccessAsync(Guid institutionId)
    {
        if (institutionId == Guid.Empty || !await _context.Institutions.AsNoTracking().AnyAsync(institution =>
                institution.Id == institutionId && institution.DeletedAt == null))
        {
            throw new InvalidOperationException("Institution not found.");
        }

        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin)
            return;
        if (role is AppRole.SchoolAdmin or AppRole.Lecturer && user.InstitutionId == institutionId)
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureQuestionSetCanBeEditedAsync(
        Guid institutionId,
        string examQuestionName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = examQuestionName.Trim().ToLower();
        var now = DateTime.UtcNow;
        if (await _context.ExamSlots.AsNoTracking().AnyAsync(slot =>
                slot.Class.DeletedAt == null &&
                slot.Class.InstitutionId == institutionId &&
                slot.ExamQuestionName.ToLower() == normalizedName &&
                slot.Status != ExamSlotStatus.Cancelled &&
                (slot.Status != ExamSlotStatus.Scheduled || slot.StartTime <= now),
                cancellationToken))
        {
            throw new InvalidOperationException("This question set cannot be changed after a linked exam has started.");
        }
    }

    private static void ValidateQuestion(
        string? questionType,
        string? questionContent,
        string? audioUrl,
        string? imageUrl,
        decimal points,
        IEnumerable<CreateQuestionOptionDto>? options)
    {
        ValidateQuestionCore(questionType, questionContent, audioUrl, imageUrl, points);
        ValidateOptions(questionType!, options);
    }

    private static void ValidateQuestion(
        string? questionType,
        string? questionContent,
        string? audioUrl,
        string? imageUrl,
        decimal points,
        IEnumerable<QuestionOption> options)
    {
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

    private static void ValidateExamQuestionName(string? examQuestionName)
    {
        if (string.IsNullOrWhiteSpace(examQuestionName))
            throw new InvalidOperationException("Exam question name is required.");
        if (examQuestionName.Trim().Length > 255)
            throw new InvalidOperationException("Exam question name cannot exceed 255 characters.");
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

    private static List<ImportedQuestionRow> ReadExcelQuestions(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("The workbook does not contain a worksheet.");
            var headerRow = sheet.FirstRowUsed()
                ?? throw new InvalidOperationException("The workbook is empty.");
            var headers = headerRow.CellsUsed()
                .GroupBy(cell => NormalizeHeader(cell.GetString()))
                .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber);
            var requiredHeaders = new[] { "stt", "question", "a", "b", "c", "d", "answers" };
            var missingHeaders = requiredHeaders.Where(header => !headers.ContainsKey(header)).ToList();
            if (missingHeaders.Count > 0)
                throw new InvalidOperationException($"Missing required columns: {string.Join(", ", missingHeaders)}.");

            var questions = new List<ImportedQuestionRow>();
            var displayOrders = new HashSet<int>();
            foreach (var row in sheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
            {
                string Value(string header) => row.Cell(headers[header]).GetFormattedString().Trim();
                var values = requiredHeaders.ToDictionary(header => header, Value);
                if (values.Values.All(string.IsNullOrWhiteSpace))
                    continue;

                var rowNumber = row.RowNumber();
                if (!int.TryParse(values["stt"], out var displayOrder) || displayOrder <= 0)
                    throw new InvalidOperationException($"Row {rowNumber}: Stt must be a positive integer.");
                if (!displayOrders.Add(displayOrder))
                    throw new InvalidOperationException($"Row {rowNumber}: Stt {displayOrder} is duplicated.");
                if (string.IsNullOrWhiteSpace(values["question"]))
                    throw new InvalidOperationException($"Row {rowNumber}: Question is required.");

                var answer = values["answers"].ToUpperInvariant();
                if (answer is not ("A" or "B" or "C" or "D"))
                    throw new InvalidOperationException($"Row {rowNumber}: Answers must be A, B, C, or D.");

                foreach (var label in new[] { "A", "B", "C", "D" })
                    if (string.IsNullOrWhiteSpace(values[label.ToLowerInvariant()]))
                        throw new InvalidOperationException($"Row {rowNumber}: option {label} is required.");

                questions.Add(new ImportedQuestionRow(
                    displayOrder, values["question"], values["a"], values["b"],
                    values["c"], values["d"], answer));
            }

            return questions;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The Excel file is invalid or cannot be read.", ex);
        }
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant();

    private static string GetImportedExamQuestionName(string fileName)
    {
        var examQuestionName = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(examQuestionName))
            throw new InvalidOperationException("The Excel file name must contain an exam name.");
        if (examQuestionName.Length > 255)
            throw new InvalidOperationException("The Excel file name cannot exceed 255 characters.");
        return examQuestionName;
    }

    private static ExamQuestion MapImportedQuestion(ImportedQuestionRow row, Guid institutionId, string examQuestionName) => new()
    {
        Id = Guid.NewGuid(),
        InstitutionId = institutionId,
        ExamQuestionName = examQuestionName,
        QuestionType = "MultipleChoice",
        QuestionContent = row.Question,
        Points = 1,
        DisplayOrder = row.Stt,
        CreatedAt = DateTime.UtcNow,
        QuestionOptions = new[] { ("A", row.A), ("B", row.B), ("C", row.C), ("D", row.D) }
            .Select(option => new QuestionOption
            {
                Id = Guid.NewGuid(),
                OptionLabel = option.Item1,
                OptionContent = option.Item2,
                IsCorrect = option.Item1 == row.Answers
            })
            .ToList()
    };

    private async Task<ReadingPassage?> GetPassageAsync(Guid? passageId, ExamQuestion question)
    {
        if (!passageId.HasValue) return null;

        var passage = await _context.ReadingPassages
            .FirstOrDefaultAsync(item => item.Id == passageId.Value)
            ?? throw new InvalidOperationException("Reading passage not found.");
        if (passage.InstitutionId != question.InstitutionId ||
            !string.Equals(passage.ExamQuestionName, question.ExamQuestionName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Reading passage must belong to the same question set.");
        }

        return passage;
    }

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

    private sealed record ImportedQuestionRow(
        int Stt, string Question, string A, string B, string C, string D, string Answers);
}
