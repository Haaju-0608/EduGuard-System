using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using ClosedXML.Excel;
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
    private readonly IProctoringSettingsService _proctoringSettings;

    public ExamParticipationServices(
        IExamParticipationRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        IProctoringSettingsService proctoringSettings)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _storage = storage;
        _proctoringSettings = proctoringSettings;
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
        EnsureExamCanAcceptParticipants(examSlot);

        // Snapshot the proctoring rules in effect right now, once, so they stay fixed for this
        // participation's whole exam even if a SchoolAdmin changes the settings mid-exam.
        var proctoringSettings = await _proctoringSettings.GetEffectiveAsync(examSlot.Class.InstitutionId);

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
            MaxAiViolationCountSnapshot = proctoringSettings.MaxAiViolationCount,
            CooldownSecondsSnapshot = proctoringSettings.CooldownSeconds,
            AllowConsecutiveSameTypeSnapshot = proctoringSettings.AllowConsecutiveSameType,
            AiNotifyThresholdSnapshot = proctoringSettings.AiNotifyThreshold,
            BrowserNotifyThresholdSnapshot = proctoringSettings.BrowserNotifyThreshold,
        };

        await _repo.AddAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "created");
        return entity;
    }

    public async Task<ImportExamParticipantsResponseDto> ImportFromExcelAsync(
        Guid examSlotId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Excel file is required.");
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("Excel file must not exceed 5 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .xlsx files are supported.");

        var examSlot = await _context.ExamSlots.AsNoTracking()
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == examSlotId, cancellationToken)
            ?? throw new InvalidOperationException("Exam slot not found.");
        EnsureExamCanAcceptParticipants(examSlot);
        await EnsureCanManageExamAsync(examSlot);

        await using var stream = file.OpenReadStream();
        var rows = ReadExamParticipantRows(stream);
        if (rows.Count == 0)
            throw new ArgumentException("The Excel file does not contain any student rows.");
        if (rows.Count > 500)
            throw new ArgumentException("A single import is limited to 500 student rows.");

        var studentCodes = rows.Select(r => r.StudentCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var students = await _context.Users.AsNoTracking()
            .Where(u => u.InstitutionId == examSlot.Class.InstitutionId &&
                        u.Role == AppRole.Student &&
                        u.Status == UserStatus.Active &&
                        u.DeletedAt == null &&
                        u.StudentCode != null &&
                        studentCodes.Contains(u.StudentCode))
            .ToListAsync(cancellationToken);
        var studentsByCode = students.ToDictionary(u => u.StudentCode!, StringComparer.OrdinalIgnoreCase);
        var studentIds = students.Select(u => u.Id).ToList();
        var enrolledStudentIds = (await _context.ClassEnrollments.AsNoTracking()
            .Where(e => e.ClassId == examSlot.ClassId &&
                        e.Status == EnrollmentStatus.Active &&
                        studentIds.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var existingStudentIds = (await _context.ExamParticipations.AsNoTracking()
            .Where(p => p.ExamSlotId == examSlotId && studentIds.Contains(p.StudentId))
            .Select(p => p.StudentId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var conflictingStudentIds = (await _context.ExamParticipations.AsNoTracking()
            .Where(p => studentIds.Contains(p.StudentId) &&
                        p.ExamSlotId != examSlotId &&
                        p.ExamSlot.Status != ExamSlotStatus.Cancelled &&
                        p.ExamSlot.StartTime < examSlot.EndTime &&
                        p.ExamSlot.EndTime > examSlot.StartTime)
            .Select(p => p.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var result = new ImportExamParticipantsResponseDto { Total = rows.Count };
        var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var participants = new List<ExamParticipation>();
        foreach (var row in rows)
        {
            var rowResult = new ImportExamParticipantRowResultDto
            {
                Row = row.Row,
                StudentCode = row.StudentCode,
                FullName = row.FullName
            };

            if (!importedCodes.Add(row.StudentCode))
                rowResult.Error = "StudentCode is duplicated in the Excel file.";
            else if (!studentsByCode.TryGetValue(row.StudentCode, out var student))
                rowResult.Error = "Active student was not found in this institution.";
            else if (!string.Equals(student.FullName.Trim(), row.FullName, StringComparison.OrdinalIgnoreCase))
                rowResult.Error = "FullName does not match the student code.";
            else if (!enrolledStudentIds.Contains(student.Id))
                rowResult.Error = "Student is not actively enrolled in this exam's class.";
            else if (existingStudentIds.Contains(student.Id))
                rowResult.Error = "Student already has an exam participation.";
            else if (conflictingStudentIds.Contains(student.Id))
                rowResult.Error = "Student has another exam in the same time range.";
            else
            {
                participants.Add(new ExamParticipation
                {
                    Id = Guid.NewGuid(),
                    ExamSlotId = examSlotId,
                    StudentId = student.Id,
                    Status = ParticipationStatus.Absent
                });
                rowResult.Success = true;
                result.Succeeded++;
            }

            if (!rowResult.Success) result.Failed++;
            result.Results.Add(rowResult);
        }

        if (participants.Count > 0)
        {
            await _repo.AddRangeAsync(participants);
            await _realtime.PublishDataChangedAsync(
                "exam-participations", "bulk-imported",
                institutionId: examSlot.Class.InstitutionId,
                lecturerId: examSlot.Class.LecturerId,
                data: new { examSlotId, result.Succeeded, result.Failed });
        }

        return result;
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
        EnsureValidStatusTransition(entity.Status, dto.Status, entity.ExamSlot.EndTime, DateTime.UtcNow);
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
        var utcNow = DateTime.UtcNow;
        var targetStatus = ResolveRestoreStatus(entity.Status, dto.Status, entity.ExamSlot.EndTime, utcNow);
        ValidateParticipationStatus(targetStatus);
        EnsureValidStatusTransition(entity.Status, targetStatus, entity.ExamSlot.EndTime, utcNow);
        if (user.Role == AppRole.Lecturer &&
            (entity.Status != ParticipationStatus.Disqualified || targetStatus is not (ParticipationStatus.Joined or ParticipationStatus.Submitted)))
        {
            throw new InvalidOperationException("Lecturers can only restore a DISQUALIFIED participation.");
        }

        if (entity.Status == ParticipationStatus.Disqualified && targetStatus != ParticipationStatus.Disqualified)
            entity.DisqualifiedReason = null;
        entity.Status = targetStatus;

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

        var aiViolationCount = await _context.ViolationLogs.CountAsync(v =>
            v.ParticipationId == participationId &&
            v.violationType != ViolationType.TabSwitch &&
            v.violationType != ViolationType.WindowBlur &&
            v.violationType != ViolationType.ExitFullscreen);

        var isTerminated = entity.Status == ParticipationStatus.Disqualified;

        return new ExamParticipationStatusResponseDto
        {
            ParticipationId = entity.Id,
            Status = MapStatusName(entity.Status),
            IsTerminated = isTerminated,
            TerminationReason = isTerminated ? entity.DisqualifiedReason : null,
            BrowserViolationCount = browserViolationCount,
            AiViolationCount = aiViolationCount
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

    private static void EnsureExamCanAcceptParticipants(ExamSlot examSlot)
    {
        if (examSlot.Status == ExamSlotStatus.Cancelled)
            throw new InvalidOperationException("Cannot add students to a cancelled exam.");
    }

    private async Task EnsureCanManageExamAsync(ExamSlot examSlot)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var canManage = user.Role == AppRole.SuperAdmin ||
            (user.Role == AppRole.SchoolAdmin && user.InstitutionId == examSlot.Class.InstitutionId) ||
            (user.Role == AppRole.Lecturer &&
             user.InstitutionId == examSlot.Class.InstitutionId &&
             user.Id == examSlot.Class.LecturerId);
        if (!canManage)
            throw new UnauthorizedAccessException("Access denied.");
    }

    private static List<ImportExamParticipantRow> ReadExamParticipantRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook does not contain a worksheet.");
        var headerRow = sheet.FirstRowUsed()
            ?? throw new ArgumentException("The workbook is empty.");
        var headers = headerRow.CellsUsed().ToDictionary(
            cell => NormalizeExcelHeader(cell.GetString()),
            cell => cell.Address.ColumnNumber);
        var requiredHeaders = new[] { "studentcode", "fullname" };
        var missingHeaders = requiredHeaders.Where(header => !headers.ContainsKey(header)).ToList();
        if (missingHeaders.Count > 0)
            throw new ArgumentException($"Missing required columns: {string.Join(", ", missingHeaders)}.");

        var rows = new List<ImportExamParticipantRow>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var studentCode = row.Cell(headers["studentcode"]).GetFormattedString().Trim();
            var fullName = row.Cell(headers["fullname"]).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(fullName))
                continue;
            if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException($"Row {row.RowNumber()} must include StudentCode and FullName.");

            rows.Add(new ImportExamParticipantRow(row.RowNumber(), studentCode, fullName));
        }

        return rows;
    }

    private static string NormalizeExcelHeader(string value) =>
        value.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant();

    private sealed record ImportExamParticipantRow(int Row, string StudentCode, string FullName);

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

    private static void EnsureValidStatusTransition(
        ParticipationStatus current,
        ParticipationStatus next,
        DateTime examEndTime,
        DateTime utcNow)
    {
        var isValid = current == next ||
            (current == ParticipationStatus.Joined && next is ParticipationStatus.Submitted or ParticipationStatus.Disqualified or ParticipationStatus.Left) ||
            (current == ParticipationStatus.Disqualified &&
             (next == ParticipationStatus.Submitted ||
              (next == ParticipationStatus.Joined && utcNow < examEndTime)));

        if (!isValid)
            throw new InvalidOperationException($"Cannot change participation status from {current} to {next}.");
    }

    private static ParticipationStatus ResolveRestoreStatus(
        ParticipationStatus current,
        ParticipationStatus requested,
        DateTime examEndTime,
        DateTime utcNow) =>
        current == ParticipationStatus.Disqualified && requested is ParticipationStatus.Joined or ParticipationStatus.Submitted
            ? utcNow < examEndTime ? ParticipationStatus.Joined : ParticipationStatus.Submitted
            : requested;

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
