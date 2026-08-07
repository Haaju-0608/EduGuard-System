using System.Text.Json;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace EduGuardProject.Services;

public class DashboardStatsService : IDashboardStatsService
{
    private static readonly TimeSpan SystemDashboardCacheTtl = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public DashboardStatsService(AppDbContext context, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<object> GetSystemDashboardAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        await _currentUser.EnsureRoleAsync(AppRole.SuperAdmin);

        // Aggregation is expensive (many COUNT/SUM queries) and this dashboard doesn't
        // need second-level freshness, so cache the computed snapshot briefly.
        var cacheKey = $"dashboard:system:{from:O}:{to:O}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached != null)
            return JsonSerializer.Deserialize<JsonElement>(cached);

        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var usersByRole = await _context.Users.AsNoTracking()
            .Where(u => u.DeletedAt == null)
            .GroupBy(u => u.Role)
            .Select(g => new { role = g.Key.ToString(), count = g.Count() })
            .ToListAsync(cancellationToken);

        var institutionsByStatus = await _context.Institutions.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToListAsync(cancellationToken);

        var successfulTransactions = _context.Transactions.AsNoTracking()
            .Where(t =>
                (t.Status == TransactionStatus.SUCCESS ||
                 t.Status == TransactionStatus.SUCCESS_LEGACY) &&
                t.CreatedAt >= start &&
                t.CreatedAt <= end);

        var result = new
        {
            range = new { from = start == DateTime.MinValue ? null : from, to = end == DateTime.MaxValue ? null : to },
            institutions = new
            {
                total = await _context.Institutions.CountAsync(cancellationToken),
                byStatus = institutionsByStatus
            },
            users = new
            {
                total = await _context.Users.CountAsync(u => u.DeletedAt == null, cancellationToken),
                byRole = usersByRole
            },
            academic = new
            {
                classes = await _context.Classes.CountAsync(c => c.DeletedAt == null, cancellationToken),
                examSlots = await _context.ExamSlots.CountAsync(e => e.CreatedAt >= start && e.CreatedAt <= end, cancellationToken),
                attendanceSessions = await _context.AttendanceSessions.CountAsync(s => s.CreatedAt >= start && s.CreatedAt <= end, cancellationToken),
                violations = await _context.ViolationLogs.CountAsync(v => v.RecordedAt >= start && v.RecordedAt <= end, cancellationToken)
            },
            wallet = new
            {
                totalBalance = await _context.Wallets.SumAsync(w => w.Balance, cancellationToken),
                topUpAmount = await successfulTransactions
                    .Where(t =>
                        t.Type == TransactionType.TOP_UP ||
                        t.Type == TransactionType.TOP_UP_LEGACY)
                    .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0,
                serviceFeeAmount = await successfulTransactions
                    .Where(t =>
                        t.Type == TransactionType.ATTENDANCE_FEE ||
                        t.Type == TransactionType.ATTENDANCE_FEE_LEGACY ||
                        t.Type == TransactionType.PROCTORING_FEE ||
                        t.Type == TransactionType.PROCTORING_FEE_LEGACY)
                    .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0
            }
        };

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SystemDashboardCacheTtl },
            cancellationToken);

        return result;
    }

    public async Task<object> GetInstitutionDashboardAsync(Guid institutionId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        await _currentUser.EnsureInstitutionAccessAsync(institutionId);

        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var classIds = await _context.Classes.AsNoTracking()
            .Where(c => c.InstitutionId == institutionId && c.DeletedAt == null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var wallet = await _context.Wallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.InstitutionId == institutionId, cancellationToken);

        var examSlotIds = await _context.ExamSlots.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return new
        {
            institutionId,
            range = new { from = start == DateTime.MinValue ? null : from, to = end == DateTime.MaxValue ? null : to },
            classes = classIds.Count,
            users = new
            {
                students = await _context.Users.CountAsync(u => u.InstitutionId == institutionId && u.Role == AppRole.Student && u.DeletedAt == null, cancellationToken),
                lecturers = await _context.Users.CountAsync(u => u.InstitutionId == institutionId && u.Role == AppRole.Lecturer && u.DeletedAt == null, cancellationToken),
                admins = await _context.Users.CountAsync(u => u.InstitutionId == institutionId && u.Role == AppRole.SchoolAdmin && u.DeletedAt == null, cancellationToken)
            },
            attendance = new
            {
                sessions = await _context.AttendanceSessions.CountAsync(s => classIds.Contains(s.ClassId) && s.CreatedAt >= start && s.CreatedAt <= end, cancellationToken),
                completed = await _context.AttendanceSessions.CountAsync(s => classIds.Contains(s.ClassId) && s.Status == SessionStatus.Completed && s.CreatedAt >= start && s.CreatedAt <= end, cancellationToken),
                recognized = await _context.AttendanceSessions.Where(s => classIds.Contains(s.ClassId) && s.CreatedAt >= start && s.CreatedAt <= end).SumAsync(s => (int?)s.TotalRecognized, cancellationToken) ?? 0
            },
            exams = new
            {
                slots = await _context.ExamSlots.CountAsync(e => classIds.Contains(e.ClassId) && e.CreatedAt >= start && e.CreatedAt <= end, cancellationToken),
                submitted = await _context.ExamParticipations.CountAsync(p => examSlotIds.Contains(p.ExamSlotId) && p.Status == ParticipationStatus.Submitted, cancellationToken),
                disqualified = await _context.ExamParticipations.CountAsync(p => examSlotIds.Contains(p.ExamSlotId) && p.Status == ParticipationStatus.Disqualified, cancellationToken)
            },
            violations = await _context.ViolationLogs.CountAsync(v =>
                examSlotIds.Contains(v.Participation.ExamSlotId) && v.RecordedAt >= start && v.RecordedAt <= end, cancellationToken),
            wallet = wallet == null
                ? null
                : new
                {
                    wallet.Id,
                    wallet.Balance,
                    wallet.Currency,
                    wallet.LowBalanceThreshold
                }
        };
    }

    public async Task<object> GetLecturerDashboardAsync(Guid? lecturerId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(from, to);
        var user = await _currentUser.GetRequiredUserAsync();
        var effectiveLecturerId = lecturerId ?? user.Id;

        if (user.Role == AppRole.Lecturer && effectiveLecturerId != user.Id)
            throw new UnauthorizedAccessException("Lecturers can only view their own dashboard.");

        var lecturer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == effectiveLecturerId && u.Role == AppRole.Lecturer && u.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Lecturer not found.");

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId != lecturer.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        var start = from ?? DateTime.MinValue;
        var end = to ?? DateTime.MaxValue;

        var classIds = await _context.Classes.AsNoTracking()
            .Where(c => c.LecturerId == effectiveLecturerId && c.DeletedAt == null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var examSlotIds = await _context.ExamSlots.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return new
        {
            lecturerId = effectiveLecturerId,
            lecturer.FullName,
            range = new { from = start == DateTime.MinValue ? null : from, to = end == DateTime.MaxValue ? null : to },
            classes = classIds.Count,
            students = await _context.ClassEnrollments.CountAsync(e => classIds.Contains(e.ClassId) && e.Status == EnrollmentStatus.Active, cancellationToken),
            attendance = new
            {
                sessions = await _context.AttendanceSessions.CountAsync(s => classIds.Contains(s.ClassId) && s.CreatedAt >= start && s.CreatedAt <= end, cancellationToken),
                inProgress = await _context.AttendanceSessions.CountAsync(s => classIds.Contains(s.ClassId) && s.Status == SessionStatus.InProgress, cancellationToken),
                recognized = await _context.AttendanceSessions.Where(s => classIds.Contains(s.ClassId) && s.CreatedAt >= start && s.CreatedAt <= end).SumAsync(s => (int?)s.TotalRecognized, cancellationToken) ?? 0
            },
            exams = new
            {
                slots = await _context.ExamSlots.CountAsync(e => classIds.Contains(e.ClassId) && e.CreatedAt >= start && e.CreatedAt <= end, cancellationToken),
                inProgress = await _context.ExamSlots.CountAsync(e => classIds.Contains(e.ClassId) && e.Status == ExamSlotStatus.InProgress, cancellationToken),
                submitted = await _context.ExamParticipations.CountAsync(p => examSlotIds.Contains(p.ExamSlotId) && p.Status == ParticipationStatus.Submitted, cancellationToken)
            },
            violations = await _context.ViolationLogs.CountAsync(v =>
                examSlotIds.Contains(v.Participation.ExamSlotId) && v.RecordedAt >= start && v.RecordedAt <= end, cancellationToken)
        };
    }

    private static void ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new InvalidOperationException("from must be earlier than or equal to to.");
    }
}
