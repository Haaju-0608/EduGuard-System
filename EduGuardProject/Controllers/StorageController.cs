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
        return await UploadCore(
            () => _storage.UploadAsync(
                request.File,
                request.Bucket,
                request.Folder,
                request.Upsert,
                cancellationToken),
            "File uploaded successfully.");
    }

    [HttpPost("biometric")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
    public async Task<IActionResult> UploadBiometric(
        [FromForm] BiometricStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadBiometricAsync(
                request.File,
                request.BiometricDataId,
                cancellationToken),
            "Biometric image uploaded successfully.");
    }

    [HttpPost("attendance")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadAttendanceSnapshot(
        [FromForm] AttendanceSnapshotStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadAttendanceSnapshotAsync(
                request.File,
                request.AttendanceRecordId,
                cancellationToken),
            "Attendance snapshot uploaded successfully.");
    }

    [HttpPost("attendance-video")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> UploadAttendanceVideo(
        [FromForm] AttendanceVideoStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadAttendanceVideoAsync(
                request.File,
                request.AttendanceSessionId,
                cancellationToken),
            "Attendance video uploaded successfully.");
    }

    [HttpPost("exam-identity")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadExamIdentity(
        [FromForm] ExamStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadExamIdentityAsync(
                request.File,
                request.ParticipationId,
                cancellationToken),
            "Exam identity image uploaded successfully.");
    }

    [HttpPost("exam-recording")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> UploadExamRecording(
        [FromForm] ExamStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadExamRecordingAsync(
                request.File,
                request.ParticipationId,
                cancellationToken),
            "Exam recording uploaded successfully.");
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(ImageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> UploadEvidence(
        [FromForm] EvidenceStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await UploadCore(
            () => _storage.UploadEvidenceAsync(
                request.File,
                request.ViolationId,
                cancellationToken),
            "Violation evidence uploaded successfully.");
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

    private async Task<IActionResult> UploadCore(
        Func<Task<StorageUploadResponseDto>> upload,
        string message)
    {
        try
        {
            var result = await upload();
            return Ok(ApiResponse<StorageUploadResponseDto>.OnSuccess(result, message));
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
