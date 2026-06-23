using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduGuardProject.DTOs.Request;

public sealed class GenericStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public string Bucket { get; set; } = string.Empty;

    public string? Folder { get; set; }
    public bool Upsert { get; set; }
}

public sealed class BiometricStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid BiometricDataId { get; set; }
}

public sealed class AttendanceSnapshotStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid AttendanceRecordId { get; set; }
}

public sealed class AttendanceVideoStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid AttendanceSessionId { get; set; }
}

public sealed class ExamStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid ParticipationId { get; set; }
}

public sealed class EvidenceStorageUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid ViolationId { get; set; }
}
