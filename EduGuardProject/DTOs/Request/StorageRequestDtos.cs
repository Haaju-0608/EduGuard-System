using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduGuardProject.DTOs.Request;

public abstract class StorageUploadFileRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}

public sealed class GenericStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public string Bucket { get; set; } = string.Empty;

    public string? Folder { get; set; }
    public bool Upsert { get; set; }
}

public sealed class BiometricStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public Guid BiometricDataId { get; set; }
}

public sealed class AttendanceSnapshotStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public Guid AttendanceRecordId { get; set; }
}

public sealed class AttendanceVideoStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public Guid AttendanceSessionId { get; set; }
}

public sealed class ExamStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public Guid ParticipationId { get; set; }
}

public sealed class EvidenceStorageUploadRequest : StorageUploadFileRequest
{
    [Required]
    public Guid ViolationId { get; set; }
}
