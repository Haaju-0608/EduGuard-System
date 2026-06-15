using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services;

public class AttendanceSessionService : IAttendanceSessionService
{
    private readonly IAttendanceSessionRepository _repo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public AttendanceSessionService(
        IAttendanceSessionRepository repo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<AttendanceSessionResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? classId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot list attendance sessions.");

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, classId);
        var dtos = new List<AttendanceSessionResponseDto>();
        foreach (var item in items)
        {
            await EnsureSessionAccessAsync(item);
            dtos.Add(await AcademicMapper.MapSessionAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<AttendanceSessionResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureSessionAccessAsync(entity);
        return await AcademicMapper.MapSessionAsync(_context, entity, expand);
    }

    public async Task<AttendanceSessionResponseDto> CreateAsync(CreateAttendanceSessionDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();

        var cls = await _classRepo.GetByIdAsync(dto.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");
        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("You can only open sessions for your own classes.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        var entity = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            CreatedBy = user.Id,
            //VideoPath = dto.VideoPath,
            StartTime = dto.StartTime,
            Status = SessionStatus.InProgress,
            TotalRecognized = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        return await AcademicMapper.MapSessionAsync(_context, entity, null);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAttendanceSessionDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureSessionAccessAsync(entity);

        if (dto.EndTime.HasValue) entity.EndTime = dto.EndTime;
        entity.Status = dto.Status;
        if (dto.VideoPath != null) entity.VideoPath = dto.VideoPath;
        if (dto.TotalRecognized.HasValue) entity.TotalRecognized = dto.TotalRecognized.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureSessionAccessAsync(entity);
        await _repo.SoftDeleteAsync(entity);
        return true;
    }

    private async Task EnsureSessionAccessAsync(AttendanceSession entity)
    {
        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
