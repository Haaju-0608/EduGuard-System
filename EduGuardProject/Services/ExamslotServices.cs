
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ExamslotServices : IExamSlotServices
{
    private readonly IExamslotRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly AppDbContext _context;

    public ExamslotServices(
        IExamslotRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
    }

    public async Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllExamSlotsAsync(string? search, string? sort, int page, int pageSizel)
    {
        return await _repo.GetAllAsync(search, sort, page, pageSizel);
    }

    public async Task<ExamslotReponseDto?> GetByIdAsync(Guid ExamId)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        return entity == null ? null : MapToResponseDto(entity);
    }

    public async Task<ExamslotReponseDto> CreateAsync(CreateExamSlotDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();

        var cls = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.ClassId && c.DeletedAt == null)
            ?? throw new InvalidOperationException("Class not found.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        var entity = new ExamSlot
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            CreatedBy = user.Id,
            ExamName = string.IsNullOrWhiteSpace(dto.ExamName)
                ? $"Exam-{dto.ClassId}"
                : dto.ExamName.Trim(),
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            ExpectedDurationMinutes = dto.ExpectedDurationMinutes,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await _notifications.SendToClassStudentsAsync(
            entity.ClassId,
            "Bạn có lịch thi mới",
            $"Lịch thi {entity.ExamName} bắt đầu lúc {entity.StartTime:yyyy-MM-dd HH:mm}.",
            NotificationType.ExamReminder,
            ReferenceTypeEnum.ExamSlot,
            entity.Id);
        await PublishExamSlotChangedAsync(entity, "created");
        return MapToResponseDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid ExamId, UpdateExamSlotDto dto)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return false;

        await EnsureExamSlotAdminAccessAsync(entity);

        if (!string.IsNullOrWhiteSpace(dto.ExamName))
            entity.ExamName = dto.ExamName.Trim();

        entity.ExpectedDurationMinutes = dto.ExpectedDurationMinutes != 0
            ? dto.ExpectedDurationMinutes
            : entity.ExpectedDurationMinutes;

        if (dto.StartTime.HasValue) entity.StartTime = dto.StartTime.Value;
        if (dto.EndTime.HasValue) entity.EndTime = dto.EndTime.Value;

        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await PublishExamSlotChangedAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid ExamId)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return false;

        await EnsureExamSlotAdminAccessAsync(entity);
        await _repo.DeleteAsync(entity);
        await PublishExamSlotChangedAsync(entity, "deleted");
        return true;
    }

    private async Task EnsureExamSlotAdminAccessAsync(ExamSlot entity)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);

        var institutionId = await _context.Classes
            .AsNoTracking()
            .Where(c => c.Id == entity.ClassId && c.DeletedAt == null)
            .Select(c => (Guid?)c.InstitutionId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Class not found.");

        await _currentUser.EnsureInstitutionAccessAsync(institutionId);
    }

    private async Task PublishExamSlotChangedAsync(ExamSlot entity, string action)
    {
        var cls = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == entity.ClassId && c.DeletedAt == null);

        await _realtime.PublishDataChangedAsync(
            "exam-slots",
            action,
            institutionId: cls?.InstitutionId,
            lecturerId: cls?.LecturerId,
            data: new
            {
                examSlotId = entity.Id,
                entity.ClassId,
                entity.ExamName,
                entity.StartTime,
                entity.EndTime,
                entity.Status
            });
    }

    private static ExamslotReponseDto MapToResponseDto(ExamSlot entity) => new()
    {
        Id = entity.Id,
        ClassId = entity.ClassId,
        ExamName = entity.ExamName,
        StartTime = entity.StartTime,
        EndTime = entity.EndTime,
        ExpectedDurationMinutes = entity.ExpectedDurationMinutes,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

}


