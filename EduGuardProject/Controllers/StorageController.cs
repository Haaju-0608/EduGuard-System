using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/storage")]
[ApiController]
[SupabaseAuthorize]
public class StorageController : AcademicApiControllerBase
{
    private const long ImageRequestLimit = 12 * 1024 * 1024;
    private const long VideoRequestLimit = 525 * 1024 * 1024;

    private readonly IStorageService _storage;

    public StorageController(IStorageService storage)
    {
        _storage = storage;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    public async Task<IActionResult> Upload(
        [FromForm] GenericStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadAsync(
                request.File,
                request.Bucket,
                request.Folder,
                request.Upsert,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "File uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("biometric")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
    public async Task<IActionResult> UploadBiometric(
        [FromForm] BiometricStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadBiometricAsync(
                request.File,
                request.BiometricDataId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Biometric image uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("attendance")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadAttendanceSnapshot(
        [FromForm] AttendanceSnapshotStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadAttendanceSnapshotAsync(
                request.File,
                request.AttendanceRecordId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Attendance snapshot uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("attendance-video")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> UploadAttendanceVideo(
        [FromForm] AttendanceVideoStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadAttendanceVideoAsync(
                request.File,
                request.AttendanceSessionId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Attendance video uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("exam-identity")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadExamIdentity(
        [FromForm] ExamStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadExamIdentityAsync(
                request.File,
                request.ParticipationId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Exam identity image uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("exam-recording")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadExamRecording(
        [FromForm] ExamStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadExamRecordingAsync(
                request.File,
                request.ParticipationId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Exam recording uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> UploadEvidence(
        [FromForm] EvidenceStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.UploadEvidenceAsync(
                request.File,
                request.ViolationId,
                cancellationToken);
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, "Violation evidence uploaded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("file")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public Task<IActionResult> DownloadFile(
        [FromQuery] string bucket,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        DownloadCore(bucket, path, cancellationToken);


    [HttpPost("signed-url")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> SignedUrl(
        [FromQuery] string bucket,
        [FromQuery] string path,
        [FromQuery] int expiresInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.CreateSignedUrlAsync(bucket, path, expiresInSeconds, cancellationToken);
            return Ok(ApiResponse<StorageSignedUrlResponseDto>.OnSuccess(result, "Signed URL created successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public Task<IActionResult> Delete(
        [FromQuery] string bucket,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        DeleteCore(bucket, path, cancellationToken);


    [HttpPost("cleanup")]
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    public async Task<IActionResult> Cleanup(
        [FromQuery] int maxAgeHours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxAgeHours <= 0)
                throw new InvalidOperationException("maxAgeHours must be greater than 0.");

            var result = await _storage.CleanupLocalTempAsync(TimeSpan.FromHours(maxAgeHours), cancellationToken);
            return Ok(ApiResponse<object>.OnSuccess(result, "Storage cleanup completed."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    private async Task<IActionResult> DownloadCore(
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _storage.DownloadAsync(bucket, path, cancellationToken);
            if (result.Owner != null)
                HttpContext.Response.RegisterForDispose(result.Owner);

            return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: true);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    private async Task<IActionResult> DeleteCore(
        string bucket,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(bucket, path, cancellationToken);
            return Ok(ApiResponse<object>.OnSuccess(null!, "File deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
