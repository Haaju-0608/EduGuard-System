using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EduGuardProject.Services;

public class StudentExamRecordService : IStudentExamRecordService
{
    private const decimal ExamScoreScale = 10m;

    private readonly IStudentExamRecordRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public StudentExamRecordService(
        IStudentExamRecordRepository repo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<StudentExamRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? studentId = null, StudentExamRecordStatus? status = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;
        if (user.Role == AppRole.Student)
            studentId = user.Id;

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, examSlotId, studentId, status, institutionId, lecturerId);
        return (items.Select(MapToResponseDto), total);
    }

    public async Task<StudentExamRecordResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureAccessAsync(entity);
        return MapToResponseDto(entity);
    }

    public async Task<StudentExamRecordResponseDto> CreateAsync(CreateStudentExamRecordDto dto)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students must submit exam records through the submit endpoint.");

        if (dto.ExamSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");
        if (dto.StudentId == Guid.Empty)
            throw new InvalidOperationException("Student id is required.");

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");

        ValidateRecordInput(dto.EndedAt, dto.SubmittedAt, dto.DurationSeconds, dto.FinalScore, dto.Status, examSlot);

        var student = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == dto.StudentId &&
                u.Role == AppRole.Student &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active)
            ?? throw new InvalidOperationException("Student not found.");

        if (user.Role != AppRole.Student)
            await EnsureClassAccessAsync(examSlot.Class, user);

        if (user.Role != AppRole.SuperAdmin && student.InstitutionId != examSlot.Class.InstitutionId)
            throw new UnauthorizedAccessException("Student does not belong to this institution.");
        await EnsureStudentRecordEligibilityAsync(dto.StudentId, examSlot);
        await EnsureNoActiveRecordAsync(dto.ExamSlotId, dto.StudentId);

        var entity = new StudentExamRecord
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            StudentId = dto.StudentId,
            CreatedAt = DateTime.UtcNow,
            EndedAt = dto.EndedAt,
            ExamRecord = NormalizeExamRecord(dto.ExamRecord),
            FinalScore = dto.FinalScore,
            SubmittedAt = dto.SubmittedAt,
            DurationSeconds = dto.DurationSeconds,
            Status = dto.Status
        };

        await _repo.AddAsync(entity);
        entity.ExamSlot = examSlot;
        entity.Student = student;
        return MapToResponseDto(entity);
    }

    public async Task<StudentExamRecordResponseDto> SubmitAsync(SubmitStudentExamRecordDto dto)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.Student)
            throw new UnauthorizedAccessException("Only students can submit exam records.");

        if (dto.ExamSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");
        if (dto.Answers == null)
            throw new InvalidOperationException("Answers are required.");

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .Include(e => e.ExamQuestions)
                .ThenInclude(q => q.QuestionOptions)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");
        ValidateDuration(dto.DurationSeconds, examSlot);

        if (user.InstitutionId != examSlot.Class.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        var student = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id && u.Role == AppRole.Student && u.DeletedAt == null)
            ?? throw new InvalidOperationException("Student not found.");

        var participation = await _context.ExamParticipations
            .FirstOrDefaultAsync(p => p.ExamSlotId == dto.ExamSlotId && p.StudentId == user.Id)
            ?? throw new InvalidOperationException("Exam participation not found.");

        var entity = await _context.StudentExamRecords
            .Include(r => r.ExamSlot)
                .ThenInclude(e => e.Class)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r =>
                r.ExamSlotId == dto.ExamSlotId &&
                r.StudentId == user.Id &&
                r.Status != StudentExamRecordStatus.Deleted);

        if (participation.Status == ParticipationStatus.Submitted && entity != null)
            return MapToResponseDto(entity);

        if (participation.Status is not ParticipationStatus.Joined and not ParticipationStatus.Submitted)
            throw new InvalidOperationException("This action is only allowed while the participation is JOINED or SUBMITTED.");

        var now = DateTime.UtcNow;
        ValidateRecordTimes(now, now, examSlot);
        var (examRecord, finalScore, requiresManualMarking) = BuildSubmissionRecord(examSlot, user.Id, dto, now);

        entity ??= new StudentExamRecord
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            StudentId = user.Id,
            CreatedAt = now
        };

        entity.EndedAt = now;
        entity.ExamRecord = examRecord;
        entity.FinalScore = finalScore;
        entity.SubmittedAt = now;
        entity.DurationSeconds = dto.DurationSeconds;
        entity.Status = requiresManualMarking ? StudentExamRecordStatus.Completed : StudentExamRecordStatus.Marked;
        entity.ExamSlot = examSlot;
        entity.Student = student;

        participation.Status = ParticipationStatus.Submitted;
        participation.ActualEnd = now;

        if (_context.Entry(entity).State == EntityState.Detached)
            await _repo.AddAsync(entity);
        else
            await _repo.UpdateAsync(entity);

        return MapToResponseDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateStudentExamRecordDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot update exam records directly.");
        await EnsureClassAccessAsync(entity.ExamSlot.Class, user);
        EnsureNotSubmitted(entity);
        ValidateRecordInput(dto.EndedAt, dto.SubmittedAt, dto.DurationSeconds, dto.FinalScore, dto.Status, entity.ExamSlot);

        entity.EndedAt = dto.EndedAt;
        entity.ExamRecord = NormalizeExamRecord(dto.ExamRecord);
        entity.FinalScore = dto.FinalScore;
        entity.SubmittedAt = dto.SubmittedAt;
        entity.DurationSeconds = dto.DurationSeconds;
        entity.Status = dto.Status;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<StudentExamRecordResponseDto?> GradeManualAsync(Guid id, GradeStudentExamRecordDto dto)
    {
        if (dto.Grades == null || dto.Grades.Count == 0)
            throw new InvalidOperationException("Grades are required.");

        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot grade exam records.");
        await EnsureClassAccessAsync(entity.ExamSlot.Class, user);

        if (entity.Status == StudentExamRecordStatus.Deleted)
            throw new InvalidOperationException("Deleted exam records cannot be graded.");

        var root = ParseExamRecord(entity.ExamRecord);
        if (root["answers"] is not JsonArray answers)
            throw new InvalidOperationException("Exam record answers are missing.");

        var grades = BuildGradeLookup(dto.Grades);
        var updatedQuestionIds = new HashSet<Guid>();
        var rawScore = 0m;
        var answeredMaxScore = 0m;
        var stillNeedsManualMarking = false;

        foreach (var item in answers)
        {
            if (item is not JsonObject answer)
                throw new InvalidOperationException("Exam record answer is invalid.");

            var questionId = ReadGuid(answer, "questionId");
            if (grades.TryGetValue(questionId, out var awardedPoints))
            {
                if (!IsManualAnswer(answer))
                    throw new InvalidOperationException("Only manual answers can be graded manually.");

                var maxPoints = ReadDecimal(answer, "maxPoints");
                if (awardedPoints > maxPoints)
                    throw new InvalidOperationException("Awarded points cannot exceed max points.");

                answer["awardedPoints"] = JsonValue.Create(awardedPoints);
                answer["needsManualMarking"] = JsonValue.Create(false);
                updatedQuestionIds.Add(questionId);
            }

            stillNeedsManualMarking |= ReadBool(answer, "needsManualMarking");
            rawScore += ReadOptionalDecimal(answer, "awardedPoints") ?? 0m;
            answeredMaxScore += ReadDecimal(answer, "maxPoints");
        }

        if (updatedQuestionIds.Count != grades.Count)
            throw new InvalidOperationException("Grade question does not exist in this exam record.");

        var rawMaxScore = ReadOptionalDecimal(root, "rawMaxScore")
            ?? ReadOptionalDecimal(root, "maxScore")
            ?? answeredMaxScore;
        var finalScore = ToExamScore(rawScore, rawMaxScore);
        root["rawScore"] = JsonValue.Create(rawScore);
        root["rawMaxScore"] = JsonValue.Create(rawMaxScore);
        root["finalScore"] = JsonValue.Create(finalScore);
        root["maxScore"] = JsonValue.Create(ExamScoreScale);
        root["requiresManualMarking"] = JsonValue.Create(stillNeedsManualMarking);
        entity.ExamRecord = root.ToJsonString();
        entity.FinalScore = finalScore;
        entity.Status = stillNeedsManualMarking ? StudentExamRecordStatus.Completed : StudentExamRecordStatus.Marked;

        await _repo.UpdateAsync(entity);
        return MapToResponseDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot delete exam records directly.");
        await EnsureClassAccessAsync(entity.ExamSlot.Class, user);

        entity.Status = StudentExamRecordStatus.Deleted;
        await _repo.UpdateAsync(entity);
        return true;
    }

    private async Task EnsureAccessAsync(StudentExamRecord entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && entity.StudentId == user.Id)
            return;

        await EnsureClassAccessAsync(entity.ExamSlot.Class, user);
    }

    private static Task EnsureClassAccessAsync(Class cls, User user)
    {
        if (user.Role == AppRole.SuperAdmin)
            return Task.CompletedTask;

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId == cls.InstitutionId)
            return Task.CompletedTask;

        if (user.Role == AppRole.Lecturer &&
            user.InstitutionId == cls.InstitutionId &&
            user.Id == cls.LecturerId)
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureStudentRecordEligibilityAsync(Guid studentId, ExamSlot examSlot)
    {
        var isEnrolled = await _context.ClassEnrollments.AsNoTracking().AnyAsync(e =>
            e.ClassId == examSlot.ClassId &&
            e.StudentId == studentId &&
            e.Status == EnrollmentStatus.Active);
        if (!isEnrolled)
            throw new UnauthorizedAccessException("Student is not enrolled in this class.");

        var hasParticipation = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
            p.ExamSlotId == examSlot.Id &&
            p.StudentId == studentId);
        if (!hasParticipation)
            throw new InvalidOperationException("Exam participation not found.");
    }

    private async Task EnsureNoActiveRecordAsync(Guid examSlotId, Guid studentId)
    {
        var exists = await _context.StudentExamRecords.AsNoTracking().AnyAsync(r =>
            r.ExamSlotId == examSlotId &&
            r.StudentId == studentId &&
            r.Status != StudentExamRecordStatus.Deleted);
        if (exists)
            throw new InvalidOperationException("Student exam record already exists.");
    }

    private static StudentExamRecordResponseDto MapToResponseDto(StudentExamRecord entity) => new()
    {
        Id = entity.Id,
        ExamSlotId = entity.ExamSlotId,
        ExamName = entity.ExamSlot?.ExamName,
        StudentId = entity.StudentId,
        StudentName = entity.Student?.FullName,
        CreatedAt = entity.CreatedAt,
        EndedAt = entity.EndedAt,
        ExamRecord = RestoreLegacyExamRecord(entity.ExamRecord),
        FinalScore = entity.FinalScore,
        SubmittedAt = entity.SubmittedAt,
        DurationSeconds = entity.DurationSeconds,
        Status = entity.Status,
        MaxScore = ExamScoreScale
    };

    private static void EnsureNotSubmitted(StudentExamRecord entity)
    {
        if (entity.SubmittedAt.HasValue)
            throw new InvalidOperationException("Submitted exam records cannot be edited.");
    }

    private static void ValidateScore(decimal? score)
    {
        if (score.HasValue && (score.Value < 0 || score.Value > ExamScoreScale))
            throw new InvalidOperationException("Final score must be between 0 and 10.");
    }

    private static void ValidateRecordInput(
        DateTime? endedAt,
        DateTime? submittedAt,
        int? durationSeconds,
        decimal? finalScore,
        StudentExamRecordStatus status,
        ExamSlot examSlot)
    {
        if (status == StudentExamRecordStatus.Deleted)
            throw new InvalidOperationException("Use the delete endpoint to delete student exam records.");
        ValidateScore(finalScore);
        ValidateDuration(durationSeconds, examSlot);
        ValidateRecordTimes(endedAt, submittedAt, examSlot);
    }

    private static void ValidateDuration(int? durationSeconds, ExamSlot examSlot)
    {
        if (durationSeconds is < 0)
            throw new InvalidOperationException("Duration seconds must be greater than or equal to 0.");

        var examDurationSeconds = (examSlot.EndTime - examSlot.StartTime).TotalSeconds;
        if (durationSeconds.HasValue && examDurationSeconds > 0 && durationSeconds.Value > examDurationSeconds)
            throw new InvalidOperationException("Duration seconds cannot exceed exam slot duration.");
    }

    private static void ValidateRecordTimes(DateTime? endedAt, DateTime? submittedAt, ExamSlot examSlot)
    {
        var now = DateTime.UtcNow;
        if (endedAt.HasValue && endedAt.Value > now)
            throw new InvalidOperationException("Ended time cannot be in the future.");
        if (submittedAt.HasValue && submittedAt.Value > now)
            throw new InvalidOperationException("Submitted time cannot be in the future.");
        if (endedAt.HasValue && IsOutsideExamSlot(endedAt.Value, examSlot))
            throw new InvalidOperationException("Ended time must be within exam slot time range.");
        if (submittedAt.HasValue && IsOutsideExamSlot(submittedAt.Value, examSlot))
            throw new InvalidOperationException("Submitted time must be within exam slot time range.");
        if (endedAt.HasValue && submittedAt.HasValue && endedAt.Value < submittedAt.Value)
            throw new InvalidOperationException("Ended time cannot be before submitted time.");
    }

    private static bool IsOutsideExamSlot(DateTime value, ExamSlot examSlot) =>
        value < examSlot.StartTime || value > examSlot.EndTime;

    private static decimal ToExamScore(decimal rawScore, decimal rawMaxScore) =>
        rawMaxScore <= 0
            ? 0
            : Math.Round(rawScore * ExamScoreScale / rawMaxScore, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeExamRecord(string? examRecord)
    {
        if (string.IsNullOrWhiteSpace(examRecord))
            return null;

        var trimmed = examRecord.Trim();
        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { legacyText = examRecord });
        }
    }

    private static string? RestoreLegacyExamRecord(string? examRecord)
    {
        if (string.IsNullOrWhiteSpace(examRecord))
            return examRecord;

        try
        {
            using var doc = JsonDocument.Parse(examRecord);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return examRecord;

            var properties = doc.RootElement.EnumerateObject().ToList();
            return properties.Count == 1 && properties[0].Name == "legacyText"
                ? properties[0].Value.GetString()
                : examRecord;
        }
        catch (JsonException)
        {
            return examRecord;
        }
    }

    private static JsonObject ParseExamRecord(string? examRecord)
    {
        if (string.IsNullOrWhiteSpace(examRecord))
            throw new InvalidOperationException("Exam record is empty.");

        try
        {
            return JsonNode.Parse(examRecord) as JsonObject
                ?? throw new InvalidOperationException("Exam record is invalid.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Exam record is invalid.");
        }
    }

    private static Dictionary<Guid, decimal> BuildGradeLookup(IEnumerable<GradeStudentAnswerDto> grades)
    {
        var lookup = new Dictionary<Guid, decimal>();
        foreach (var grade in grades)
        {
            if (grade.QuestionId == Guid.Empty)
                throw new InvalidOperationException("Question id is required.");
            if (grade.AwardedPoints < 0)
                throw new InvalidOperationException("Awarded points must be greater than or equal to 0.");
            if (!lookup.TryAdd(grade.QuestionId, grade.AwardedPoints))
                throw new InvalidOperationException("Duplicate grades are not allowed.");
        }

        return lookup;
    }

    private static Guid ReadGuid(JsonObject json, string property)
    {
        var value = json[property]?.GetValue<string>();
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Exam record {property} is invalid.");
    }

    private static decimal ReadDecimal(JsonObject json, string property) =>
        ReadOptionalDecimal(json, property)
            ?? throw new InvalidOperationException($"Exam record {property} is missing.");

    private static decimal? ReadOptionalDecimal(JsonObject json, string property) =>
        json[property]?.GetValue<decimal>();

    private static bool ReadBool(JsonObject json, string property) =>
        json[property]?.GetValue<bool>() == true;

    private static bool IsManualAnswer(JsonObject answer) =>
        ReadBool(answer, "needsManualMarking") ||
        string.Equals(answer["questionType"]?.GetValue<string>(), "Essay", StringComparison.OrdinalIgnoreCase);

    private static (string ExamRecord, decimal FinalScore, bool RequiresManualMarking) BuildSubmissionRecord(
        ExamSlot examSlot,
        Guid studentId,
        SubmitStudentExamRecordDto dto,
        DateTime submittedAt)
    {
        if (examSlot.ExamQuestions.Count > 0 && dto.Answers.Count == 0)
            throw new InvalidOperationException("Answers are required.");

        var questions = examSlot.ExamQuestions.ToDictionary(q => q.Id);
        var seenQuestionIds = new HashSet<Guid>();
        var answers = new List<object>();
        var rawScore = 0m;
        var requiresManualMarking = false;

        foreach (var answer in dto.Answers)
        {
            if (answer.QuestionId == Guid.Empty)
                throw new InvalidOperationException("Question id is required.");

            if (!seenQuestionIds.Add(answer.QuestionId))
                throw new InvalidOperationException("Duplicate answers are not allowed.");

            if (!questions.TryGetValue(answer.QuestionId, out var question))
                throw new InvalidOperationException("Question does not belong to this exam slot.");

            var options = question.QuestionOptions.ToList();
            var isManual = IsManualQuestion(question, options);
            var selectedOption = isManual ? null : FindSelectedOption(options, answer);

            if (!isManual && selectedOption == null)
                throw new InvalidOperationException("Answer option not found.");

            var awardedPoints = !isManual && selectedOption!.IsCorrect ? question.Points : 0m;
            rawScore += awardedPoints;
            requiresManualMarking |= isManual;

            answers.Add(new
            {
                questionId = question.Id,
                questionType = question.QuestionType,
                optionId = selectedOption?.Id ?? answer.OptionId,
                selectedOption = selectedOption?.OptionLabel ?? answer.SelectedOption,
                answerText = answer.AnswerText,
                awardedPoints,
                maxPoints = question.Points,
                needsManualMarking = isManual
            });
        }

        var rawMaxScore = examSlot.ExamQuestions.Sum(q => q.Points);
        var finalScore = ToExamScore(rawScore, rawMaxScore);
        var examRecord = JsonSerializer.Serialize(new
        {
            examSlotId = examSlot.Id,
            studentId,
            submittedAt,
            durationSeconds = dto.DurationSeconds,
            rawScore,
            rawMaxScore,
            finalScore,
            maxScore = ExamScoreScale,
            requiresManualMarking,
            answers
        });

        return (examRecord, finalScore, requiresManualMarking);
    }

    private static bool IsManualQuestion(ExamQuestion question, IReadOnlyCollection<QuestionOption> options) =>
        options.Count == 0 || string.Equals(question.QuestionType, "Essay", StringComparison.OrdinalIgnoreCase);

    private static QuestionOption? FindSelectedOption(IEnumerable<QuestionOption> options, SubmitStudentAnswerDto answer)
    {
        if (answer.OptionId.HasValue && answer.OptionId.Value != Guid.Empty)
            return options.FirstOrDefault(o => o.Id == answer.OptionId.Value);

        if (!string.IsNullOrWhiteSpace(answer.SelectedOption))
        {
            var selectedOption = answer.SelectedOption.Trim();
            return options.FirstOrDefault(o =>
                string.Equals(o.OptionLabel, selectedOption, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
