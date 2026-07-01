using EduGuardProject.DTOs.Response;
using Microsoft.AspNetCore.Http;

namespace EduGuardProject.Services.IServices;

public sealed record StorageDownloadResult(
    Stream Content,
    string ContentType,
    string FileName,
    IDisposable? Owner = null);

public interface IStorageService
{
    const int NinetyDaySignedUrlExpiresInSeconds = 60 * 60 * 24 * 90;

    Task<StorageUploadResponseDto> UploadAsync(IFormFile file, string bucket, string? folder = null, bool upsert = false, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadBiometricAsync(IFormFile file, Guid biometricDataId, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadAttendanceSnapshotAsync(IFormFile file, Guid attendanceRecordId, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadAttendanceVideoAsync(IFormFile file, Guid attendanceSessionId, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadExamIdentityAsync(IFormFile file, Guid participationId, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadExamRecordingAsync(IFormFile file, Guid participationId, CancellationToken cancellationToken = default);
    Task<StorageUploadResponseDto> UploadEvidenceAsync(IFormFile file, Guid violationId, CancellationToken cancellationToken = default);
    Task<StorageDownloadResult> DownloadAsync(string bucket, string path, CancellationToken cancellationToken = default);
    Task<StorageSignedUrlResponseDto> CreateSignedUrlAsync(string bucket, string path, int expiresInSeconds = NinetyDaySignedUrlExpiresInSeconds, CancellationToken cancellationToken = default);
    Task DeleteAsync(string bucket, string path, CancellationToken cancellationToken = default);
    Task<object> CleanupLocalTempAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}
