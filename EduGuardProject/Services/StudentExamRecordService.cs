using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class StudentExamRecordService : IStudentExamRecordService
{
    private readonly IStudentExamRecordRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public StudentExamRecordService(
        IStudentExamRecordRepository repo,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _repo = repo;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<(IEnumerable<StudentExamRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? studentId = null, StudentExamRecordStatus? status = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var institutionId = user.Role == AppRole.SchoolAdmin
            ? user.InstitutionId ?? throw new UnauthorizedAccessException("School admin is not assigned to an institution.")
            : (Guid?)null;
        var lecturerId = user.Role == AppRole.Lecturer ? user.Id : (Guid?)null;
        if (user.Role == AppRole.Student)
            studentId = user.Id;

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, examSlotId, studentId, status, institutionId, lecturerId);
        return (items.Select(MapToResponseDto), total);
    }

    public async Task<StudentExamRecordResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;
        await EnsureAccessAsync(entity);
        return MapToResponseDto(entity);
    }

    public async Task<StudentExamRecordResponseDto> CreateAsync(CreateStudentExamRecordDto dto)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && dto.StudentId != user.Id)
            throw new UnauthorizedAccessException("Students can only create their own exam records.");

        var examSlot = await _context.ExamSlots
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == dto.ExamSlotId)
            ?? throw new InvalidOperationException("Exam slot not found.");

        var student = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == dto.StudentId && u.Role == AppRole.Student && u.DeletedAt == null)
            ?? throw new InvalidOperationException("Student not found.");

        if (user.Role != AppRole.Student)
            await EnsureClassAccessAsync(examSlot.Class, user);

        if (user.Role != AppRole.SuperAdmin && student.InstitutionId != examSlot.Class.InstitutionId)
            throw new UnauthorizedAccessException("Student does not belong to this institution.");

        var entity = new StudentExamRecord
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            StudentId = dto.StudentId,
            CreatedAt = DateTime.UtcNow,
            EndedAt = dto.EndedAt,
            ExamRecord = dto.ExamRecord,
            Status = dto.Status
        };

        await _repo.AddAsync(entity);
        entity.ExamSlot = examSlot;
        entity.Student = student;
        return MapToResponseDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateStudentExamRecordDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await EnsureAccessAsync(entity);
        entity.EndedAt = dto.EndedAt;
        entity.ExamRecord = dto.ExamRecord;
        entity.Status = dto.Status;

        await _repo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;

        await EnsureAccessAsync(entity);
        entity.Status = StudentExamRecordStatus.Deleted;
        await _repo.UpdateAsync(entity);
        return true;
    }

    private async Task EnsureAccessAsync(StudentExamRecord entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Student && entity.StudentId == user.Id)
            return;

        await EnsureClassAccessAsync(entity.ExamSlot.Class, user);
    }

    private static Task EnsureClassAccessAsync(Class cls, User user)
    {
        if (user.Role == AppRole.SuperAdmin)
            return Task.CompletedTask;

        if (user.Role == AppRole.SchoolAdmin && user.InstitutionId == cls.InstitutionId)
            return Task.CompletedTask;

        if (user.Role == AppRole.Lecturer &&
            user.InstitutionId == cls.InstitutionId &&
            user.Id == cls.LecturerId)
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private static StudentExamRecordResponseDto MapToResponseDto(StudentExamRecord entity) => new()
    {
        Id = entity.Id,
        ExamSlotId = entity.ExamSlotId,
        ExamName = entity.ExamSlot?.ExamName,
        StudentId = entity.StudentId,
        StudentName = entity.Student?.FullName,
        CreatedAt = entity.CreatedAt,
        EndedAt = entity.EndedAt,
        ExamRecord = entity.ExamRecord,
        Status = entity.Status
    };
}
