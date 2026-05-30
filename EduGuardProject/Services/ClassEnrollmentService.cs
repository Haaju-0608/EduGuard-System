using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ClassEnrollmentService : IClassEnrollmentService
{
    private readonly IClassEnrollmentRepository _repo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public ClassEnrollmentService(
        IClassEnrollmentRepository repo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<ClassEnrollmentResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? classId = null, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();

        if (user.Role == AppRole.Student)
            studentId = user.Id;
        else if (user.Role == AppRole.Lecturer && classId == null)
        {
            var lecturerClassIds = await _context.Classes.AsNoTracking()
                .Where(c => c.LecturerId == user.Id && c.DeletedAt == null)
                .Select(c => c.Id)
                .ToListAsync();
            if (!lecturerClassIds.Any())
                return ([], 0);
        }

        var (items, total) = await _repo.GetAllAsync(search, sort, page, pageSize, classId, studentId);
        var dtos = new List<ClassEnrollmentResponseDto>();
        foreach (var item in items)
        {
            if (user.Role != AppRole.SuperAdmin)
                await EnsureEnrollmentAccessAsync(item);
            dtos.Add(await AcademicMapper.MapEnrollmentAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<ClassEnrollmentResponseDto?> GetByKeyAsync(Guid classId, Guid studentId, string? expand)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return null;
        await EnsureEnrollmentAccessAsync(entity);
        return await AcademicMapper.MapEnrollmentAsync(_context, entity, expand);
    }

    public async Task<ClassEnrollmentResponseDto> CreateAsync(CreateClassEnrollmentDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);

        var cls = await _classRepo.GetByIdAsync(dto.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("You can only enroll students in your own classes.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        var student = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dto.StudentId && u.DeletedAt == null);
        if (student == null) throw new InvalidOperationException("Student not found.");
        if (student.Role != AppRole.Student)
            throw new InvalidOperationException("User is not a student.");

        var existing = await _repo.GetByKeyAsync(dto.ClassId, dto.StudentId);
        if (existing != null)
        {
            if (existing.Status == EnrollmentStatus.Dropped)
            {
                existing.Status = EnrollmentStatus.Active;
                existing.EnrolledAt = DateTime.UtcNow;
                await _repo.UpdateAsync(existing);
                return await AcademicMapper.MapEnrollmentAsync(_context, existing, null);
            }
            throw new InvalidOperationException("Student is already enrolled in this class.");
        }

        var entity = new ClassEnrollment
        {
            ClassId = dto.ClassId,
            StudentId = dto.StudentId,
            Status = dto.Status,
            EnrolledAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        return await AcademicMapper.MapEnrollmentAsync(_context, entity, null);
    }

    public async Task<bool> UpdateAsync(Guid classId, Guid studentId, UpdateClassEnrollmentDto dto)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureEnrollmentAccessAsync(entity);

        entity.Status = dto.Status;
        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid classId, Guid studentId)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureEnrollmentAccessAsync(entity);

        await _repo.SoftDeleteAsync(entity);
        return true;
    }

    private async Task EnsureEnrollmentAccessAsync(ClassEnrollment entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin) return;

        if (user.Role == AppRole.Student && entity.StudentId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");

        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
