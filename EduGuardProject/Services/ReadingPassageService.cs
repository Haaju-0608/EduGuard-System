using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ReadingPassageService : IReadingPassageService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReadingPassageService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ReadingPassageResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await BaseQuery().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        var user = await EnsureReadAccessAsync(entity);
        return MapToResponseDto(entity, includeAnswers: user.Role.ToCanonical() != AppRole.Student);
    }

    public async Task<ReadingPassageResponseDto> CreateAsync(CreateReadingPassageDto dto)
    {
        ValidateExamQuestionName(dto.ExamQuestionName);
        await EnsureQuestionBankAccessAsync(dto.InstitutionId);
        await EnsureQuestionSetCanBeEditedAsync(dto.InstitutionId, dto.ExamQuestionName);

        var now = DateTime.UtcNow;
        var entity = new ReadingPassage
        {
            Id = Guid.NewGuid(),
            InstitutionId = dto.InstitutionId,
            ExamQuestionName = dto.ExamQuestionName.Trim(),
            PassageText = ValidatePassageText(dto.PassageText),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ReadingPassages.Add(entity);
        await _context.SaveChangesAsync();
        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<ReadingPassageResponseDto?> UpdateAsync(Guid id, UpdateReadingPassageDto dto)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        await EnsurePassageWriteAccessAsync(entity);
        entity.PassageText = ValidatePassageText(dto.PassageText);
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return false;

        await EnsurePassageWriteAccessAsync(entity);
        if (entity.ExamQuestions.Count > 0)
            throw new InvalidOperationException("Reading passage cannot be deleted while exam questions reference it.");

        _context.ReadingPassages.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    private IQueryable<ReadingPassage> BaseQuery() =>
        _context.ReadingPassages
            .Include(p => p.ExamQuestions)
            .ThenInclude(q => q.QuestionOptions);

    private async Task<User> EnsureReadAccessAsync(ReadingPassage passage)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (CanAccessAsStaff(user, passage))
            return user;

        if (user.Role.ToCanonical() == AppRole.Student)
        {
            var now = DateTime.UtcNow;
            var normalizedName = passage.ExamQuestionName.ToLower();
            var hasParticipation = await _context.ExamSlots.AsNoTracking().AnyAsync(slot =>
                slot.Class.InstitutionId == passage.InstitutionId &&
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

    private async Task EnsurePassageWriteAccessAsync(ReadingPassage passage)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (!CanAccessAsStaff(user, passage))
            throw new UnauthorizedAccessException("Access denied.");
        await EnsureQuestionSetCanBeEditedAsync(passage.InstitutionId, passage.ExamQuestionName);
    }

    private static bool CanAccessAsStaff(User user, ReadingPassage passage)
    {
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin)
            return true;
        if (user.InstitutionId != passage.InstitutionId)
            return false;

        return role == AppRole.SchoolAdmin || role == AppRole.Lecturer;
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
        if ((role is AppRole.SchoolAdmin or AppRole.Lecturer) && user.InstitutionId == institutionId)
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureQuestionSetCanBeEditedAsync(Guid institutionId, string examQuestionName)
    {
        var normalizedName = examQuestionName.Trim().ToLower();
        var now = DateTime.UtcNow;
        if (await _context.ExamSlots.AsNoTracking().AnyAsync(slot =>
                slot.Class.DeletedAt == null &&
                slot.Class.InstitutionId == institutionId &&
                slot.ExamQuestionName.ToLower() == normalizedName &&
                slot.Status != ExamSlotStatus.Cancelled &&
                (slot.Status != ExamSlotStatus.Scheduled || slot.StartTime <= now)))
        {
            throw new InvalidOperationException("Reading passages cannot be changed after a linked exam has started.");
        }
    }

    private static void ValidateExamQuestionName(string? examQuestionName)
    {
        if (string.IsNullOrWhiteSpace(examQuestionName))
            throw new InvalidOperationException("Exam question name is required.");
        if (examQuestionName.Trim().Length > 255)
            throw new InvalidOperationException("Exam question name cannot exceed 255 characters.");
    }

    private static string ValidatePassageText(string? passageText)
    {
        if (string.IsNullOrWhiteSpace(passageText))
            throw new InvalidOperationException("Passage text is required.");
        return passageText.Trim();
    }

    private static ReadingPassageResponseDto MapToResponseDto(ReadingPassage entity, bool includeAnswers) => new()
    {
        Id = entity.Id,
        InstitutionId = entity.InstitutionId,
        ExamQuestionName = entity.ExamQuestionName,
        PassageText = entity.PassageText,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Questions = entity.ExamQuestions
            .OrderBy(q => q.DisplayOrder)
            .ThenBy(q => q.CreatedAt)
            .Select(q => AcademicMapper.ToExamQuestionResponseDto(q, includeAnswers))
            .ToList()
    };
}
