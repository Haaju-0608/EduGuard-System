using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AttendanceReportResponseDto> GetAttendanceReportAsync(Guid? institutionId, Guid? classId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        var includeIds = role != AppRole.SchoolAdmin;
        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var classes = _context.Classes.AsNoTracking().Where(c => c.DeletedAt == null);
        if (institutionId.HasValue)
        {
            await _currentUser.EnsureInstitutionAccessAsync(institutionId.Value);
            classes = classes.Where(c => c.InstitutionId == institutionId.Value);
        }

        if (role == AppRole.SchoolAdmin)
        {
            classes = classes.Where(c => c.InstitutionId == user.InstitutionId);
        }
        else if (role == AppRole.Lecturer)
        {
            classes = classes.Where(c => c.LecturerId == user.Id);
        }
        else if (role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (classId.HasValue)
            classes = classes.Where(c => c.Id == classId.Value);

        var classIds = await classes.Select(c => c.Id).ToListAsync(cancellationToken);
        var sessions = await _context.AttendanceSessions.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.StartTime >= start && s.StartTime <= end)
            .OrderByDescending(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.Class.InstitutionId,
                InstitutionName = s.Class.Institution.Name,
                s.ClassId,
                ClassName = s.Class.CourseName,
                s.Class.CourseCode,
                s.StartTime,
                s.EndTime,
                s.Status,
                s.TotalRecognized,
                totalStudents = _context.ClassEnrollments.Count(e => e.ClassId == s.ClassId && e.Status == EnrollmentStatus.Active),
                records = _context.AttendanceRecords.Count(r => r.SessionId == s.Id)
            })
            .ToListAsync(cancellationToken);

        return new AttendanceReportResponseDto
        {
            Filters = new AttendanceReportFilterDto
            {
                InstitutionId = includeIds ? institutionId : null,
                ClassId = includeIds ? classId : null,
                From = from,
                To = to
            },
            Summary = new AttendanceReportSummaryDto
            {
                Sessions = sessions.Count,
                Completed = sessions.Count(s => s.Status == SessionStatus.Completed),
                TotalRecognized = sessions.Sum(s => s.TotalRecognized),
                AverageRecognitionRate = sessions.Count == 0
                    ? 0
                    : sessions.Average(s => s.totalStudents == 0 ? 0 : (double)s.TotalRecognized / s.totalStudents)
            },
            Items = sessions.Select(s => new AttendanceReportItemDto
            {
                Id = includeIds ? s.Id : null,
                InstitutionId = includeIds ? s.InstitutionId : null,
                InstitutionName = s.InstitutionName,
                ClassId = s.ClassId,
                ClassName = s.ClassName,
                CourseCode = s.CourseCode,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status,
                TotalRecognized = s.TotalRecognized,
                TotalStudents = s.totalStudents,
                Records = s.records
            }).ToList()
        };
    }

    public async Task<ViolationReportResponseDto> GetViolationReportAsync(Guid? institutionId, Guid? examSlotId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        var includeIds = role != AppRole.SchoolAdmin;
        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var query = _context.ViolationLogs.AsNoTracking()
            .Include(v => v.Participation)
            .ThenInclude(p => p.Student)
            .Include(v => v.Participation)
            .ThenInclude(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .Where(v => v.RecordedAt >= start && v.RecordedAt <= end);

        if (institutionId.HasValue)
        {
            await _currentUser.EnsureInstitutionAccessAsync(institutionId.Value);
            query = query.Where(v => v.Participation.ExamSlot.Class.InstitutionId == institutionId.Value);
        }

        if (role == AppRole.SchoolAdmin)
        {
            query = query.Where(v => v.Participation.ExamSlot.Class.InstitutionId == user.InstitutionId);
        }
        else if (role == AppRole.Lecturer)
        {
            query = query.Where(v =>
                v.Participation.ExamSlot.Class.LecturerId == user.Id ||
                (examSlotId.HasValue && v.Participation.ExamSlot.ProctorId == user.Id));
        }
        else if (role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (examSlotId.HasValue)
            query = query.Where(v => v.Participation.ExamSlotId == examSlotId.Value);

        var items = await query
            .OrderByDescending(v => v.RecordedAt)
            .Select(v => new
            {
                violationId = v.Id,
                v.ParticipationId,
                v.Participation.ExamSlot.Class.InstitutionId,
                InstitutionName = v.Participation.ExamSlot.Class.Institution.Name,
                v.Participation.ExamSlot.ClassId,
                ClassName = v.Participation.ExamSlot.Class.CourseName,
                v.Participation.ExamSlotId,
                v.Participation.ExamSlot.ExamName,
                v.Participation.StudentId,
                studentName = v.Participation.Student.FullName,
                type = v.violationType,
                severity = v.severity,
                v.AiConfidence,
                v.EvidencePath,
                v.IsReviewed,
                v.RecordedAt
            })
            .ToListAsync(cancellationToken);

        return new ViolationReportResponseDto
        {
            Filters = new ViolationReportFilterDto
            {
                InstitutionId = includeIds ? institutionId : null,
                ExamSlotId = includeIds ? examSlotId : null,
                From = from,
                To = to
            },
            Summary = new ViolationReportSummaryDto
            {
                Total = items.Count,
                Reviewed = items.Count(i => i.IsReviewed),
                ByType = items.GroupBy(i => i.type)
                    .Select(g => new ViolationTypeCountDto { Type = g.Key.ToString(), Count = g.Count() })
                    .ToList(),
                BySeverity = items.GroupBy(i => i.severity)
                    .Select(g => new ViolationSeverityCountDto { Severity = g.Key.ToString(), Count = g.Count() })
                    .ToList()
            },
            Items = items.Select(i => new ViolationReportItemDto
            {
                ViolationId = includeIds ? i.violationId : null,
                ParticipationId = includeIds ? i.ParticipationId : null,
                InstitutionId = includeIds ? i.InstitutionId : null,
                InstitutionName = i.InstitutionName,
                ClassId = includeIds ? i.ClassId : null,
                ClassName = i.ClassName,
                ExamSlotId = includeIds ? i.ExamSlotId : null,
                ExamName = i.ExamName,
                StudentId = includeIds ? i.StudentId : null,
                StudentName = i.studentName,
                Type = i.type,
                Severity = i.severity,
                AiConfidence = i.AiConfidence,
                EvidencePath = i.EvidencePath,
                IsReviewed = i.IsReviewed,
                RecordedAt = i.RecordedAt
            }).ToList()
        };
    }

    public async Task<WalletReportResponseDto> GetWalletReportAsync(Guid? institutionId, Guid? walletId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        var includeIds = role != AppRole.SchoolAdmin;
        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var wallets = _context.Wallets.AsNoTracking();
        if (institutionId.HasValue)
        {
            await _currentUser.EnsureInstitutionAccessAsync(institutionId.Value);
            wallets = wallets.Where(w => w.InstitutionId == institutionId.Value);
        }

        if (role == AppRole.SchoolAdmin)
        {
            wallets = wallets.Where(w => w.InstitutionId == user.InstitutionId);
        }
        else if (role != AppRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (walletId.HasValue)
            wallets = wallets.Where(w => w.Id == walletId.Value);

        var walletIds = await wallets.Select(w => w.Id).ToListAsync(cancellationToken);
        var transactions = await _context.Transactions.AsNoTracking()
            .Where(t => walletIds.Contains(t.WalletId) && t.CreatedAt >= start && t.CreatedAt <= end)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Wallet.InstitutionId,
                InstitutionName = t.Wallet.Institution.Name,
                t.WalletId,
                t.Amount,
                t.Type,
                t.Status,
                t.Description,
                t.CreatedAt,
                t.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        return new WalletReportResponseDto
        {
            Filters = new WalletReportFilterDto
            {
                InstitutionId = includeIds ? institutionId : null,
                WalletId = includeIds ? walletId : null,
                From = from,
                To = to
            },
            Summary = new WalletReportSummaryDto
            {
                TotalTransactions = transactions.Count,
                SuccessAmount = transactions.Where(t => t.Status.IsSuccess()).Sum(t => t.Amount),
                TopUpAmount = transactions.Where(t => t.Status.IsSuccess() && t.Type.IsTopUp()).Sum(t => t.Amount),
                FeeAmount = transactions.Where(t => t.Status.IsSuccess() && !t.Type.IsTopUp()).Sum(t => t.Amount)
            },
            Items = transactions.Select(t => new WalletReportItemDto
            {
                Id = includeIds ? t.Id : null,
                InstitutionId = includeIds ? t.InstitutionId : null,
                InstitutionName = t.InstitutionName,
                WalletId = includeIds ? t.WalletId : null,
                Amount = t.Amount,
                Type = t.Type.ToCanonicalName(),
                Status = t.Status.ToCanonicalName(),
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                ProcessedAt = t.ProcessedAt
            }).ToList()
        };
    }

    public async Task<object> GetRevenueReportAsync(DateTime? from, DateTime? to, string groupBy = "day", CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        if (!string.Equals(groupBy, "day", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(groupBy, "month", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("groupBy must be day or month.");
        }

        await _currentUser.EnsureRoleAsync(AppRole.SuperAdmin);

        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var transactions = await _context.Transactions.AsNoTracking()
            .Where(t =>
                (t.Status == TransactionStatus.SUCCESS ||
                 t.Status == TransactionStatus.SUCCESS_LEGACY) &&
                t.CreatedAt >= start &&
                t.CreatedAt <= end)
            .Select(t => new
            {
                t.Amount,
                t.Type,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        string Bucket(DateTime value) => groupBy.Equals("month", StringComparison.OrdinalIgnoreCase)
            ? value.ToString("yyyy-MM")
            : value.ToString("yyyy-MM-dd");

        var items = transactions
            .GroupBy(t => Bucket(t.CreatedAt))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                period = g.Key,
                topUpAmount = g.Where(t => t.Type.IsTopUp()).Sum(t => t.Amount),
                attendanceFeeAmount = g.Where(t => t.Type.IsAttendanceFee()).Sum(t => t.Amount),
                proctoringFeeAmount = g.Where(t => t.Type.IsProctoringFee()).Sum(t => t.Amount),
                serviceFeeAmount = g.Where(t => !t.Type.IsTopUp()).Sum(t => t.Amount)
            })
            .ToList();

        return new
        {
            filters = new { from, to, groupBy },
            summary = new
            {
                topUpAmount = transactions.Where(t => t.Type.IsTopUp()).Sum(t => t.Amount),
                serviceFeeAmount = transactions.Where(t => !t.Type.IsTopUp()).Sum(t => t.Amount),
                transactionCount = transactions.Count
            },
            items
        };
    }

    private static void ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new InvalidOperationException("from must be earlier than or equal to to.");
    }
}
