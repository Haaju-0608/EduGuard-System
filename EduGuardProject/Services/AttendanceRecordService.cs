using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class AttendanceRecordService : IAttendanceRecordService
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IAttendanceRecordRepository _repo;
    private readonly IAttendanceSessionRepository _sessionRepo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;
    private readonly AppDbContext _context;
    private readonly IAiServiceClient _aiClient;

    public AttendanceRecordService(
        IWebHostEnvironment webHostEnvironment,
        IAttendanceRecordRepository repo,
        IAttendanceSessionRepository sessionRepo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        INotificationDispatcher notifications,
        IRealtimeEventDispatcher realtime,
        IStorageService storage,
        AppDbContext context,
        IAiServiceClient aiClient)
    {
        _webHostEnvironment = webHostEnvironment;
        _repo = repo;
        _sessionRepo = sessionRepo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _notifications = notifications;
        _realtime = realtime;
        _storage = storage;
        _context = context;
        _aiClient = aiClient;
    }

    public async Task<(IEnumerable<AttendanceRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? sessionId = null, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            studentId = user.Id;

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, sessionId, studentId);
        var dtos = new List<AttendanceRecordResponseDto>();
        foreach (var item in items)
        {
            await EnsureRecordAccessAsync(item);
            dtos.Add(await AcademicMapper.MapRecordAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<AttendanceRecordResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureRecordAccessAsync(entity);
        return await AcademicMapper.MapRecordAsync(_context, entity, expand);
    }

    public async Task<AttendanceRecordResponseDto> CreateAsync(CreateAttendanceRecordDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(dto.SessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");

        await EnsureSessionAccessAsync(session);

        var existing = await _repo.GetBySessionAndStudentAsync(dto.SessionId, dto.StudentId);
        if (existing != null)
            throw new InvalidOperationException("Attendance record already exists for this student in this session.");

        await EnsureStudentEnrolledAsync(session.ClassId, dto.StudentId);

        var entity = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            SessionId = dto.SessionId,
            StudentId = dto.StudentId,
            Status = dto.Status,
            Method = dto.Method,
            ConfidenceScore = dto.ConfidenceScore,
            SnapshotPath = dto.SnapshotPath,
            CheckinAt = dto.CheckinAt ?? DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await UpdateSessionRecognizedCountAsync(dto.SessionId);
        await DispatchAttendanceProgressAsync(entity, "created");
        return await AcademicMapper.MapRecordAsync(_context, entity, null);
    }

    public async Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkManualAsync(Guid sessionId, BulkManualAttendanceDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");
        await EnsureSessionAccessAsync(session);

        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Can only mark attendance for in-progress sessions.");

        var studentIds = dto.PresentStudentIds.Distinct().ToList();

        var activeEnrollments = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == session.ClassId && studentIds.Contains(e.StudentId) && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .ToListAsync();

        var existingRecords = await _context.AttendanceRecords
            .Where(r => r.SessionId == sessionId && studentIds.Contains(r.StudentId))
            .ToDictionaryAsync(r => r.StudentId);

        var now = DateTime.UtcNow;
        var recordsToInsert = new List<AttendanceRecord>();
        var results = new List<AttendanceRecordResponseDto>();

        foreach (var studentId in activeEnrollments)
        {
            if (existingRecords.TryGetValue(studentId, out var existing))
            {
                existing.Status = dto.Status;
                existing.Method = dto.Method;
                existing.CheckinAt = now;
                _context.AttendanceRecords.Update(existing);
                results.Add(await AcademicMapper.MapRecordAsync(_context, existing, null));
            }
            else
            {
                var record = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    StudentId = studentId,
                    Status = dto.Status,
                    Method = dto.Method,
                    CheckinAt = now
                };
                recordsToInsert.Add(record);
            }
        }

        if (recordsToInsert.Count > 0)
            await _context.AttendanceRecords.AddRangeAsync(recordsToInsert);

        await _context.SaveChangesAsync();

        foreach (var record in recordsToInsert)
        {
            results.Add(await AcademicMapper.MapRecordAsync(_context, record, null));
            await DispatchAttendanceProgressAsync(record, "created");
        }

        await UpdateSessionRecognizedCountAsync(sessionId);
        return results;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAttendanceRecordDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);

        var user = await _currentUser.GetRequiredUserAsync();
        entity.Status = dto.Status;
        entity.Method = dto.Method;
        entity.ConfidenceScore = dto.ConfidenceScore;
        entity.SnapshotPath = dto.SnapshotPath;
        entity.CheckinAt = dto.CheckinAt;
        entity.AdjustedBy = user.Id;
        entity.AdjustedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await UpdateSessionRecognizedCountAsync(entity.SessionId);
        await DispatchAttendanceProgressAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);

        var user = await _currentUser.GetRequiredUserAsync();
        var snapshotPath = entity.SnapshotPath;
        await _repo.SoftDeleteAsync(entity, user.Id);
        if (!string.IsNullOrWhiteSpace(snapshotPath))
            await _storage.DeleteAsync(StorageService.AttendanceSnapshotsBucket, snapshotPath);
        await UpdateSessionRecognizedCountAsync(entity.SessionId);
        await DispatchAttendanceProgressAsync(entity, "deleted");
        return true;
    }

    private async Task EnsureStudentEnrolledAsync(Guid classId, Guid studentId)
    {
        var enrolled = await _context.ClassEnrollments.AsNoTracking()
            .AnyAsync(e => e.ClassId == classId && e.StudentId == studentId && e.Status == EnrollmentStatus.Active);
        if (!enrolled)
            throw new InvalidOperationException("Student is not actively enrolled in this class.");
    }

    private async Task UpdateSessionRecognizedCountAsync(Guid sessionId)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId);
        if (session == null) return;

        var count = await _context.AttendanceRecords.CountAsync(r =>
            r.SessionId == sessionId &&
            r.Status == AttendanceStatus.Present &&
            !(r.Status == AttendanceStatus.Absent && r.AdjustedAt != null && r.CheckinAt == null));

        session.TotalRecognized = count;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session);
    }

    private async Task DispatchAttendanceProgressAsync(AttendanceRecord entity, string action)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == entity.SessionId);
        if (session == null) return;

        var student = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == entity.StudentId);

        var totalRecognized = await _context.AttendanceRecords.CountAsync(r =>
            r.SessionId == entity.SessionId && r.Status == AttendanceStatus.Present);
        var totalStudents = await _context.ClassEnrollments.CountAsync(e =>
            e.ClassId == session.ClassId && e.Status == EnrollmentStatus.Active);

        var payload = new
        {
            sessionId = entity.SessionId,
            session.ClassId,
            recordId = entity.Id,
            entity.StudentId,
            fullName = student?.FullName,
            entity.Status,
            entity.Method,
            entity.ConfidenceScore,
            entity.SnapshotPath,
            entity.CheckinAt,
            totalRecognized,
            totalStudents
        };

        await _realtime.PushAttendanceSessionAsync(entity.SessionId, HubEvents.AttendanceProgressChanged, payload);
        var cls = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == session.ClassId && c.DeletedAt == null);

        await _realtime.PublishDataChangedAsync(
            "attendance-records",
            action,
            institutionId: cls?.InstitutionId,
            lecturerId: cls?.LecturerId,
            userId: entity.StudentId,
            data: payload);

        if (entity.Status == AttendanceStatus.Present)
        {
            await _notifications.SendToUserAsync(
                entity.StudentId,
                "Điểm danh thành công",
                "Bạn đã được ghi nhận có mặt trong ca điểm danh.",
                NotificationType.AttendanceSessionStarted,
                ReferenceTypeEnum.AttendanceSession,
                entity.SessionId);
        }
    }

    private async Task EnsureRecordAccessAsync(AttendanceRecord entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
        {
            if (entity.StudentId != user.Id)
                throw new UnauthorizedAccessException("Access denied.");
            return;
        }

        var session = await _sessionRepo.GetByIdAsync(entity.SessionId);
        if (session != null) await EnsureSessionAccessAsync(session);
    }

    private async Task EnsureSessionAccessAsync(AttendanceSession session)
    {
        var cls = await _classRepo.GetByIdAsync(session.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }

    // 🌟 SỬA ĐỔI: Không ghi ổ đĩa local nữa, đẩy trực tiếp Stream sang FastAPI & Supabase
    public async Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkByAiVideoAsync(Guid sessionId, Stream videoStream, string fileName)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");

        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Can only mark attendance for in-progress sessions.");

        // 1. Gọi trực tiếp FastAPI để upload video lên Supabase Storage và bóc tách Vector khuôn mặt
        VideoVectorsResponse aiResult;
        try
        {
            aiResult = await _aiClient.ExtractVectorsFromVideoAsync(videoStream, fileName);
        }
        catch (Exception aiEx)
        {
            throw new InvalidOperationException($"Lỗi xử lý AI từ Python: {aiEx.Message}");
        }

        // 2. Cập nhật đường dẫn Video từ Cloud Supabase do FastAPI trả về
        session.VideoPath = aiResult.VideoUrl;
        session.Status = SessionStatus.Completed;

        var presentStudentIds = new List<Guid>();
        double threshold = 0.40;

        // 3. Truy vấn Vector Khớp Diện Mạo từ pgvector
        foreach (var vectorArray in aiResult.Vectors)
        {
            var pgVector = new Pgvector.Vector(vectorArray);

            var matchedStudentId = await _context.Database.SqlQueryRaw<Guid?>(@"
                SELECT user_id AS ""Value"" FROM biometric_data 
                WHERE is_active = true AND (face_vector <-> {0}) < {1}
                ORDER BY face_vector <-> {0} 
                LIMIT 1", pgVector, threshold)
            .FirstOrDefaultAsync();

            if (matchedStudentId.HasValue)
            {
                presentStudentIds.Add(matchedStudentId.Value);
            }
        }

        var uniqueStudentIds = presentStudentIds.Distinct().ToList();

        // 4. [TỐI ƯU] Tải trước toàn bộ Enrollment hợp lệ của lớp học
        var activeEnrollmentIds = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == session.ClassId && uniqueStudentIds.Contains(e.StudentId) && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .ToListAsync();

        // 5. [TỐI ƯU] Tải trước danh sách Record đã có của phiên này
        var existingRecords = await _context.AttendanceRecords
            .Where(r => r.SessionId == sessionId && uniqueStudentIds.Contains(r.StudentId))
            .ToDictionaryAsync(r => r.StudentId);

        var now = DateTime.UtcNow;
        var recordsToInsert = new List<AttendanceRecord>();
        var results = new List<AttendanceRecordResponseDto>();

        // 6. Thực hiện so khớp trực tiếp trên RAM (In-Memory)
        foreach (var studentId in uniqueStudentIds)
        {
            if (!activeEnrollmentIds.Contains(studentId)) continue;

            if (existingRecords.TryGetValue(studentId, out var existing))
            {
                existing.Status = AttendanceStatus.Present;
                existing.Method = AttendanceMethod.Ai;
                existing.CheckinAt = now;

                _context.AttendanceRecords.Update(existing);
                results.Add(await AcademicMapper.MapRecordAsync(_context, existing, null));
            }
            else
            {
                var record = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    StudentId = studentId,
                    Status = AttendanceStatus.Present,
                    Method = AttendanceMethod.Ai,
                    CheckinAt = now
                };
                recordsToInsert.Add(record);
            }
        }

        if (recordsToInsert.Count > 0)
            await _context.AttendanceRecords.AddRangeAsync(recordsToInsert);

        await _context.SaveChangesAsync();

        foreach (var record in recordsToInsert)
            results.Add(await AcademicMapper.MapRecordAsync(_context, record, null));

        await UpdateSessionRecognizedCountAsync(sessionId);

        return results;
    }
}