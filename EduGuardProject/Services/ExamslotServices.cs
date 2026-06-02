
using EduGuardProject.DTOs.Request;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services;

public class ExamslotServices : IExamSlotServices
{
    private readonly IExamslotRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ExamslotServices(IExamslotRepository repo, AppDbContext context, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<(IEnumerable<ExamSlot> Items, int TotalCount)> GetAllExamSlotsAsync(string? search, string? sort, int page, int pageSizel)
    {
        return await _repo.GetAllAsync(search, sort, page, pageSizel);
    }

    public Task<ExamSlot?> GetByIdAsync(Guid ExamId)
    {
        return _repo.GetByIdAsync(ExamId);
    }

    public async Task<ExamSlot> CreateAsync(CreateExamSlotDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();

        var entity = new ExamSlot
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            CreatedBy = user.Id,
            // DTO does not include `ExamName` in current workspace DTOs; supply a sensible default.
            ExamName = $"Exam-{dto.ExamId}",
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            ExpectedDurationMinutes = dto.ExpectedDurationMinutes,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };

        await _repo.AddAsync(entity);
        return entity;
    }

    public async Task<bool> UpdateAsync(Guid ExamId, UpdateExamSlotDto dto)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return false;

        entity.ExpectedDurationMinutes = dto.ExpectedDurationMinutes != 0
            ? dto.ExpectedDurationMinutes
            : entity.ExpectedDurationMinutes;

        if (dto.StartTime.HasValue) entity.StartTime = dto.StartTime.Value;
        if (dto.EndTime.HasValue) entity.EndTime = dto.EndTime.Value;

        entity.UpdatedAt = dto.UpdatedAt;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid ExamId)
    {
        var entity = await _repo.GetByIdAsync(ExamId);
        if (entity == null) return false;

        await _repo.DeleteAsync(entity);
        return true;
    }
}
     
