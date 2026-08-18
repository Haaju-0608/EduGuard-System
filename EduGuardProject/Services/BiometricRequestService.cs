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
    private readonly IAiServiceClient _aiClient;

    public BiometricRequestService(
        IBiometricRequestRepository repo,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        AppDbContext context,
        ILogger<BiometricRequestService> logger,
        IAiServiceClient aiClient)
    {
        _repo = repo;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
        _storage = storage;
        _context = context;
        _logger = logger;
        _aiClient = aiClient;
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
        if (entity == null) return null;

        await EnsureRequestAccessAsync(entity);
        return await AcademicMapper.MapBiometricRequestAsync(_context, entity, expand);
    }

    //  SỬA ĐỔI: Không lưu file vào wwwroot nữa, đẩy trực tiếp Stream sang FastAPI & Supabase
    public async Task<BiometricRequestResponseDto> CreateAsync(CreateBiometricRequestDto dto)
    {
        // 1. Kiểm tra phân quyền Sinh viên
        await _currentUser.EnsureRoleAsync(AppRole.Student);
        var user = await _currentUser.GetRequiredUserAsync();

        await SubscriptionGuard.EnsureInstitutionActiveAsync(_context, user.InstitutionId);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Reason is required.");
        if (dto.FrontFile.Length == 0 || dto.LeftFile.Length == 0 || dto.RightFile.Length == 0)
            throw new InvalidOperationException("All three face images must not be empty.");

        var hasPendingRequest = await _context.BiometricRequests.AsNoTracking()
            .AnyAsync(r => r.StudentId == user.Id && r.Status == BiometricReqStatus.Pending);
        if (hasPendingRequest)
            throw new InvalidOperationException("You already have a pending biometric request awaiting review.");

        // 2. Mở Stream 3 file ảnh từ Request
        using var frontStream = dto.FrontFile.OpenReadStream();
        using var leftStream = dto.LeftFile.OpenReadStream();
        using var rightStream = dto.RightFile.OpenReadStream();

        // 3. Gọi FastAPI Python trên Render để AI xử lý trích xuất Vector & Upload Supabase
        var aiResponse = await _aiClient.ExtractVectorFrom3FacesAsync(
            frontStream, dto.FrontFile.FileName,
            leftStream, dto.LeftFile.FileName,
            rightStream, dto.RightFile.FileName
        );

        // 4. Khởi tạo thực thể Request lưu link Supabase URL thu được từ AI Service
        var entity = new BiometricRequest
        {
            Id = Guid.NewGuid(),
            StudentId = user.Id,
            Reason = dto.Reason.Trim(),
            Status = BiometricReqStatus.Pending,
            // Lưu trực tiếp URL Supabase được trả về từ Python AI Service
            FrontImagePath = aiResponse.FrontUrl,
            LeftImagePath = aiResponse.LeftUrl,  
            RightImagePath = aiResponse.RightUrl,
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
        if (entity == null || entity.Status != BiometricReqStatus.Pending) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        await EnsureReviewerAccessAsync(user, entity.StudentId);

        var studentInstitutionId = await _context.Users
        .AsNoTracking()
        .Where(u => u.Id == entity.StudentId)
        .Select(u => u.InstitutionId)
        .FirstOrDefaultAsync();
        await SubscriptionGuard.EnsureInstitutionActiveAsync(_context, studentInstitutionId);


        if (string.IsNullOrWhiteSpace(entity.FrontImagePath) ||
            string.IsNullOrWhiteSpace(entity.LeftImagePath) ||
            string.IsNullOrWhiteSpace(entity.RightImagePath))
        {
            throw new InvalidOperationException("Thiếu ảnh để trích xuất vector (cần đủ 3 ảnh thẳng/trái/phải).");
        }

        // mỗi lần chỉ đưa 1 URL nên "average" của 1 phần tử = chính vector đó.
        var imagePaths = new (string Url, string Label)[]
        {
    (entity.FrontImagePath!, "front"),
    (entity.LeftImagePath!, "left"),
    (entity.RightImagePath!, "right"),
        };

        var newVectors = new List<(float[] Vector, string Label)>();
        try
        {
            foreach (var (url, label) in imagePaths)
            {
                var vector = await _aiClient.ExtractAverageVectorFromUrlsAsync(new List<string> { url });
                newVectors.Add((vector, label));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lỗi trích xuất vector khi duyệt: {ex.Message}");
        }

        entity.Status = BiometricReqStatus.Approved;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        var oldActiveRecords = await _context.BiometricData
            .Where(b => b.UserId == entity.StudentId && b.IsActive)
            .ToListAsync();
        foreach (var old in oldActiveRecords)
        {
            old.IsActive = false;
            old.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var (vector, label) in newVectors)
        {
            var biometricDatum = new BiometricDatum
            {
                Id = Guid.NewGuid(),
                UserId = entity.StudentId,
                BioRequestId = entity.Id,
                ModelVersion = "face_recognition_v1",
                FaceVector = new Pgvector.Vector(vector),
                FaceImageUrl = entity.FrontImagePath,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.BiometricData.AddAsync(biometricDatum);
        }
        await _repo.UpdateAsync(entity);
        await _context.SaveChangesAsync();

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
        if (entity == null || entity.Status != BiometricReqStatus.Pending) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        await EnsureReviewerAccessAsync(user, entity.StudentId);
        entity.Status = BiometricReqStatus.Rejected;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        await _repo.UpdateAsync(entity);

        await DeleteRequestImagesAsync(entity);

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

    // THÊM MỚI: helper xoá 3 ảnh gắn trực tiếp với BiometricRequest
    private async Task DeleteRequestImagesAsync(BiometricRequest entity)
    {
        var paths = new[] { entity.FrontImagePath, entity.LeftImagePath, entity.RightImagePath }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct();

        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteAsync(StorageService.BiometricFacesBucket, path!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không xoá được ảnh {Path} của request {RequestId}.", path, entity.Id);
            }
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
        {
            if (entity.StudentId != user.Id)
                throw new UnauthorizedAccessException("Access denied.");

            if (entity.Status == BiometricReqStatus.Approved)
                throw new InvalidOperationException(
                    "Không thể xoá yêu cầu đã được duyệt. Nếu muốn đăng ký lại khuôn mặt, " +
                    "hãy gửi một yêu cầu đăng ký mới.");
        }
        else
        {
            await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
            await EnsureReviewerAccessAsync(user, entity.StudentId);

            if (entity.Status == BiometricReqStatus.Approved)
            {
                var relatedBiometrics = await _context.BiometricData
                    .Where(b => b.BioRequestId == entity.Id && b.IsActive)
                    .ToListAsync();
                foreach (var b in relatedBiometrics)
                {
                    b.IsActive = false;
                    b.UpdatedAt = DateTime.UtcNow;
                }
                if (relatedBiometrics.Count > 0)
                    await _context.SaveChangesAsync();
            }
        }

        await _repo.SoftDeleteAsync(entity);
        await DeleteRequestImagesAsync(entity); // dọn 3 ảnh front/left/right khỏi storage,
                                                // nhất quán với luồng RejectAsync
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

    private async Task DeleteBiometricFilesAsync(Guid biometricRequestId)
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
                    "Could not delete biometric review file {Path} for request {RequestId}.",
                    path,
                    biometricRequestId);
            }
        }
    }
}