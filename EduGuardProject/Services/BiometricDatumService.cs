using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class BiometricDatumService : IBiometricDatumService
{
    private readonly IBiometricDatumRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public BiometricDatumService(
        IBiometricDatumRepository repo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<BiometricDatumResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? userId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student)
            userId = user.Id;
        else
            await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, userId);
        var dtos = new List<BiometricDatumResponseDto>();
        foreach (var item in items)
            dtos.Add(await AcademicMapper.MapBiometricDatumAsync(_context, item, expand));
        return (dtos, total);
    }

    public async Task<BiometricDatumResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureAccessAsync(entity);
        return await AcademicMapper.MapBiometricDatumAsync(_context, entity, expand);
    }

    public async Task<BiometricDatumResponseDto> CreateAsync(CreateBiometricDatumDto dto)
    {
        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student && dto.UserId != user.Id)
            throw new UnauthorizedAccessException("Students can only create biometric data for themselves.");
        else
            await _currentUser.EnsureRoleAsync(AppRole.Student, AppRole.SchoolAdmin, AppRole.SuperAdmin);

        await DeactivateExistingActiveAsync(dto.UserId);

        var entity = new BiometricDatum
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            BioRequestId = dto.BioRequestId,
            ModelVersion = dto.ModelVersion,
            FaceImageUrl = dto.FaceImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        return await AcademicMapper.MapBiometricDatumAsync(_context, entity, null);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateBiometricDatumDto dto)
    {
        var entity = await _context.BiometricData.FirstOrDefaultAsync(b => b.Id == id);
        if (entity == null || !entity.IsActive) return false;

        await EnsureAccessAsync(entity);

        if (!string.IsNullOrWhiteSpace(dto.ModelVersion))
            entity.ModelVersion = dto.ModelVersion;
        if (dto.FaceImageUrl != null)
            entity.FaceImageUrl = dto.FaceImageUrl;
        if (dto.IsActive.HasValue)
            entity.IsActive = dto.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.BiometricData.FirstOrDefaultAsync(b => b.Id == id);
        if (entity == null || !entity.IsActive) return false;

        await EnsureAccessAsync(entity);
        await _repo.SoftDeleteAsync(entity);
        return true;
    }

    private async Task DeactivateExistingActiveAsync(Guid userId)
    {
        var activeRecords = await _context.BiometricData
            .Where(b => b.UserId == userId && b.IsActive)
            .ToListAsync();

        foreach (var record in activeRecords)
        {
            record.IsActive = false;
            record.UpdatedAt = DateTime.UtcNow;
        }

        if (activeRecords.Count > 0)
            await _context.SaveChangesAsync();
    }

    private async Task EnsureAccessAsync(BiometricDatum entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin || user.Role == AppRole.SchoolAdmin) return;

        if (user.Role == AppRole.Student && entity.UserId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
