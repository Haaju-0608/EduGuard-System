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

    public async Task<object> GetAttendanceReportAsync(Guid? institutionId, Guid? classId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var classes = _context.Classes.AsNoTracking().Where(c => c.DeletedAt == null);
        if (institutionId.HasValue)
        {
            await _currentUser.EnsureInstitutionAccessAsync(institutionId.Value);
            classes = classes.Where(c => c.InstitutionId == institutionId.Value);
        }
        else if (user.Role == AppRole.SchoolAdmin)
        {
            classes = classes.Where(c => c.InstitutionId == user.InstitutionId);
        }
        else if (user.Role == AppRole.Lecturer)
        {
            classes = classes.Where(c => c.LecturerId == user.Id);
        }
        else if (user.Role != AppRole.SuperAdmin)
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
                s.ClassId,
                s.StartTime,
                s.EndTime,
                s.Status,
                s.TotalRecognized,
                totalStudents = _context.ClassEnrollments.Count(e => e.ClassId == s.ClassId && e.Status == EnrollmentStatus.Active),
                records = _context.AttendanceRecords.Count(r => r.SessionId == s.Id)
            })
            .ToListAsync(cancellationToken);

        return new
        {
            filters = new { institutionId, classId, from, to },
            summary = new
            {
                sessions = sessions.Count,
                completed = sessions.Count(s => s.Status == SessionStatus.Completed),
                totalRecognized = sessions.Sum(s => s.TotalRecognized),
                averageRecognitionRate = sessions.Count == 0
                    ? 0
                    : sessions.Average(s => s.totalStudents == 0 ? 0 : (double)s.TotalRecognized / s.totalStudents)
            },
            items = sessions
        };
    }

    public async Task<object> GetViolationReportAsync(Guid? institutionId, Guid? examSlotId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
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
        else if (user.Role == AppRole.SchoolAdmin)
        {
            query = query.Where(v => v.Participation.ExamSlot.Class.InstitutionId == user.InstitutionId);
        }
        else if (user.Role == AppRole.Lecturer)
        {
            query = query.Where(v => v.Participation.ExamSlot.Class.LecturerId == user.Id);
        }
        else if (user.Role != AppRole.SuperAdmin)
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

        return new
        {
            filters = new { institutionId, examSlotId, from, to },
            summary = new
            {
                total = items.Count,
                reviewed = items.Count(i => i.IsReviewed),
                byType = items.GroupBy(i => i.type).Select(g => new { type = g.Key.ToString(), count = g.Count() }),
                bySeverity = items.GroupBy(i => i.severity).Select(g => new { severity = g.Key.ToString(), count = g.Count() })
            },
            items
        };
    }

    public async Task<object> GetWalletReportAsync(Guid? institutionId, Guid? walletId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var wallets = _context.Wallets.AsNoTracking();
        if (institutionId.HasValue)
        {
            await _currentUser.EnsureInstitutionAccessAsync(institutionId.Value);
            wallets = wallets.Where(w => w.InstitutionId == institutionId.Value);
        }
        else if (user.Role == AppRole.SchoolAdmin)
        {
            wallets = wallets.Where(w => w.InstitutionId == user.InstitutionId);
        }
        else if (user.Role != AppRole.SuperAdmin)
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
                t.WalletId,
                t.Amount,
                t.Type,
                t.Status,
                t.Description,
                t.CreatedAt,
                t.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        return new
        {
            filters = new { institutionId, walletId, from, to },
            summary = new
            {
                totalTransactions = transactions.Count,
                successAmount = transactions.Where(t => t.Status.IsSuccess()).Sum(t => t.Amount),
                topUpAmount = transactions.Where(t => t.Status.IsSuccess() && t.Type.IsTopUp()).Sum(t => t.Amount),
                feeAmount = transactions.Where(t => t.Status.IsSuccess() && !t.Type.IsTopUp()).Sum(t => t.Amount)
            },
            items = transactions.Select(t => new
            {
                t.Id,
                t.WalletId,
                t.Amount,
                type = t.Type.ToCanonicalName(),
                status = t.Status.ToCanonicalName(),
                t.Description,
                t.CreatedAt,
                t.ProcessedAt
            })
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
