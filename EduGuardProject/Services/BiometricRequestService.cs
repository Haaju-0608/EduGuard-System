using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class BiometricRequestService : IBiometricRequestService
{
    private readonly IBiometricRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;
    private readonly IAiServiceClient _aiClient;

    public BiometricRequestService(
        IBiometricRequestRepository repo,
        ICurrentUserService currentUser,
        AppDbContext context,
        IAiServiceClient aiClient)
    {
        _repo = repo;
        _currentUser = currentUser;
        _context = context;
        _aiClient = aiClient;
    }

    public async Task<(IEnumerable<BiometricRequestResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student)
            studentId = user.Id;
        else
            await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin, AppRole.Lecturer);

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, studentId);
        var dtos = new List<BiometricRequestResponseDto>();
        foreach (var item in items)
            dtos.Add(await AcademicMapper.MapBiometricRequestAsync(_context, item, expand));
        return (dtos, total);
    }

    public async Task<BiometricRequestResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        //if (entity == null || entity.Status == BiometricReqStatus.Rejected) return null;
        //await EnsureRequestAccessAsync(entity);
        //return await AcademicMapper.MapBiometricRequestAsync(_context, entity, expand);
        // SỬA: Cho phép xem đơn bị Rejected để check lịch sử, chỉ chặn khi không tìm thấy thực tế
        if (entity == null) return null;

        await EnsureRequestAccessAsync(entity);
        return await AcademicMapper.MapBiometricRequestAsync(_context, entity, expand);
    }

    //public async Task<BiometricRequestResponseDto> CreateAsync(CreateBiometricRequestDto dto)
    //{
    //    await _currentUser.EnsureRoleAsync(AppRole.Student);
    //    var user = await _currentUser.GetRequiredUserAsync();

    //    var entity = new BiometricRequest
    //    {
    //        Id = Guid.NewGuid(),
    //        StudentId = user.Id,
    //        Reason = dto.Reason,
    //        Status = BiometricReqStatus.Pending,
    //        CreatedAt = DateTime.UtcNow
    //    };

    //    await _repo.AddAsync(entity);
    //    return await AcademicMapper.MapBiometricRequestAsync(_context, entity, null);
    //}

    // SỬA: Controller sau khi upload file lên Supabase Storage thành công sẽ ném 3 đường dẫn Path vào đây với giờ chưa có storage nên tui lưu ở wwwroot nhá
    public async Task<BiometricRequestResponseDto> CreateAsync(CreateBiometricRequestDto dto)
    {
        // 1. Kiểm tra phân quyền Sinh viên
        await _currentUser.EnsureRoleAsync(AppRole.Student);
        var user = await _currentUser.GetRequiredUserAsync();

        // 2. Kiểm tra và tạo thư mục lưu ảnh cục bộ nếu chưa có
        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "biometrics");
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        // 3. Định danh tên file độc nhất bằng Guid để tránh ghi đè
        var frontFileName = $"{Guid.NewGuid()}_{dto.FrontFile.FileName}";
        var leftFileName = $"{Guid.NewGuid()}_{dto.LeftFile.FileName}";
        var rightFileName = $"{Guid.NewGuid()}_{dto.RightFile.FileName}";

        // Đường dẫn tương đối lưu vào DB
        string frontPath = Path.Combine("uploads", "biometrics", frontFileName);
        string leftPath = Path.Combine("uploads", "biometrics", leftFileName);
        string rightPath = Path.Combine("uploads", "biometrics", rightFileName);

        // 4. Tiến hành ghi trực tiếp file vật lý xuống ổ đĩa cục bộ server
        using (var fs = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", frontPath), FileMode.Create))
            await dto.FrontFile.CopyToAsync(fs);
        using (var fs = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", leftPath), FileMode.Create))
            await dto.LeftFile.CopyToAsync(fs);
        using (var fs = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rightPath), FileMode.Create))
            await dto.RightFile.CopyToAsync(fs);

        // 5. Khởi tạo thực thể Request gửi lên trường với trạng thái PENDING (Hoàn toàn không có Vector)
        var entity = new BiometricRequest
        {
            Id = Guid.NewGuid(),
            StudentId = user.Id,
            Reason = dto.Reason,
            Status = BiometricReqStatus.Pending,
            FrontImagePath = frontPath,
            LeftImagePath = leftPath,
            RightImagePath = rightPath,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        return await AcademicMapper.MapBiometricRequestAsync(_context, entity, null);
    }

    public async Task<bool> ApproveAsync(Guid id, ReviewBiometricRequestDto? dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.Status != BiometricReqStatus.Pending) return false;

        var user = await _currentUser.GetRequiredUserAsync();

        try
        {
            // 1. Tìm đường dẫn vật lý của 3 file ảnh thô trong thư mục wwwroot
            var frontFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", entity.FrontImagePath);
            var leftFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", entity.LeftImagePath);
            var rightFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", entity.RightImagePath);

            if (!File.Exists(frontFullPath) || !File.Exists(leftFullPath) || !File.Exists(rightFullPath))
                throw new FileNotFoundException("Không tìm thấy dữ liệu ảnh eKYC vật lý trên ổ đĩa Server cục bộ.");

            // 2. Mở luồng đọc file trực tiếp từ ổ đĩa
            using var frontStream = File.OpenRead(frontFullPath);
            using var leftStream = File.OpenRead(leftFullPath);
            using var rightStream = File.OpenRead(rightFullPath);

            // 3. Bắn đồng thời 3 luồng ảnh sang AI Python để xử lý tính toán Vector trung bình
            float[] vectorArray = await _aiClient.ExtractVectorFrom3FacesAsync(
                frontStream, Path.GetFileName(frontFullPath),
                leftStream, Path.GetFileName(leftFullPath),
                rightStream, Path.GetFileName(rightFullPath)
            );
            var pgVector = new Pgvector.Vector(vectorArray);

            // 4. VÔ HIỆU HÓA các nhận diện khuôn mặt cũ đang hoạt động của sinh viên này
            var oldRecords = await _context.BiometricData
                .Where(b => b.UserId == entity.StudentId && b.IsActive)
                .ToListAsync();
            foreach (var r in oldRecords)
            {
                r.IsActive = false;
                r.UpdatedAt = DateTime.UtcNow;
            }

            // 5. Khởi tạo bản ghi dữ liệu sinh trắc học chính thức lưu vào bảng BIOMETRIC_DATA
            var newBioData = new BiometricDatum
            {
                Id = Guid.NewGuid(),
                UserId = entity.StudentId,
                BioRequestId = entity.Id,
                FaceVector = pgVector,                // Vector chuẩn hóa nạp vào đây!
                ModelVersion = "face_recognition_v1",
                IsActive = true,                       // Kích hoạt nhận diện mới
                FaceImageUrl = entity.FrontImagePath, // Lưu path ảnh thẳng làm ảnh đối soát
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.BiometricData.AddAsync(newBioData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lỗi đồng bộ eKYC với Server AI trong quá trình phê duyệt: {ex.Message}");
        }

        // 6. Cập nhật trạng thái đơn yêu cầu thành APPROVED
        entity.Status = BiometricReqStatus.Approved;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        await _repo.UpdateAsync(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(Guid id, ReviewBiometricRequestDto? dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        entity.Status = BiometricReqStatus.Rejected;
        entity.ApprovedBy = user.Id;
        entity.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto?.Reason))
            entity.Reason = dto.Reason;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && entity.StudentId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
        else
            await _currentUser.EnsureRoleAsync(AppRole.Student, AppRole.SchoolAdmin, AppRole.SuperAdmin);

        await _repo.SoftDeleteAsync(entity);
        return true;
    }

    private async Task EnsureRequestAccessAsync(BiometricRequest entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin || user.Role == AppRole.SchoolAdmin) return;

        if (user.Role == AppRole.Student && entity.StudentId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
