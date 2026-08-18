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

        var user = await EnsureReadAccessAsync(entity.ExamSlot);
        return MapToResponseDto(entity, includeAnswers: user.Role.ToCanonical() != AppRole.Student);
    }

    public async Task<ReadingPassageResponseDto> CreateAsync(CreateReadingPassageDto dto)
    {
        if (dto.ExamSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");
        await EnsureStaffAccessAsync(examSlot.Class);
        EnsureExamQuestionsCanBeEdited(examSlot);

        var now = DateTime.UtcNow;
        var entity = new ReadingPassage
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            PassageText = ValidatePassageText(dto.PassageText),
            CreatedAt = now,
            UpdatedAt = now,
            ExamSlot = examSlot
        };

        _context.ReadingPassages.Add(entity);
        await _context.SaveChangesAsync();
        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<ReadingPassageResponseDto?> UpdateAsync(Guid id, UpdateReadingPassageDto dto)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        await EnsureStaffAccessAsync(entity.ExamSlot.Class);
        EnsureExamQuestionsCanBeEdited(entity.ExamSlot);

        entity.PassageText = ValidatePassageText(dto.PassageText);
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToResponseDto(entity, includeAnswers: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.ReadingPassages
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return false;

        await EnsureStaffAccessAsync(entity.ExamSlot.Class);
        EnsureExamQuestionsCanBeEdited(entity.ExamSlot);

        if (await _context.ExamQuestions.AnyAsync(q => q.PassageId == id))
            throw new InvalidOperationException("Reading passage cannot be deleted while exam questions reference it.");

        _context.ReadingPassages.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    private IQueryable<ReadingPassage> BaseQuery() =>
        _context.ReadingPassages
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .Include(p => p.ExamQuestions)
            .ThenInclude(q => q.ExamSlot)
            .Include(p => p.ExamQuestions)
            .ThenInclude(q => q.QuestionOptions);

    private async Task<User> EnsureReadAccessAsync(ExamSlot examSlot)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (CanAccessAsStaff(user, examSlot.Class))
            return user;

        if (user.Role.ToCanonical() == AppRole.Student)
        {
            var now = DateTime.UtcNow;
            if (examSlot.Status is ExamSlotStatus.Cancelled or ExamSlotStatus.Completed ||
                now < examSlot.StartTime || now > examSlot.EndTime)
            {
                throw new InvalidOperationException("Reading passages are only available during the exam slot.");
            }

            var hasParticipation = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
                p.ExamSlotId == examSlot.Id &&
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
        if (!CanAccessAsStaff(user, cls))
            throw new UnauthorizedAccessException("Access denied.");
    }

    private static bool CanAccessAsStaff(User user, Class cls)
    {
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin)
            return true;
        if (user.InstitutionId != cls.InstitutionId)
            return false;

        return role == AppRole.SchoolAdmin ||
               (role == AppRole.Lecturer && cls.LecturerId == user.Id);
    }

    private static void EnsureExamQuestionsCanBeEdited(ExamSlot examSlot)
    {
        if (examSlot.Status != ExamSlotStatus.Scheduled)
            throw new InvalidOperationException("Reading passages can only be edited while the exam slot is scheduled.");
        if (examSlot.StartTime <= DateTime.UtcNow)
            throw new InvalidOperationException("Reading passages cannot be edited after the exam has started.");
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
        ExamSlotId = entity.ExamSlotId,
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
