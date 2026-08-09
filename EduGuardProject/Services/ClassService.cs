using System.Text.RegularExpressions;
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

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

    public async Task<(IEnumerable<ClassResponseDto> Items, int TotalCount)> GetMyClassesAsync(
        string? search, string? sort, int page, int pageSize, string? expand)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role != AppRole.Student)
            throw new UnauthorizedAccessException("Only students can view their own classes.");

        var query = _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == user.Id &&
                        e.Status != EnrollmentStatus.Dropped &&
                        e.Class.DeletedAt == null)
            .Select(e => e.Class);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.CourseName.ToLower().Contains(s) ||
                (c.CourseCode != null && c.CourseCode.ToLower().Contains(s)) ||
                c.Semester.ToLower().Contains(s) ||
                c.AcademicYear.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await ApplySort(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

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

        ValidateClassFields(dto.CourseName, dto.CourseCode, dto.Semester, dto.AcademicYear, dto.StartDate, dto.EndDate);
        await EnsureLecturerBelongsToInstitutionAsync(dto.LecturerId, dto.InstitutionId);

        var entity = new Class
        {
            Id = Guid.NewGuid(),
            InstitutionId = dto.InstitutionId,
            LecturerId = dto.LecturerId,
            CourseName = dto.CourseName.Trim(),
            CourseCode = NormalizeOptional(dto.CourseCode),
            Semester = dto.Semester.Trim(),
            AcademicYear = dto.AcademicYear.Trim(),
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

        ValidateClassFields(dto.CourseName, dto.CourseCode, dto.Semester, dto.AcademicYear, dto.StartDate, dto.EndDate);
        if (dto.LecturerId != entity.LecturerId)
            await EnsureLecturerBelongsToInstitutionAsync(dto.LecturerId, entity.InstitutionId);

        entity.CourseName = dto.CourseName.Trim();
        entity.CourseCode = NormalizeOptional(dto.CourseCode);
        entity.Semester = dto.Semester.Trim();
        entity.AcademicYear = dto.AcademicYear.Trim();
        entity.LecturerId = dto.LecturerId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await PublishClassChangedAsync(entity, "updated");
        return true;
    }

    private static readonly Regex HasLetterRegex = new(@"[A-Za-zÀ-ỹ]", RegexOptions.Compiled);
    private static readonly Regex HasDigitRegex = new(@"\d", RegexOptions.Compiled);

    private static void ValidateClassFields(
        string courseName, string? courseCode, string semester, string academicYear,
        DateOnly? startDate, DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(courseName))
            throw new InvalidOperationException("Course name is required.");
        var trimmedName = courseName.Trim();
        if (char.IsDigit(trimmedName[0]))
            throw new InvalidOperationException("Course name cannot start with a number.");

        if (!string.IsNullOrWhiteSpace(courseCode))
        {
            var trimmedCode = courseCode.Trim();
            if (char.IsDigit(trimmedCode[0]))
                throw new InvalidOperationException("Course code cannot start with a number.");
            if (!HasLetterRegex.IsMatch(trimmedCode) || !HasDigitRegex.IsMatch(trimmedCode))
                throw new InvalidOperationException("Course code must contain both letters and numbers.");
        }

        if (string.IsNullOrWhiteSpace(semester))
            throw new InvalidOperationException("Semester is required.");
        if (HasDigitRegex.IsMatch(semester))
            throw new InvalidOperationException("Semester cannot contain numbers.");

        if (string.IsNullOrWhiteSpace(academicYear))
            throw new InvalidOperationException("Academic year is required.");
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            throw new InvalidOperationException("End date cannot be before start date.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureLecturerBelongsToInstitutionAsync(Guid lecturerId, Guid institutionId)
    {
        var lecturer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Id == lecturerId &&
                u.Role == AppRole.Lecturer &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active);
        if (lecturer == null)
            throw new InvalidOperationException("Lecturer not found.");
        if (lecturer.InstitutionId != institutionId)
            throw new InvalidOperationException("Lecturer does not belong to this institution.");
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

    private static IQueryable<Class> ApplySort(IQueryable<Class> query, string? sort) =>
        (sort ?? "-createdAt").ToLower() switch
        {
            "coursename" => query.OrderBy(c => c.CourseName),
            "-coursename" => query.OrderByDescending(c => c.CourseName),
            "createdat" => query.OrderBy(c => c.CreatedAt),
            "-createdat" => query.OrderByDescending(c => c.CreatedAt),
            "semester" => query.OrderBy(c => c.Semester),
            "-semester" => query.OrderByDescending(c => c.Semester),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };
}
