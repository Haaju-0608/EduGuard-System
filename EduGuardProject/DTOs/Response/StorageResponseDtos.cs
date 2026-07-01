namespace EduGuardProject.DTOs.Response;

public sealed record StorageUploadResponseDto(
    string Bucket,
    string Path,
    string FilePath,
    long Size,
    string ContentType,
    string Storage,
    DateTime UploadedAt,
    string? Url = null);

public sealed record StorageSignedUrlResponseDto(
    string Bucket,
    string Path,
    string SignedUrl,
    int ExpiresInSeconds,
    string Storage);
