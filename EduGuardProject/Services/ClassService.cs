using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services;

public class ClassService : IClassService
{
    private readonly IClassRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly AppDbContext _context;

    public ClassService(
        IClassRepository repo,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _realtime = realtime;
        _context = context;
    }

    public async Task<(IEnumerable<ClassResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        Guid? institutionId = user.Role == AppRole.SuperAdmin ? null : user.InstitutionId;
        Guid? lecturerId = user.Role == AppRole.Lecturer ? user.Id : null;

        if (user.Role == AppRole.Student)
            throw new UnauthorizedAccessException("Students cannot list all classes.");

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, institutionId, lecturerId);
        var dtos = new List<ClassResponseDto>();
        foreach (var item in items)
            dtos.Add(await AcademicMapper.MapClassAsync(_context, item, expand));
        return (dtos, total);
    }

    public async Task<ClassResponseDto?> GetByIdAsync(Guid id, string? expand)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureCanAccessClassAsync(entity);
        return await AcademicMapper.MapClassAsync(_context, entity, expand);
    }

    public async Task<ClassResponseDto> CreateAsync(CreateClassDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var user = await _currentUser.GetRequiredUserAsync();
        await _currentUser.EnsureInstitutionAccessAsync(dto.InstitutionId);

        if (user.Role == AppRole.Lecturer && dto.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Lecturers can only create classes assigned to themselves.");

        var entity = new Class
        {
            Id = Guid.NewGuid(),
            InstitutionId = dto.InstitutionId,
            LecturerId = dto.LecturerId,
            CourseName = dto.CourseName,
            CourseCode = dto.CourseCode,
            Semester = dto.Semester,
            AcademicYear = dto.AcademicYear,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedBy = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await PublishClassChangedAsync(entity, "created");
        return await AcademicMapper.MapClassAsync(_context, entity, null);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateClassDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await EnsureCanManageClassAsync(entity);

        entity.CourseName = dto.CourseName;
        entity.CourseCode = dto.CourseCode;
        entity.Semester = dto.Semester;
        entity.AcademicYear = dto.AcademicYear;
        entity.LecturerId = dto.LecturerId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await PublishClassChangedAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;
        await EnsureCanManageClassAsync(entity);
        await _repo.SoftDeleteAsync(entity);
        await PublishClassChangedAsync(entity, "deleted");
        return true;
    }

    private Task PublishClassChangedAsync(Class entity, string action) =>
        _realtime.PublishDataChangedAsync(
            "classes",
            action,
            institutionId: entity.InstitutionId,
            lecturerId: entity.LecturerId,
            data: new
            {
                classId = entity.Id,
                entity.InstitutionId,
                entity.LecturerId,
                entity.CourseName,
                entity.CourseCode,
                entity.Semester,
                entity.AcademicYear
            });

    private async Task EnsureCanAccessClassAsync(Class entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.InstitutionId != entity.InstitutionId)
            throw new UnauthorizedAccessException("Access denied to this class.");

        if (user.Role == AppRole.Lecturer && entity.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied to this class.");
    }

    private async Task EnsureCanManageClassAsync(Class entity)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureCanAccessClassAsync(entity);
    }
}
