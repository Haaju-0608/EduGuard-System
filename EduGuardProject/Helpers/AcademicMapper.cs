using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Helpers;

public static class AcademicMapper
{
    public static UserSummaryDto ToUserSummary(User? user) =>
        user == null ? null! : new UserSummaryDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            StudentCode = user.StudentCode
        };

    public static InstitutionSummaryDto ToInstitutionSummary(Institution? institution) =>
        institution == null ? null! : new InstitutionSummaryDto
        {
            Id = institution.Id,
            Name = institution.Name
        };

    public static ClassSummaryDto ToClassSummary(Class? cls) =>
        cls == null ? null! : new ClassSummaryDto
        {
            Id = cls.Id,
            CourseName = cls.CourseName,
            CourseCode = cls.CourseCode
        };

    public static async Task<ClassResponseDto> MapClassAsync(AppDbContext context, Class entity, string? expand = null)
    {
        var dto = new ClassResponseDto
        {
            Id = entity.Id,
            InstitutionId = entity.InstitutionId,
            LecturerId = entity.LecturerId,
            CourseName = entity.CourseName,
            CourseCode = entity.CourseCode,
            Semester = entity.Semester,
            AcademicYear = entity.AcademicYear,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        var parts = expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).ToHashSet();

        if (parts.Contains("institution"))
        {
            var institution = await context.Institutions.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == entity.InstitutionId);
            dto.Institution = ToInstitutionSummary(institution);
        }

        if (parts.Contains("lecturer"))
        {
            var lecturer = await context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == entity.LecturerId);
            dto.Lecturer = ToUserSummary(lecturer);
        }

        if (parts.Contains("enrollments"))
        {
            var enrollments = await context.ClassEnrollments.AsNoTracking()
                .Where(e => e.ClassId == entity.Id && e.Status != EnrollmentStatus.Dropped)
                .ToListAsync();
            dto.Enrollments = new List<ClassEnrollmentResponseDto>();
            foreach (var e in enrollments)
                dto.Enrollments.Add(await MapEnrollmentAsync(context, e, null));
        }

        return dto;
    }

    public static async Task<ClassEnrollmentResponseDto> MapEnrollmentAsync(
        AppDbContext context, ClassEnrollment entity, string? expand)
    {
        var dto = new ClassEnrollmentResponseDto
        {
            ClassId = entity.ClassId,
            StudentId = entity.StudentId,
            Status = entity.Status,
            EnrolledAt = entity.EnrolledAt
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        var parts = expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).ToHashSet();

        if (parts.Contains("class"))
        {
            var cls = await context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entity.ClassId);
            dto.Class = ToClassSummary(cls);
        }

        if (parts.Contains("student"))
        {
            var student = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.StudentId);
            dto.Student = ToUserSummary(student);
        }

        return dto;
    }

    public static async Task<AttendanceSessionResponseDto> MapSessionAsync(
        AppDbContext context, AttendanceSession entity, string? expand)
    {
        var dto = new AttendanceSessionResponseDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            CreatedBy = entity.CreatedBy,
            BillingTransId = entity.BillingTransId,
            VideoPath = entity.VideoPath,
            TotalRecognized = entity.TotalRecognized,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        var parts = expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).ToHashSet();

        if (parts.Contains("class"))
        {
            var cls = await context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entity.ClassId);
            dto.Class = ToClassSummary(cls);
        }

        if (parts.Contains("creator"))
        {
            var creator = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.CreatedBy);
            dto.Creator = ToUserSummary(creator);
        }

        if (parts.Contains("records"))
        {
            var records = await context.AttendanceRecords.AsNoTracking()
                .Where(r => r.SessionId == entity.Id)
                .ToListAsync();
            dto.Records = new List<AttendanceRecordResponseDto>();
            foreach (var r in records)
                dto.Records.Add(await MapRecordAsync(context, r, null));
        }

        return dto;
    }

    public static async Task<AttendanceRecordResponseDto> MapRecordAsync(
        AppDbContext context, AttendanceRecord entity, string? expand)
    {
        var dto = new AttendanceRecordResponseDto
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            StudentId = entity.StudentId,
            ConfidenceScore = entity.ConfidenceScore,
            SnapshotPath = entity.SnapshotPath,
            CheckinAt = entity.CheckinAt,
            AdjustedBy = entity.AdjustedBy,
            AdjustedAt = entity.AdjustedAt,
            Method = entity.Method,
            Status = entity.Status
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        var parts = expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).ToHashSet();

        if (parts.Contains("student"))
        {
            var student = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.StudentId);
            dto.Student = ToUserSummary(student);
        }

        if (parts.Contains("session"))
        {
            var session = await context.AttendanceSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == entity.SessionId);
            dto.Session = session == null ? null : new AttendanceSessionSummaryDto
            {
                Id = session.Id,
                ClassId = session.ClassId,
                Status = session.Status,
                StartTime = session.StartTime
            };
        }

        return dto;
    }

    public static async Task<BiometricRequestResponseDto> MapBiometricRequestAsync(
        AppDbContext context, BiometricRequest entity, string? expand)
    {
        string? faceImageUrl = null;
        try
        {
            faceImageUrl = await context.BiometricData
                .AsNoTracking()
                .Where(b => b.BioRequestId == entity.Id && b.FaceImageUrl != null)
                .OrderByDescending(b => b.UpdatedAt)
                .Select(b => b.FaceImageUrl)
                .FirstOrDefaultAsync();
        }
        catch
        {
            // biometric_data.bio_request_id may not exist until migration is applied
        }

        var dto = new BiometricRequestResponseDto
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            ApprovedBy = entity.ApprovedBy,
            Reason = entity.Reason,
            Status = entity.Status,
            ReviewedAt = entity.ReviewedAt,
            CreatedAt = entity.CreatedAt,
            FaceImageUrl = faceImageUrl,

            FrontImageUrl = entity.FrontImagePath,
            LeftImageUrl = entity.LeftImagePath,
            RightImageUrl = entity.RightImagePath
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        var parts = expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).ToHashSet();

        if (parts.Contains("student"))
        {
            var student = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.StudentId);
            dto.Student = ToUserSummary(student);
        }

        if (parts.Contains("approver") && entity.ApprovedBy.HasValue)
        {
            var approver = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.ApprovedBy);
            dto.Approver = ToUserSummary(approver);
        }

        return dto;
    }

    public static async Task<BiometricDatumResponseDto> MapBiometricDatumAsync(
        AppDbContext context, BiometricDatum entity, string? expand)
    {
        var dto = new BiometricDatumResponseDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            BioRequestId = entity.BioRequestId,
            ModelVersion = entity.ModelVersion,
            IsActive = entity.IsActive,
            FaceImageUrl = entity.FaceImageUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        if (string.IsNullOrWhiteSpace(expand)) return dto;

        if (expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(p => p.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entity.UserId);
            dto.User = ToUserSummary(user);
        }

        return dto;
    }
}
