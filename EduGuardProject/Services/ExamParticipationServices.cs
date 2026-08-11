using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace EduGuardProject.Services;

public class ExamParticipationServices : IExamParticipationService
{
    private readonly IExamParticipationRepository _repo;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;

    public ExamParticipationServices(
        IExamParticipationRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        IStorageService storage)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _storage = storage;
    }

    public async Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllExamparticipationsAsync(
        string? search, string? sort, int page, int pageSize, Guid? examSlotId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;
        var studentId = user.Role == AppRole.Student ? user.Id : (Guid?)null;

        return await _repo.GetAllAsync(search, sort, page, pageSize, institutionId, lecturerId, studentId, examSlotId);
    }

    public async Task<ExamParticipationResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null)
            return null;

        var user = await _currentUser.GetRequiredUserAsync();
        var hasAccess =
            user.Role == AppRole.SuperAdmin ||
            (user.Role == AppRole.SchoolAdmin &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId) ||
            (user.Role == AppRole.Lecturer &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId &&
             user.Id == entity.ExamSlot.Class.LecturerId) ||
            (user.Role == AppRole.Student && user.Id == entity.StudentId);

        if (!hasAccess)
            throw new UnauthorizedAccessException("Access denied.");

        return MapToResponseDto(entity);
    }

    public async Task<ExamParticipation> CreateAsync(CreateExamParticipationDto dto)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (dto.ExamSlotId == Guid.Empty)
            throw new InvalidOperationException("Exam slot id is required.");

        if (user.Role == AppRole.Student)
        {
            if (dto.StudentId == Guid.Empty)
                dto.StudentId = user.Id;
            if (dto.StudentId != user.Id)
                throw new UnauthorizedAccessException("Students can only create their own exam participation.");
        }
        if (dto.StudentId == Guid.Empty)
            throw new InvalidOperationException("Student id is required.");
        ValidateNewParticipation(dto);

        var examSlot = await _context.ExamSlots
            .AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");

        await EnsureStudentCanTakeExamAsync(dto.StudentId, examSlot);

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId != examSlot.Class.InstitutionId)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var exists = await _context.ExamParticipations.AnyAsync(p =>
            p.ExamSlotId == dto.ExamSlotId && p.StudentId == dto.StudentId);
        if (exists)
            throw new InvalidOperationException("Exam participation already exists.");

        var entity = new ExamParticipation
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            StudentId = dto.StudentId,
            BillingTransId = null,
            ActualStart = null,
            ActualEnd = null,
            Status = ParticipationStatus.Absent,
            DisqualifiedReason = null,
            RecordingVideoPath = null,
            IdentitySnapshotPath = null,
        };

        await _repo.AddAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "created");
        return entity;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateExamParticipationDto dto)
    {
        var entity = await GetManageableParticipationAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && entity.Status is ParticipationStatus.Submitted or ParticipationStatus.Disqualified)
            throw new InvalidOperationException("Submitted or disqualified exam participations cannot be edited.");
        if (user.Role == AppRole.Student && dto.Status != entity.Status)
            throw new UnauthorizedAccessException("Students cannot update exam participation status directly.");
        ValidateParticipationStatus(dto.Status);
        EnsureValidStatusTransition(entity.Status, dto.Status);
        ValidateParticipationTimes(dto.ActualStart, dto.ActualEnd);

        // update allowed fields
        entity.ActualStart = dto.ActualStart;
        entity.ActualEnd = dto.ActualEnd;
        entity.Status = dto.Status;
        entity.DisqualifiedReason = dto.DisqualifiedReason;
        entity.RecordingVideoPath = dto.RecordingVideoPath;
        entity.IdentitySnapshotPath = dto.IdentitySnapshotPath;

        await _repo.UpdateAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "updated");
        return true;
    }
    public async Task<bool> UpdateAsyncOnlyExamPartipationStatus(Guid examSlotId, UpdateExamParticipationStatusDto dto)
    {
        var entity = await GetManageableParticipationAsync(examSlotId, allowLecturer: true, allowStudent: false);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        ValidateParticipationStatus(dto.Status);
        EnsureValidStatusTransition(entity.Status, dto.Status);
        if (user.Role == AppRole.Lecturer &&
            (entity.Status != ParticipationStatus.Disqualified || dto.Status != ParticipationStatus.Submitted))
        {
            throw new InvalidOperationException("Lecturers can only change DISQUALIFIED participation to SUBMITTED.");
        }

        if (entity.Status == ParticipationStatus.Disqualified && dto.Status != ParticipationStatus.Disqualified)
            entity.DisqualifiedReason = null;
        entity.Status = dto.Status;

        await _repo.UpdateAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "status-updated");
        return true;
    }
    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await GetManageableParticipationAsync(id);
        if (entity == null) return false;
        await PublishParticipationChangedAsync(entity.Id, "deleted");
        if (!string.IsNullOrWhiteSpace(entity.IdentitySnapshotPath))
            await _storage.DeleteAsync(StorageService.ExamIdentityBucket, entity.IdentitySnapshotPath);
        if (!string.IsNullOrWhiteSpace(entity.RecordingVideoPath))
            await _storage.DeleteAsync(StorageService.ExamRecordingsBucket, entity.RecordingVideoPath);
        await _repo.DeleteAsync(entity);
        return true;
    }

    public async Task<ExamParticipationStatusResponseDto?> GetParticipationStatusAsync(Guid participationId)
    {
        var entity = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId);
        if (entity == null)
            return null;

        var user = await _currentUser.GetRequiredUserAsync();
        var hasAccess =
            user.Role == AppRole.SuperAdmin ||
            (user.Role == AppRole.SchoolAdmin &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId) ||
            (user.Role == AppRole.Lecturer &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId &&
             user.Id == entity.ExamSlot.Class.LecturerId) ||
            (user.Role == AppRole.Student && user.Id == entity.StudentId);

        if (!hasAccess)
            throw new UnauthorizedAccessException("Access denied.");

        var browserViolationCount = await _context.ViolationLogs.CountAsync(v =>
            v.ParticipationId == participationId &&
            (v.violationType == ViolationType.TabSwitch ||
             v.violationType == ViolationType.WindowBlur ||
             v.violationType == ViolationType.ExitFullscreen));

        var isTerminated = entity.Status == ParticipationStatus.Disqualified;

        return new ExamParticipationStatusResponseDto
        {
            ParticipationId = entity.Id,
            Status = MapStatusName(entity.Status),
            IsTerminated = isTerminated,
            TerminationReason = isTerminated ? entity.DisqualifiedReason : null,
            BrowserViolationCount = browserViolationCount
        };
    }

    private static string MapStatusName(ParticipationStatus status) => status switch
    {
        ParticipationStatus.Joined => "Active",
        ParticipationStatus.Disqualified => "Terminated",
        _ => status.ToString()
    };

    private async Task EnsureStudentCanTakeExamAsync(Guid studentId, ExamSlot examSlot)
    {
        var studentExists = await _context.Users.AsNoTracking().AnyAsync(u =>
            u.Id == studentId &&
            u.Role == AppRole.Student &&
            u.DeletedAt == null &&
            u.Status == UserStatus.Active);
        if (!studentExists)
            throw new InvalidOperationException("Student not found.");

        var isEnrolled = await _context.ClassEnrollments.AsNoTracking().AnyAsync(e =>
            e.ClassId == examSlot.ClassId &&
            e.StudentId == studentId &&
            e.Status == EnrollmentStatus.Active);
        if (!isEnrolled)
            throw new UnauthorizedAccessException("Student is not enrolled in this class.");

        var hasOverlap = await _context.ExamParticipations.AsNoTracking().AnyAsync(p =>
            p.StudentId == studentId &&
            p.ExamSlotId != examSlot.Id &&
            p.ExamSlot.Status != ExamSlotStatus.Cancelled &&
            p.ExamSlot.StartTime < examSlot.EndTime &&
            p.ExamSlot.EndTime > examSlot.StartTime);
        if (hasOverlap)
            throw new InvalidOperationException("Student already has another exam in this time range.");
    }

    private static void ValidateParticipationTimes(DateTime? actualStart, DateTime? actualEnd)
    {
        var now = DateTime.UtcNow;
        if (actualStart.HasValue && actualStart.Value < now)
            throw new InvalidOperationException("Actual start time cannot be in the past.");
        if (actualEnd.HasValue && actualEnd.Value < now)
            throw new InvalidOperationException("Actual end time cannot be in the past.");
        if (actualStart.HasValue && actualEnd.HasValue && actualEnd.Value <= actualStart.Value)
            throw new InvalidOperationException("Actual end time must be after actual start time.");
    }

    private static void ValidateNewParticipation(CreateExamParticipationDto dto)
    {
        ValidateParticipationStatus(dto.Status);
        if (dto.Status != ParticipationStatus.Absent)
            throw new InvalidOperationException("New exam participations must start as ABSENT.");
        if (dto.ActualStart.HasValue || dto.ActualEnd.HasValue || dto.BillingTransId.HasValue ||
            !string.IsNullOrWhiteSpace(dto.DisqualifiedReason) ||
            !string.IsNullOrWhiteSpace(dto.RecordingVideoPath) ||
            !string.IsNullOrWhiteSpace(dto.IdentitySnapshotPath))
        {
            throw new InvalidOperationException("Workflow-managed participation fields cannot be set when creating a participation.");
        }
    }

    private static void ValidateParticipationStatus(ParticipationStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new InvalidOperationException("Invalid exam participation status.");
    }

    private static void EnsureValidStatusTransition(ParticipationStatus current, ParticipationStatus next)
    {
        var isValid = current == next ||
            (current == ParticipationStatus.Joined && next is ParticipationStatus.Submitted or ParticipationStatus.Disqualified or ParticipationStatus.Left) ||
            (current == ParticipationStatus.Disqualified && next == ParticipationStatus.Submitted);

        if (!isValid)
            throw new InvalidOperationException($"Cannot change participation status from {current} to {next}.");
    }

    private async Task<ExamParticipation?> GetManageableParticipationAsync(
        Guid participationId,
        bool allowLecturer = false,
        bool allowStudent = true)
    {
        var entity = await _context.ExamParticipations
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId);
        if (entity == null)
            return null;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin)
            return entity;

        if (user.Role == AppRole.SchoolAdmin &&
            user.InstitutionId == entity.ExamSlot.Class.InstitutionId)
        {
            return entity;
        }

        if (allowLecturer &&
            user.Role == AppRole.Lecturer &&
            user.InstitutionId == entity.ExamSlot.Class.InstitutionId &&
            user.Id == entity.ExamSlot.Class.LecturerId)
        {
            return entity;
        }

        if (allowStudent && user.Role == AppRole.Student && user.Id == entity.StudentId)
            return entity;

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task PublishParticipationChangedAsync(Guid participationId, string action)
    {
        var participation = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId);

        if (participation == null)
            return;

        await _realtime.PublishDataChangedAsync(
            "exam-participations",
            action,
            institutionId: participation.ExamSlot.Class.InstitutionId,
            lecturerId: participation.ExamSlot.Class.LecturerId,
            userId: participation.StudentId,
            data: new
            {
                participationId = participation.Id,
                participation.ExamSlotId,
                participation.ExamSlot.ExamName,
                participation.StudentId,
                participation.Student.FullName,
                participation.Status,
                participation.ActualStart,
                participation.ActualEnd,
                participation.DisqualifiedReason
            });
    }

    private static ExamParticipationResponseDto MapToResponseDto(ExamParticipation entity) => new()
    {
        Id = entity.Id,
        ExamSlotId = entity.ExamSlotId,
        ExamName = entity.ExamSlot?.ExamName,
        StudentId = entity.StudentId,
        BillingTransId = entity.BillingTransId,
        ActualStart = entity.ActualStart,
        ActualEnd = entity.ActualEnd,
        Status = entity.Status,
        DisqualifiedReason = entity.DisqualifiedReason,
        RecordingVideoPath = entity.RecordingVideoPath,
        IdentitySnapshotPath = entity.IdentitySnapshotPath
    };
}
