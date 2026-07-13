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

    // Generic Upload File
    // Truyền dữ liệu: form-data file, bucket, folder, upsert.
    // Điều kiện: role SuperAdmin; không dùng endpoint này cho biometric raw face image.
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

    // Upload Biometric Image
    // Truyền dữ liệu: form-data file, biometricDataId.
    // Điều kiện: SuperAdmin, SchoolAdmin cùng institution hoặc Student chính chủ; biometric request phải Pending.
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

    // Upload Attendance Snapshot
    // Truyền dữ liệu: form-data file, attendanceRecordId.
    // Điều kiện: Student chính chủ attendance record; hoặc Lecturer/SchoolAdmin/SuperAdmin có quyền với class.
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

    // Upload Attendance Video
    // Truyền dữ liệu: form-data file, attendanceSessionId.
    // Điều kiện: Lecturer của class, SchoolAdmin cùng institution hoặc SuperAdmin; attendance session phải tồn tại.
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

    // Upload Exam Identity Image
    // Truyền dữ liệu: form-data file, participationId.
    // Điều kiện: Student chính chủ participation; hoặc Lecturer/SchoolAdmin/SuperAdmin có quyền với class của exam.
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

    // Upload Exam Recording
    // Truyền dữ liệu: form-data file, participationId.
    // Điều kiện: Student chính chủ participation; hoặc Lecturer/SchoolAdmin/SuperAdmin có quyền với class của exam.
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

    // Upload Violation Evidence
    // Truyền dữ liệu: form-data file, violationId.
    // Điều kiện: Lecturer của class, SchoolAdmin cùng institution hoặc SuperAdmin; violation log phải tồn tại.
    [HttpPost("evidence")]
    [RequestSizeLimit(VideoRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoRequestLimit)]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
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

    // Download Storage File
    // Truyền dữ liệu: query bucket, path.
    // Điều kiện: user đã đăng nhập và có quyền truy cập object theo owner/class/institution.
    [HttpGet("file")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public Task<IActionResult> DownloadFile(
        [FromQuery] string bucket,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        DownloadCore(bucket, path, cancellationToken);


    // Create Signed URL
    // Truyền dữ liệu: query bucket, path, expiresInSeconds.
    // Điều kiện: user đã đăng nhập và có quyền truy cập object; expiresInSeconds từ 60 đến giới hạn 90 ngày.
    [HttpPost("signed-url")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> SignedUrl(
        [FromQuery] string bucket,
        [FromQuery] string path,
        [FromQuery] int expiresInSeconds = IStorageService.NinetyDaySignedUrlExpiresInSeconds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _storage.CreateSignedUrlAsync(bucket, path, expiresInSeconds, cancellationToken);
            return Ok(ApiResponse<StorageSignedUrlResponseDto>.OnSuccess(result, "Signed URL created successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Delete Storage File
    // Truyền dữ liệu: query bucket, path.
    // Điều kiện: user đã đăng nhập và có quyền truy cập object theo owner/class/institution.
    [HttpDelete]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public Task<IActionResult> Delete(
        [FromQuery] string bucket,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        DeleteCore(bucket, path, cancellationToken);


    // Cleanup Local Temp Files
    // Truyền dữ liệu: query maxAgeHours.
    // Điều kiện: role SuperAdmin; maxAgeHours phải lớn hơn 0.
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
