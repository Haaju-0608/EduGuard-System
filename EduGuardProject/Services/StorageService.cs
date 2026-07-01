using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class StorageService : IStorageService
{
    public const string BiometricFacesBucket = "biometric-faces";
    public const string AttendanceSnapshotsBucket = "attendance-snapshots";
    public const string AttendanceVideosBucket = "attendance-videos";
    public const string ExamIdentityBucket = "exam-identity";
    public const string ExamRecordingsBucket = "exam-recordings";
    public const string ExamEvidenceBucket = "exam-evidence";

    private const long ImageLimit = 10L * 1024 * 1024;
    private const long VideoLimit = 500L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, StoragePolicy> Policies =
        new Dictionary<string, StoragePolicy>(StringComparer.OrdinalIgnoreCase)
        {
            [BiometricFacesBucket] = StoragePolicy.Images,
            [AttendanceSnapshotsBucket] = StoragePolicy.Images,
            [AttendanceVideosBucket] = StoragePolicy.Videos,
            [ExamIdentityBucket] = StoragePolicy.Images,
            [ExamRecordingsBucket] = StoragePolicy.Videos,
            [ExamEvidenceBucket] = StoragePolicy.Images
        };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;
    private readonly ILogger<StorageService> _logger;

    public StorageService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IRealtimeEventDispatcher realtime,
        ICurrentUserService currentUser,
        AppDbContext context,
        ILogger<StorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _realtime = realtime;
        _currentUser = currentUser;
        _context = context;
        _logger = logger;
    }

    public async Task<StorageUploadResponseDto> UploadAsync(
        IFormFile file,
        string bucket,
        string? folder = null,
        bool upsert = false,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        if (actor.Role != AppRole.SuperAdmin)
            throw new UnauthorizedAccessException("Only Super Admin can use the generic storage upload endpoint.");

        bucket = NormalizeBucket(bucket);
        if (bucket == BiometricFacesBucket)
            throw new InvalidOperationException("Use /api/storage/biometric so raw face images stay tied to a review request.");

        var result = await UploadBusinessFileAsync(
            file,
            bucket,
            string.IsNullOrWhiteSpace(folder) ? [] : NormalizeStoragePath(folder).Split('/'),
            actor.Id,
            upsert,
            cancellationToken);
        result = result with
        {
            Url = (await CreateObjectUrlAsync(
                result.Bucket,
                result.Path,
                IStorageService.NinetyDaySignedUrlExpiresInSeconds,
                cancellationToken)).SignedUrl
        };
        await DispatchStorageChangedAsync("uploaded", result, actor, actor.InstitutionId, cancellationToken);
        LogUpload(actor, result, file.FileName);
        return result;
    }

    public async Task<StorageUploadResponseDto> UploadBiometricAsync(
        IFormFile file,
        Guid biometricDataId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var datum = await _context.BiometricData
            .Include(b => b.User)
            .Include(b => b.BioRequest)
            .FirstOrDefaultAsync(b => b.Id == biometricDataId, cancellationToken)
            ?? throw new InvalidOperationException("Biometric data was not found.");

        EnsureBiometricAccess(actor, datum.User);
        EnsureTemporaryBiometricUpload(datum);
        var institutionId = RequireInstitution(datum.User);

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                BiometricFacesBucket,
                [institutionId.ToString("N"), datum.UserId.ToString("N")],
                datum.UserId,
                datum.FaceImageUrl,
                actor,
                institutionId,
                "biometricDataId",
                datum.Id,
                async uploadedPath =>
                {
                    datum.FaceImageUrl = uploadedPath;
                    datum.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageUploadResponseDto> UploadAttendanceSnapshotAsync(
        IFormFile file,
        Guid attendanceRecordId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var record = await _context.AttendanceRecords
            .Include(r => r.Session)
            .ThenInclude(s => s.Class)
            .FirstOrDefaultAsync(r => r.Id == attendanceRecordId, cancellationToken)
            ?? throw new InvalidOperationException("Attendance record was not found.");

        EnsureAttendanceRecordAccess(actor, record);
        var institutionId = record.Session.Class.InstitutionId;

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                AttendanceSnapshotsBucket,
                [institutionId.ToString("N"), record.Id.ToString("N")],
                record.StudentId,
                record.SnapshotPath,
                actor,
                institutionId,
                "attendanceRecordId",
                record.Id,
                async uploadedPath =>
                {
                    record.SnapshotPath = uploadedPath;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageUploadResponseDto> UploadAttendanceVideoAsync(
        IFormFile file,
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var session = await _context.AttendanceSessions
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Attendance session was not found.");

        EnsureClassStaffAccess(actor, session.Class);

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                AttendanceVideosBucket,
                [session.Class.InstitutionId.ToString("N"), session.Id.ToString("N")],
                session.Id,
                session.VideoPath,
                actor,
                session.Class.InstitutionId,
                "attendanceSessionId",
                session.Id,
                async uploadedPath =>
                {
                    session.VideoPath = uploadedPath;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageUploadResponseDto> UploadExamIdentityAsync(
        IFormFile file,
        Guid participationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        EnsureParticipationAccess(actor, participation);

        var institutionId = participation.ExamSlot.Class.InstitutionId;

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                ExamIdentityBucket,
                ExamFolders(participation),
                participation.StudentId,
                participation.IdentitySnapshotPath,
                actor,
                institutionId,
                "participationId",
                participation.Id,
                async uploadedPath =>
                {
                    participation.IdentitySnapshotPath = uploadedPath;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageUploadResponseDto> UploadExamRecordingAsync(
        IFormFile file,
        Guid participationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var participation = await GetParticipationAsync(participationId, cancellationToken);
        EnsureParticipationAccess(actor, participation);

        var institutionId = participation.ExamSlot.Class.InstitutionId;

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                ExamRecordingsBucket,
                ExamFolders(participation),
                participation.StudentId,
                participation.RecordingVideoPath,
                actor,
                institutionId,
                "participationId",
                participation.Id,
                async uploadedPath =>
                {
                    participation.RecordingVideoPath = uploadedPath;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageUploadResponseDto> UploadEvidenceAsync(
        IFormFile file,
        Guid violationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        var violation = await _context.ViolationLogs
            .Include(v => v.Participation)
            .ThenInclude(p => p.Student)
            .Include(v => v.Participation)
            .ThenInclude(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .ThenInclude(c => c.Institution)
            .FirstOrDefaultAsync(v => v.Id == violationId, cancellationToken)
            ?? throw new InvalidOperationException("Violation log was not found.");

        EnsureClassStaffAccess(actor, violation.Participation.ExamSlot.Class);
        var institutionId = violation.Participation.ExamSlot.Class.InstitutionId;

        return await UploadAndPersistAsync(
            file,
            new StorageUploadPlan(
                ExamEvidenceBucket,
                ExamFolders(violation.Participation),
                violation.Id,
                violation.EvidencePath,
                actor,
                institutionId,
                "violationId",
                violation.Id,
                async uploadedPath =>
                {
                    violation.EvidencePath = uploadedPath;
                    await _context.SaveChangesAsync(cancellationToken);
                }),
            cancellationToken);
    }

    public async Task<StorageDownloadResult> DownloadAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        bucket = NormalizeBucket(bucket);
        path = NormalizeObjectPathForBucket(bucket, path);
        await EnsureObjectAccessAsync(actor, bucket, path, cancellationToken);

        if (UseLocalStorage)
        {
            var localPath = GetLocalPath(bucket, path);
            if (!File.Exists(localPath))
                throw new FileNotFoundException("Storage object was not found.", path);

            return new StorageDownloadResult(
                File.Open(localPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                GetContentType(path),
                Path.GetFileName(localPath));
        }

        var client = CreateSupabaseClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSupabaseObjectUrl(bucket, path, authenticated: true));
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException($"Supabase download failed: {(int)response.StatusCode} {responseBody}");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new StorageDownloadResult(
            stream,
            response.Content.Headers.ContentType?.ToString() ?? GetContentType(path),
            Path.GetFileName(path),
            response);
    }

    public async Task<StorageSignedUrlResponseDto> CreateSignedUrlAsync(
        string bucket,
        string path,
        int expiresInSeconds = IStorageService.NinetyDaySignedUrlExpiresInSeconds,
        CancellationToken cancellationToken = default)
    {
        ValidateSignedUrlExpiration(expiresInSeconds);
        var actor = await _currentUser.GetRequiredUserAsync();
        bucket = NormalizeBucket(bucket);
        path = NormalizeObjectPathForBucket(bucket, path);
        await EnsureObjectAccessAsync(actor, bucket, path, cancellationToken);

        return await CreateObjectUrlAsync(bucket, path, expiresInSeconds, cancellationToken);
    }

    private async Task<StorageSignedUrlResponseDto> CreateObjectUrlAsync(
        string bucket,
        string path,
        int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        ValidateSignedUrlExpiration(expiresInSeconds);
        bucket = NormalizeBucket(bucket);
        path = NormalizeObjectPathForBucket(bucket, path);

        if (UseLocalStorage)
        {
            var localPath = GetLocalPath(bucket, path);
            if (!File.Exists(localPath))
                throw new FileNotFoundException("Storage object was not found.", path);

            return new StorageSignedUrlResponseDto(
                bucket,
                path,
                $"/api/storage/file?bucket={Uri.EscapeDataString(bucket)}&path={Uri.EscapeDataString(path)}",
                expiresInSeconds,
                "local");
        }

        if (await IsSupabaseBucketPublicAsync(bucket, cancellationToken))
        {
            return new StorageSignedUrlResponseDto(
                bucket,
                path,
                BuildSupabasePublicObjectUrl(bucket, path),
                0,
                "supabase-public");
        }

        var client = CreateSupabaseClient();
        var body = JsonSerializer.Serialize(new { expiresIn = expiresInSeconds });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(BuildSupabaseSignUrl(bucket, path), content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase signed URL failed: {(int)response.StatusCode} {responseBody}");

        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;
        var signedUrl = root.TryGetProperty("signedURL", out var signedUrlProperty)
            ? signedUrlProperty.GetString()
            : root.TryGetProperty("signedUrl", out var signedUrlCamel)
                ? signedUrlCamel.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(signedUrl))
            throw new InvalidOperationException("Supabase did not return a signed URL.");

        if (signedUrl.StartsWith('/'))
            signedUrl = $"{SupabaseUrl}/storage/v1{signedUrl}";

        return new StorageSignedUrlResponseDto(bucket, path, signedUrl, expiresInSeconds, "supabase-signed");
    }

    public async Task DeleteAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken = default)
    {
        var actor = await _currentUser.GetRequiredUserAsync();
        bucket = NormalizeBucket(bucket);
        path = NormalizeObjectPathForBucket(bucket, path);
        var access = await EnsureObjectAccessAsync(actor, bucket, path, cancellationToken);

        await DeleteObjectCoreAsync(bucket, path, cancellationToken);
        await ClearMetadataReferenceAsync(bucket, path, cancellationToken);

        var payload = new StorageObjectEvent(bucket, path, $"{bucket}/{path}", null, null, DateTime.UtcNow);
        await DispatchStorageChangedAsync("deleted", payload, actor, access.InstitutionId, cancellationToken);
        _logger.LogInformation(
            "Storage delete by {UserId} ({Role}) at {DeletedAt}: {Bucket}/{Path}",
            actor.Id,
            actor.Role,
            DateTime.UtcNow,
            bucket,
            path);
    }

    public async Task<object> CleanupLocalTempAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "tmp");
        if (!Directory.Exists(root))
            return new { deleted = 0, root };

        var cutoff = DateTime.UtcNow.Subtract(maxAge);
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc > cutoff)
                continue;

            info.Delete();
            deleted++;
        }

        var result = new { deleted, root, maxAge = maxAge.ToString() };
        if (deleted > 0)
        {
            var payload = new
            {
                action = "cleanup",
                result,
                changedAt = DateTime.UtcNow
            };

            await _realtime.PushDashboardSystemAsync(HubEvents.StorageObjectChanged, payload, cancellationToken);
            await _realtime.PublishDataChangedAsync(
                "storage",
                "cleanup",
                data: payload,
                cancellationToken: cancellationToken);
        }

        return result;
    }

    private async Task<StorageUploadResponseDto> UploadAndPersistAsync(
        IFormFile file,
        StorageUploadPlan plan,
        CancellationToken cancellationToken)
    {
        var result = await UploadBusinessFileAsync(
            file,
            plan.Bucket,
            plan.Folders,
            plan.NameOwnerId,
            upsert: false,
            cancellationToken);
        var storageUrl = (await CreateObjectUrlAsync(
            result.Bucket,
            result.Path,
            IStorageService.NinetyDaySignedUrlExpiresInSeconds,
            cancellationToken)).SignedUrl;
        result = result with { Url = storageUrl };

        try
        {
            await plan.PersistMetadata(result.Path);
        }
        catch
        {
            await TryDeleteObjectAsync(plan.Bucket, result.Path, cancellationToken);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(plan.PreviousPath))
        {
            var normalizedPreviousPath = NormalizeObjectPathForBucket(plan.Bucket, plan.PreviousPath);
            if (!string.Equals(normalizedPreviousPath, result.Path, StringComparison.OrdinalIgnoreCase))
                await TryDeleteObjectAsync(plan.Bucket, normalizedPreviousPath, cancellationToken);
        }

        var eventData = new
        {
            result.Bucket,
            result.Path,
            result.FilePath,
            result.Size,
            result.ContentType,
            result.UploadedAt,
            url = result.Url,
            referenceName = plan.ReferenceName,
            referenceId = plan.ReferenceId
        };

        await DispatchStorageChangedAsync("uploaded", eventData, plan.Actor, plan.InstitutionId, cancellationToken);
        LogUpload(plan.Actor, result, file.FileName);
        return result;
    }

    private async Task<StorageUploadResponseDto> UploadBusinessFileAsync(
        IFormFile file,
        string bucket,
        IEnumerable<string> folders,
        Guid nameOwnerId,
        bool upsert,
        CancellationToken cancellationToken)
    {
        bucket = NormalizeBucket(bucket);
        var extension = await ValidateFileAsync(file, GetPolicy(bucket), cancellationToken);
        var path = BuildBusinessPath(folders, nameOwnerId, extension);
        return await UploadObjectAsync(file, bucket, path, upsert, cancellationToken, validateFile: false);
    }

    private async Task<StorageUploadResponseDto> UploadObjectAsync(
        IFormFile file,
        string bucket,
        string path,
        bool upsert,
        CancellationToken cancellationToken,
        bool validateFile = true)
    {
        bucket = NormalizeBucket(bucket);
        path = NormalizeStoragePath(path);
        if (validateFile)
            await ValidateFileAsync(file, GetPolicy(bucket), cancellationToken);

        var uploadedAt = DateTime.UtcNow;
        if (UseLocalStorage)
        {
            var localPath = GetLocalPath(bucket, path);
            if (!upsert && File.Exists(localPath))
                throw new InvalidOperationException("Storage object already exists.");

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await using var output = File.Create(localPath);
            await file.CopyToAsync(output, cancellationToken);

            return new StorageUploadResponseDto(
                bucket,
                path,
                $"{bucket}/{path}",
                file.Length,
                NormalizeContentType(file.ContentType, path),
                "local",
                uploadedAt);
        }

        var client = CreateSupabaseClient();
        await using var input = file.OpenReadStream();
        using var content = new StreamContent(input);
        content.Headers.ContentType = new MediaTypeHeaderValue(NormalizeContentType(file.ContentType, path));

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildSupabaseObjectUrl(bucket, path));
        request.Headers.TryAddWithoutValidation("x-upsert", upsert ? "true" : "false");
        request.Content = content;

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase upload failed: {(int)response.StatusCode} {responseBody}");

        return new StorageUploadResponseDto(
            bucket,
            path,
            $"{bucket}/{path}",
            file.Length,
            NormalizeContentType(file.ContentType, path),
            "supabase",
            uploadedAt);
    }

    private async Task<string> ValidateFileAsync(
        IFormFile file,
        StoragePolicy policy,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > policy.MaxBytes)
            throw new InvalidOperationException(
                $"File exceeds the maximum allowed size of {policy.MaxBytes / 1024 / 1024} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!policy.Extensions.Contains(extension))
            throw new InvalidOperationException($"Unsupported file extension: {extension}.");

        var contentType = file.ContentType?.Trim();
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
            !policy.ContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException($"Unsupported content type: {contentType}.");
        }

        var header = new byte[16];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!HasExpectedSignature(extension, header.AsSpan(0, read)))
            throw new InvalidOperationException("File signature does not match its extension.");

        return extension;
    }

    private async Task<StorageAccess> EnsureObjectAccessAsync(
        User actor,
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        var storedPath = path;
        var fullPath = $"{bucket}/{path}";

        switch (bucket)
        {
            case BiometricFacesBucket:
                {
                    var datum = await _context.BiometricData
                        .AsNoTracking()
                        .Include(b => b.User)
                        .FirstOrDefaultAsync(
                            b => b.FaceImageUrl == storedPath ||
                                 b.FaceImageUrl == fullPath,
                            cancellationToken);
                    if (datum != null)
                    {
                        EnsureBiometricAccess(actor, datum.User);
                        return new StorageAccess(datum.User.InstitutionId);
                    }
                    break;
                }
            case AttendanceSnapshotsBucket:
                {
                    var record = await _context.AttendanceRecords
                        .AsNoTracking()
                        .Include(r => r.Session)
                        .ThenInclude(s => s.Class)
                        .FirstOrDefaultAsync(
                            r => r.SnapshotPath == storedPath ||
                                 r.SnapshotPath == fullPath,
                            cancellationToken);
                    if (record != null)
                    {
                        EnsureAttendanceRecordAccess(actor, record);
                        return new StorageAccess(record.Session.Class.InstitutionId);
                    }
                    break;
                }
            case AttendanceVideosBucket:
                {
                    var session = await _context.AttendanceSessions
                        .AsNoTracking()
                        .Include(s => s.Class)
                        .FirstOrDefaultAsync(
                            s => s.VideoPath == storedPath ||
                                 s.VideoPath == fullPath,
                            cancellationToken);
                    if (session != null)
                    {
                        EnsureClassStaffAccess(actor, session.Class);
                        return new StorageAccess(session.Class.InstitutionId);
                    }
                    break;
                }
            case ExamIdentityBucket:
            case ExamRecordingsBucket:
                {
                    var participation = await _context.ExamParticipations
                        .AsNoTracking()
                        .Include(p => p.ExamSlot)
                        .ThenInclude(e => e.Class)
                        .FirstOrDefaultAsync(
                            p => bucket == ExamIdentityBucket
                                ? p.IdentitySnapshotPath == storedPath ||
                                  p.IdentitySnapshotPath == fullPath
                                : p.RecordingVideoPath == storedPath ||
                                  p.RecordingVideoPath == fullPath,
                            cancellationToken);
                    if (participation != null)
                    {
                        EnsureParticipationAccess(actor, participation);
                        return new StorageAccess(participation.ExamSlot.Class.InstitutionId);
                    }
                    break;
                }
            case ExamEvidenceBucket:
                {
                    var violation = await _context.ViolationLogs
                        .AsNoTracking()
                        .Include(v => v.Participation)
                        .ThenInclude(p => p.ExamSlot)
                        .ThenInclude(e => e.Class)
                        .FirstOrDefaultAsync(
                            v => v.EvidencePath == storedPath ||
                                 v.EvidencePath == fullPath,
                            cancellationToken);
                    if (violation != null)
                    {
                        EnsureViolationAccess(actor, violation);
                        return new StorageAccess(violation.Participation.ExamSlot.Class.InstitutionId);
                    }
                    break;
                }
        }

        if (actor.Role == AppRole.SuperAdmin)
            return new StorageAccess(null);

        throw new UnauthorizedAccessException("The storage object is not linked to a record you can access.");
    }

    private async Task ClearMetadataReferenceAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        var fullPath = $"{bucket}/{path}";

        switch (bucket)
        {
            case BiometricFacesBucket:
                {
                    var items = await _context.BiometricData
                        .Where(b => b.FaceImageUrl == path ||
                                    b.FaceImageUrl == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                    {
                        item.FaceImageUrl = null;
                        item.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
                }
            case AttendanceSnapshotsBucket:
                {
                    var items = await _context.AttendanceRecords
                        .Where(r => r.SnapshotPath == path ||
                                    r.SnapshotPath == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                        item.SnapshotPath = null;
                    break;
                }
            case AttendanceVideosBucket:
                {
                    var items = await _context.AttendanceSessions
                        .Where(s => s.VideoPath == path ||
                                    s.VideoPath == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                    {
                        item.VideoPath = null;
                        item.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
                }
            case ExamIdentityBucket:
                {
                    var items = await _context.ExamParticipations
                        .Where(p => p.IdentitySnapshotPath == path ||
                                    p.IdentitySnapshotPath == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                        item.IdentitySnapshotPath = null;
                    break;
                }
            case ExamRecordingsBucket:
                {
                    var items = await _context.ExamParticipations
                        .Where(p => p.RecordingVideoPath == path ||
                                    p.RecordingVideoPath == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                        item.RecordingVideoPath = null;
                    break;
                }
            case ExamEvidenceBucket:
                {
                    var items = await _context.ViolationLogs
                        .Where(v => v.EvidencePath == path ||
                                    v.EvidencePath == fullPath)
                        .ToListAsync(cancellationToken);
                    foreach (var item in items)
                        item.EvidencePath = null;
                    break;
                }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExamParticipation> GetParticipationAsync(
        Guid participationId,
        CancellationToken cancellationToken) =>
        await _context.ExamParticipations
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .ThenInclude(c => c.Institution)
            .FirstOrDefaultAsync(p => p.Id == participationId, cancellationToken)
        ?? throw new InvalidOperationException("Exam participation was not found.");

    private static void EnsureBiometricAccess(User actor, User owner)
    {
        if (actor.Role == AppRole.SuperAdmin)
            return;

        if (actor.Role == AppRole.Student && actor.Id == owner.Id)
            return;

        if (actor.Role == AppRole.SchoolAdmin &&
            actor.InstitutionId.HasValue &&
            actor.InstitutionId == owner.InstitutionId)
        {
            return;
        }

        throw new UnauthorizedAccessException("You do not have access to this biometric file.");
    }

    private static void EnsureTemporaryBiometricUpload(BiometricDatum datum)
    {
        // ponytail: review-only raw face image; add an audit table if permanent retention becomes required.
        if (datum.BioRequestId == null || datum.BioRequest?.Status != BiometricReqStatus.Pending)
            throw new InvalidOperationException("Raw biometric images can only be uploaded for a pending biometric request.");
    }

    private static void EnsureAttendanceRecordAccess(User actor, AttendanceRecord record)
    {
        if (actor.Role == AppRole.Student && actor.Id == record.StudentId)
            return;

        EnsureClassStaffAccess(actor, record.Session.Class);
    }

    private static void EnsureParticipationAccess(User actor, ExamParticipation participation)
    {
        if (actor.Role == AppRole.Student && actor.Id == participation.StudentId)
            return;

        EnsureClassStaffAccess(actor, participation.ExamSlot.Class);
    }

    private static void EnsureViolationAccess(User actor, ViolationLog violation)
    {
        if (actor.Role == AppRole.Student && actor.Id == violation.Participation.StudentId)
            return;

        EnsureClassStaffAccess(actor, violation.Participation.ExamSlot.Class);
    }

    private static void EnsureClassStaffAccess(User actor, Class cls)
    {
        if (actor.Role == AppRole.SuperAdmin)
            return;

        if (actor.Role == AppRole.SchoolAdmin && actor.InstitutionId == cls.InstitutionId)
            return;

        if (actor.Role == AppRole.Lecturer &&
            actor.InstitutionId == cls.InstitutionId &&
            actor.Id == cls.LecturerId)
        {
            return;
        }

        throw new UnauthorizedAccessException("You do not have access to this class storage.");
    }

    private static Guid RequireInstitution(User user) =>
        user.InstitutionId
        ?? throw new InvalidOperationException("The target user is not assigned to an institution.");

    private static string[] ExamFolders(ExamParticipation participation) =>
        [
            participation.ExamSlot.Class.Institution.Name,
            participation.ExamSlot.ExamName,
            participation.Student.FullName
        ];

    private bool UseLocalStorage =>
        string.IsNullOrWhiteSpace(SupabaseUrl) || string.IsNullOrWhiteSpace(SupabaseKey);

    private string SupabaseUrl =>
        _configuration["Supabase:Url"]?.TrimEnd('/') ?? string.Empty;

    private string SupabaseKey =>
        _configuration["Supabase:ServiceRoleKey"]
        ?? _configuration["Supabase:Key"]
        ?? string.Empty;

    private HttpClient CreateSupabaseClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(StorageService));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseKey);
        client.DefaultRequestHeaders.TryAddWithoutValidation("apikey", SupabaseKey);
        return client;
    }

    private string BuildSupabaseObjectUrl(string bucket, string path, bool authenticated = false)
    {
        var prefix = authenticated ? "object/authenticated" : "object";
        return $"{SupabaseUrl}/storage/v1/{prefix}/{Uri.EscapeDataString(bucket)}/{EscapeObjectPath(path)}";
    }

    private string BuildSupabasePublicObjectUrl(string bucket, string path) =>
        $"{SupabaseUrl}/storage/v1/object/public/{Uri.EscapeDataString(bucket)}/{EscapeObjectPath(path)}";

    private string BuildSupabaseSignUrl(string bucket, string path) =>
        $"{SupabaseUrl}/storage/v1/object/sign/{Uri.EscapeDataString(bucket)}/{EscapeObjectPath(path)}";

    private async Task<bool> IsSupabaseBucketPublicAsync(
        string bucket,
        CancellationToken cancellationToken)
    {
        var client = CreateSupabaseClient();
        using var response = await client.GetAsync(
            $"{SupabaseUrl}/storage/v1/bucket/{Uri.EscapeDataString(bucket)}",
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase bucket lookup failed: {(int)response.StatusCode} {responseBody}");

        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.TryGetProperty("public", out var isPublic) &&
               isPublic.ValueKind == JsonValueKind.True;
    }

    private string GetLocalPath(string bucket, string path)
    {
        bucket = NormalizeBucket(bucket);
        path = NormalizeStoragePath(path);

        var root = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            bucket));
        var localPath = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!localPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid local storage path.");

        return localPath;
    }

    private async Task DeleteObjectCoreAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        if (UseLocalStorage)
        {
            var localPath = GetLocalPath(bucket, path);
            if (File.Exists(localPath))
                File.Delete(localPath);
            return;
        }

        var client = CreateSupabaseClient();
        var body = JsonSerializer.Serialize(new { prefixes = new[] { path } });
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{SupabaseUrl}/storage/v1/object/{Uri.EscapeDataString(bucket)}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase delete failed: {(int)response.StatusCode} {responseBody}");
    }

    private async Task TryDeleteObjectAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await DeleteObjectCoreAsync(bucket, path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete replaced storage object {Bucket}/{Path}.", bucket, path);
        }
    }

    private async Task DispatchStorageChangedAsync(
        string action,
        object data,
        User actor,
        Guid? institutionId,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            action,
            userId = actor.Id,
            institutionId,
            data,
            changedAt = DateTime.UtcNow
        };

        var tasks = new List<Task>
        {
            _realtime.PushDashboardSystemAsync(HubEvents.StorageObjectChanged, payload, cancellationToken),
            _realtime.PublishDataChangedAsync(
                "storage",
                action,
                institutionId: institutionId,
                userId: actor.Id,
                data: payload,
                cancellationToken: cancellationToken),
            _realtime.PushUserAsync(actor.Id, HubEvents.StorageObjectChanged, payload, cancellationToken)
        };

        if (institutionId.HasValue)
        {
            tasks.Add(_realtime.PushDashboardInstitutionAsync(
                institutionId.Value,
                HubEvents.StorageObjectChanged,
                payload,
                cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private void LogUpload(User actor, StorageUploadResponseDto result, string originalFileName)
    {
        _logger.LogInformation(
            "Storage upload by {UserId} ({Role}) at {UploadedAt}: {OriginalFileName}, {Size} bytes -> {Bucket}/{Path}",
            actor.Id,
            actor.Role,
            result.UploadedAt,
            originalFileName,
            result.Size,
            result.Bucket,
            result.Path);
    }

    private static string NormalizeBucket(string bucket)
    {
        bucket = bucket?.Trim() ?? string.Empty;
        if (!Policies.ContainsKey(bucket))
            throw new InvalidOperationException("Unsupported storage bucket.");

        return Policies.Keys.First(key => string.Equals(key, bucket, StringComparison.OrdinalIgnoreCase));
    }

    private static StoragePolicy GetPolicy(string bucket) =>
        Policies.TryGetValue(bucket, out var policy)
            ? policy
            : throw new InvalidOperationException("Unsupported storage bucket.");

    private static string BuildBusinessPath(
        IEnumerable<string> folders,
        Guid nameOwnerId,
        string extension)
    {
        var normalizedFolders = folders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(NormalizeSegment)
            .ToArray();
        var fileName = $"{nameOwnerId:N}_{Guid.NewGuid():N}{extension}";

        return normalizedFolders.Length == 0
            ? fileName
            : $"{string.Join('/', normalizedFolders)}/{fileName}";
    }

    private static void ValidateSignedUrlExpiration(int expiresInSeconds)
    {
        if (expiresInSeconds is < 60 or > IStorageService.NinetyDaySignedUrlExpiresInSeconds)
        {
            throw new InvalidOperationException(
                $"expiresInSeconds must be between 60 and {IStorageService.NinetyDaySignedUrlExpiresInSeconds}.");
        }
    }

    private static string NormalizeObjectPathForBucket(string bucket, string path)
    {
        path = ExtractObjectPathFromUrl(bucket, path) ?? path;
        path = NormalizeStoragePath(path);
        var prefix = $"{bucket}/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }

    private static string? ExtractObjectPathFromUrl(string bucket, string value)
    {
        var raw = value.Trim();
        var queryIndex = raw.IndexOf('?');
        if (queryIndex >= 0)
        {
            var bucketQuery = GetQueryValue(raw[(queryIndex + 1)..], "bucket");
            var pathQuery = GetQueryValue(raw[(queryIndex + 1)..], "path");
            if (string.Equals(bucketQuery, bucket, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pathQuery))
            {
                return pathQuery;
            }

            raw = raw[..queryIndex];
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            raw = uri.AbsolutePath;

        raw = Uri.UnescapeDataString(raw);
        foreach (var marker in new[]
                 {
                     $"/object/sign/{bucket}/",
                     $"/object/public/{bucket}/",
                     $"/object/authenticated/{bucket}/",
                     $"/object/{bucket}/"
                 })
        {
            var markerIndex = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
                return raw[(markerIndex + marker.Length)..].Trim('/');
        }

        return null;
    }

    private static string? GetQueryValue(string query, string key)
    {
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(pair[0], key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1].Replace("+", " "));
        }

        return null;
    }

    private static string NormalizeStoragePath(string path)
    {
        path = path.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(path) ||
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return string.Join(
            '/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(NormalizeSegment));
    }

    private static string NormalizeSegment(string value)
    {
        var safe = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray());

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
            throw new InvalidOperationException("Invalid storage path segment.");

        return safe;
    }

    private static string EscapeObjectPath(string path) =>
        string.Join('/', NormalizeStoragePath(path).Split('/').Select(Uri.EscapeDataString));

    private static string NormalizeContentType(string? contentType, string path) =>
        string.IsNullOrWhiteSpace(contentType) ||
        string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            ? GetContentType(path)
            : contentType;

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };

    private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> header) =>
        extension switch
        {
            ".jpg" or ".jpeg" =>
                header.Length >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,
            ".png" =>
                header.Length >= 8 &&
                header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" =>
                header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header.Slice(8, 4).SequenceEqual("WEBP"u8),
            ".mp4" =>
                header.Length >= 12 &&
                header.Slice(4, 4).SequenceEqual("ftyp"u8),
            ".webm" =>
                header.Length >= 4 &&
                header[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };

    private sealed record StoragePolicy(
        long MaxBytes,
        HashSet<string> Extensions,
        HashSet<string> ContentTypes)
    {
        public static StoragePolicy Images { get; } = new(
            ImageLimit,
            new HashSet<string>([".jpg", ".jpeg", ".png", ".webp"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["image/jpeg", "image/png", "image/webp"], StringComparer.OrdinalIgnoreCase));

        public static StoragePolicy Videos { get; } = new(
            VideoLimit,
            new HashSet<string>([".mp4", ".webm"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["video/mp4", "video/webm"], StringComparer.OrdinalIgnoreCase));
    }

    private sealed record StorageAccess(Guid? InstitutionId);

    private sealed record StorageUploadPlan(
        string Bucket,
        IEnumerable<string> Folders,
        Guid NameOwnerId,
        string? PreviousPath,
        User Actor,
        Guid InstitutionId,
        string ReferenceName,
        Guid ReferenceId,
        Func<string, Task> PersistMetadata);

    private sealed record StorageObjectEvent(
        string Bucket,
        string Path,
        string FilePath,
        long? Size,
        string? ContentType,
        DateTime ChangedAt);
}
