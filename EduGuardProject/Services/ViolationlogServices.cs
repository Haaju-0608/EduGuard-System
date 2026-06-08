using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services;

public class ViolationLogServices : IViolationLogService
{
    private readonly IViolationLogRepository _repo;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ViolationLogServices(IViolationLogRepository repo, AppDbContext context, ICurrentUserService currentUser)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
    }

    public Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? participationId = null)
    {
        return _repo.GetAllAsync(search, sort, page, pageSize, participationId);
    }
    public Task<ViolationLog?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public async Task<ViolationLog> CreateAsync(CreateViolationLogDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);

        var entity = new ViolationLog
        {
            Id = Guid.NewGuid(),
            severity = dto.severity,
            violationType = dto.violationType,
            ParticipationId = dto.ParticipationId,
            RecordedAt = DateTime.UtcNow
        };
        entity.RecordedAt = entity.RecordedAt == default ? DateTime.UtcNow : entity.RecordedAt;

        await _repo.CreateAsync(entity);
        return entity;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateViolationLogDto dto)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null) return false;

        // update allowed fields
        existing.IsReviewed = dto.IsReviewed;
        existing.ReviewedBy = dto.ReviewedBy;

        await _repo.UpdateAsync(id, dto);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null) return false;

        await _repo.DeleteAsync(id);
        return true;
    }
}
