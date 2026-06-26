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
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;
    private readonly AppDbContext _context;

    public BiometricDatumService(
        IBiometricDatumRepository repo,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _realtime = realtime;
        _storage = storage;
        _context = context;
    }

    public async Task<(IEnumerable<BiometricDatumResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? userId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        Guid? institutionId = null;

        if (user.Role == AppRole.Student)
        {
            userId = user.Id;
        }
        else if (user.Role == AppRole.SchoolAdmin)
        {
            institutionId = user.InstitutionId
                ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.");
        }
        else if (user.Role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, userId, institutionId: institutionId);
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
        if (!string.IsNullOrWhiteSpace(dto.FaceImageUrl))
            throw new InvalidOperationException("Upload biometric images through /api/storage/biometric.");

        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student)
        {
            if (dto.UserId != user.Id)
                throw new UnauthorizedAccessException("Students can only create biometric data for themselves.");
        }
        else if (user.Role == AppRole.SchoolAdmin)
        {
            await EnsureSameInstitutionAsync(user, dto.UserId);
        }
        else if (user.Role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        await DeactivateExistingActiveAsync(dto.UserId);

        var entity = new BiometricDatum
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            BioRequestId = dto.BioRequestId,
            ModelVersion = dto.ModelVersion,
            FaceImageUrl = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await PublishBiometricDatumChangedAsync(entity, "created");
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
            throw new InvalidOperationException("Upload biometric images through /api/storage/biometric.");
        if (dto.IsActive.HasValue)
            entity.IsActive = dto.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await PublishBiometricDatumChangedAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.BiometricData.FirstOrDefaultAsync(b => b.Id == id);
        if (entity == null || !entity.IsActive) return false;

        await EnsureAccessAsync(entity);
        var faceImagePath = entity.FaceImageUrl;
        await _repo.SoftDeleteAsync(entity);
        if (!string.IsNullOrWhiteSpace(faceImagePath))
            await _storage.DeleteAsync(StorageService.BiometricFacesBucket, faceImagePath);
        await PublishBiometricDatumChangedAsync(entity, "deleted");
        return true;
    }

    private async Task PublishBiometricDatumChangedAsync(BiometricDatum entity, string action)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == entity.UserId);

        await _realtime.PublishDataChangedAsync(
            "biometric-data",
            action,
            institutionId: user?.InstitutionId,
            userId: entity.UserId,
            data: new
            {
                biometricDatumId = entity.Id,
                entity.UserId,
                entity.BioRequestId,
                entity.ModelVersion,
                entity.FaceImageUrl,
                entity.IsActive,
                entity.UpdatedAt
            });
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
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.Role == AppRole.Student && entity.UserId == user.Id)
            return;

        if (user.Role == AppRole.SchoolAdmin)
        {
            await EnsureSameInstitutionAsync(user, entity.UserId);
            return;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureSameInstitutionAsync(User actor, Guid targetUserId)
    {
        if (!actor.InstitutionId.HasValue)
            throw new UnauthorizedAccessException("School admin is not assigned to an institution.");

        var targetInstitutionId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == targetUserId && u.DeletedAt == null)
            .Select(u => u.InstitutionId)
            .FirstOrDefaultAsync();

        if (!targetInstitutionId.HasValue ||
            targetInstitutionId.Value != actor.InstitutionId.Value)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }
    }
}
