using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class BiometricRequestService : IBiometricRequestService
{
    private readonly IBiometricRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;
    private readonly AppDbContext _context;
    private readonly ILogger<BiometricRequestService> _logger;

    public BiometricRequestService(
        IBiometricRequestRepository repo,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        AppDbContext context,
        ILogger<BiometricRequestService> logger)
    {
        _repo = repo;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
        _storage = storage;
        _context = context;
        _logger = logger;
    }

    public async Task<(IEnumerable<BiometricRequestResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student)
            studentId = user.Id;
        else
            await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin, AppRole.Lecturer);

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, studentId);
        var dtos = new List<BiometricRequestResponseDto>();
        foreach (var item in items)
            dtos.Add(await AcademicMapper.MapBiometricRequestAsync(_context, item, expand));
        return (dtos, total);
    }

    public async Task<BiometricRequestResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.Status == BiometricReqStatus.Rejected) return null;
        await EnsureRequestAccessAsync(entity);
        return await AcademicMapper.MapBiometricRequestAsync(_context, entity, expand);
    }

    public async Task<BiometricRequestResponseDto> CreateAsync(CreateBiometricRequestDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Student);
        var user = await _currentUser.GetRequiredUserAsync();

        var entity = new BiometricRequest
        {
            Id = Guid.NewGuid(),
            StudentId = user.Id,
            Reason = dto.Reason,
            Status = BiometricReqStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await PublishBiometricRequestChangedAsync(entity, "created");
        return await AcademicMapper.MapBiometricRequestAsync(_context, entity, null);
    }

    public async Task<bool> ApproveAsync(Guid id, ReviewBiometricRequestDto? dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        await EnsureReviewerAccessAsync(user, entity.StudentId);
        entity.Status = BiometricReqStatus.Approved;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        await _repo.UpdateAsync(entity);
        await _notifications.SendToUserAsync(
            entity.StudentId,
            "Khuôn mặt đã được phê duyệt",
            "Đăng ký khuôn mặt của bạn đã được phê duyệt.",
            NotificationType.BiometricRequestStatus,
            null,
            entity.Id);
        await PublishBiometricRequestChangedAsync(entity, "approved");
        return true;
    }

    public async Task<bool> RejectAsync(Guid id, ReviewBiometricRequestDto? dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        await EnsureReviewerAccessAsync(user, entity.StudentId);
        entity.Status = BiometricReqStatus.Rejected;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        await _repo.UpdateAsync(entity);
        await DeleteRejectedBiometricFilesAsync(entity.Id);
        await _notifications.SendToUserAsync(
            entity.StudentId,
            "Đăng ký khuôn mặt bị từ chối",
            entity.Reason,
            NotificationType.BiometricRequestStatus,
            null,
            entity.Id);
        await PublishBiometricRequestChangedAsync(entity, "rejected");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && entity.StudentId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
        else
            await _currentUser.EnsureRoleAsync(AppRole.Student, AppRole.SchoolAdmin, AppRole.SuperAdmin);

        await _repo.SoftDeleteAsync(entity);
        await PublishBiometricRequestChangedAsync(entity, "deleted");
        return true;
    }

    private async Task PublishBiometricRequestChangedAsync(BiometricRequest entity, string action)
    {
        var student = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == entity.StudentId);

        await _realtime.PublishDataChangedAsync(
            "biometric-requests",
            action,
            institutionId: student?.InstitutionId,
            userId: entity.StudentId,
            data: new
            {
                requestId = entity.Id,
                entity.StudentId,
                studentName = student?.FullName,
                entity.Status,
                entity.ApprovedBy,
                entity.ReviewedAt,
                entity.CreatedAt
            });
    }

    private async Task EnsureRequestAccessAsync(BiometricRequest entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin)
            return;

        if (user.Role == AppRole.SchoolAdmin)
        {
            await EnsureReviewerAccessAsync(user, entity.StudentId);
            return;
        }

        if (user.Role == AppRole.Student && entity.StudentId == user.Id)
            return;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task EnsureReviewerAccessAsync(User reviewer, Guid studentId)
    {
        if (reviewer.Role == AppRole.SuperAdmin)
            return;

        var studentInstitutionId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == studentId)
            .Select(u => u.InstitutionId)
            .FirstOrDefaultAsync();

        if (reviewer.Role == AppRole.SchoolAdmin &&
            reviewer.InstitutionId.HasValue &&
            reviewer.InstitutionId == studentInstitutionId)
        {
            return;
        }

        throw new UnauthorizedAccessException("You cannot review biometric requests from another institution.");
    }

    private async Task DeleteRejectedBiometricFilesAsync(Guid biometricRequestId)
    {
        var paths = await _context.BiometricData
            .AsNoTracking()
            .Where(b => b.BioRequestId == biometricRequestId && b.FaceImageUrl != null)
            .Select(b => b.FaceImageUrl!)
            .ToListAsync();

        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteAsync(StorageService.BiometricFacesBucket, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not delete rejected biometric file {Path} for request {RequestId}.",
                    path,
                    biometricRequestId);
            }
        }
    }
}
