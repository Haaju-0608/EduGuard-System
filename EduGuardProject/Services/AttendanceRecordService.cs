using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Supabase.Interfaces;

namespace EduGuardProject.Services;

public class AttendanceRecordService : IAttendanceRecordService
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IAttendanceRecordRepository _repo;
    private readonly IAttendanceSessionRepository _sessionRepo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;
    private readonly IAiServiceClient _aiClient;

    public AttendanceRecordService(
        IWebHostEnvironment webHostEnvironment,
        IAttendanceRecordRepository repo,
        IAttendanceSessionRepository sessionRepo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        AppDbContext context,
        IAiServiceClient aiClient)
    {
        _webHostEnvironment = webHostEnvironment;
        _repo = repo;
        _sessionRepo = sessionRepo;
        _classRepo = classRepo;
        _currentUser = currentUser;
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

        var user = await _currentUser.GetRequiredUserAsync();
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
        return await AcademicMapper.MapRecordAsync(_context, entity, null);
    }

    public async Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkManualAsync(
        Guid sessionId, BulkManualAttendanceDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");
        await EnsureSessionAccessAsync(session);

        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Can only mark attendance for in-progress sessions.");

        var now = DateTime.UtcNow;
        var records = new List<AttendanceRecord>();
        var results = new List<AttendanceRecordResponseDto>();

        foreach (var studentId in dto.PresentStudentIds.Distinct())
        {
            await EnsureStudentEnrolledAsync(session.ClassId, studentId);

            var existing = await _repo.GetBySessionAndStudentAsync(sessionId, studentId);
            if (existing != null)
            {
                existing.Status = dto.Status;
                existing.Method = dto.Method;
                existing.CheckinAt = now;
                //await _repo.UpdateAsync(existing);
                _context.AttendanceRecords.Update(existing); // Xếp hàng chờ lưu
                results.Add(await AcademicMapper.MapRecordAsync(_context, existing, null));
                continue;
            }

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = studentId,
                Status = dto.Status,
                Method = dto.Method,
                CheckinAt = now
            };
            records.Add(record);
        }

        //if (records.Count > 0)
        //    await _repo.AddRangeAsync(records);

        if (records.Count > 0)
            await _context.AttendanceRecords.AddRangeAsync(records);

        // Lưu tất cả cập nhật mới và cũ trong đúng 1 Transaction duy nhất
        await _context.SaveChangesAsync();

        foreach (var record in records)
            results.Add(await AcademicMapper.MapRecordAsync(_context, record, null));

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
        await _repo.SoftDeleteAsync(entity, user.Id);
        await UpdateSessionRecognizedCountAsync(entity.SessionId);
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

    // =========================================================================
    // HÀM XỬ LÝ ĐIỂM DANH TỪ VIDEO AI (ĐÃ TỐI ƯU HÓA BATCH SAVE 🚀)
    // =========================================================================
    public async Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkByAiVideoAsync(Guid sessionId, Stream videoStream, string fileName)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Attendance session not found.");

        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException("Can only mark attendance for in-progress sessions.");

        string savedVideoPath = null!;
        string fullPath = null!;
        try
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "attendance-videos");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"session_{sessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{fileExtension}";
            fullPath = Path.Combine(uploadsFolder, uniqueFileName);

            // Copy stream gốc vào file vật lý trong wwwroot
            using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                await videoStream.CopyToAsync(fileStream);
            }

            // Đường dẫn tương đối lưu vào DB
            savedVideoPath = $"/uploads/attendance-videos/{uniqueFileName}";
        }
        catch (Exception fileEx)
        {
            throw new InvalidOperationException($"Lỗi lưu file video vào hệ thống: {fileEx.Message}");
        }

        // =========================================================================
        // 🌟 BƯỚC 2: MỞ LUỒNG STREAM MỚI TỪ FILE ĐÃ LƯU ĐỂ GỬI SANG PYTHON AI
        // =========================================================================
        List<float[]> detectedVectors;
        try
        {
            // Mở luồng đọc từ file vật lý vừa ghi thành công trên ổ cứng
            using (var pythonStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                detectedVectors = await _aiClient.ExtractVectorsFromVideoAsync(pythonStream, fileName);
            }
        }
        catch (Exception aiEx)
        {
            // Nếu AI lỗi, chủ động xóa file rác vừa lưu ở wwwroot để tránh đầy ổ cứng
            if (File.Exists(fullPath)) File.Delete(fullPath);

            throw new InvalidOperationException($"Lỗi xử lý AI từ Python: {aiEx.Message}");
        }

        // Cập nhật thông tin đường dẫn video vào thực thể session đang được EF tracking
        session.VideoPath = savedVideoPath;
        session.Status = SessionStatus.Completed;

        var presentStudentIds = new List<Guid>();
        double threshold = 0.40; // Độ khắt khe nhận diện hình ảnh

        // 2. Tìm kiếm hàng loạt sinh viên khớp diện mạo bằng pgvector toán tử <->
        foreach (var vectorArray in detectedVectors)
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

        var now = DateTime.UtcNow;
        var records = new List<AttendanceRecord>();
        var results = new List<AttendanceRecordResponseDto>();

        // 3. Quét kiểm tra điều kiện lớp học và Gom danh sách chờ lưu dữ liệu
        foreach (var studentId in uniqueStudentIds)
        {
            var enrolled = await _context.ClassEnrollments.AsNoTracking()
                .AnyAsync(e => e.ClassId == session.ClassId && e.StudentId == studentId && e.Status == EnrollmentStatus.Active);

            if (!enrolled) continue; // Đi học ké lớp khác thì bỏ qua

            var existing = await _repo.GetBySessionAndStudentAsync(sessionId, studentId);
            if (existing != null)
            {
                existing.Status = AttendanceStatus.Present;
                existing.Method = AttendanceMethod.Ai;
                existing.CheckinAt = now;

                _context.AttendanceRecords.Update(existing); // 🌟 TỐI ƯU: Đánh dấu cập nhật vào context, CHƯA SAVE vội
                results.Add(await AcademicMapper.MapRecordAsync(_context, existing, null));
                continue;
            }

            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = studentId,
                Status = AttendanceStatus.Present,
                Method = AttendanceMethod.Ai,
                CheckinAt = now
            };
            records.Add(record);
        }

        // 🌟 TỐI ƯU CHÍ MẠNG: Bắn đúng 1 lệnh duy nhất lưu tất cả bản ghi mới/cũ xuống Database!
        if (records.Count > 0)
            await _context.AttendanceRecords.AddRangeAsync(records);

        await _context.SaveChangesAsync();

        foreach (var record in records)
            results.Add(await AcademicMapper.MapRecordAsync(_context, record, null));

        // Cập nhật lại tổng số sinh viên có mặt trong phiên
        await UpdateSessionRecognizedCountAsync(sessionId);

        return results;
    }
}
