using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ClassEnrollmentRepository : IClassEnrollmentRepository
{
    private readonly AppDbContext _context;

    public ClassEnrollmentRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<ClassEnrollment> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? classId = null, Guid? studentId = null, EnrollmentStatus? status = null)
    {
        var query = _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.Status != EnrollmentStatus.Dropped);

        if (classId.HasValue)
            query = query.Where(e => e.ClassId == classId.Value);
        if (studentId.HasValue)
            query = query.Where(e => e.StudentId == studentId.Value);
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                _context.Users.Any(u => u.Id == e.StudentId && u.FullName.ToLower().Contains(search.ToLower())) ||
                _context.Classes.Any(c => c.Id == e.ClassId && c.CourseName.ToLower().Contains(search.ToLower())));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<ClassEnrollment?> GetByKeyAsync(Guid classId, Guid studentId) =>
        _context.ClassEnrollments.FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == studentId);

    public async Task AddAsync(ClassEnrollment entity)
    {
        await _context.ClassEnrollments.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ClassEnrollment entity)
    {
        _context.ClassEnrollments.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(ClassEnrollment entity)
    {
        entity.Status = EnrollmentStatus.Dropped;
        _context.ClassEnrollments.Update(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<ClassEnrollment> ApplySort(IQueryable<ClassEnrollment> query, string? sort) =>
        (sort ?? "-enrolledAt").ToLower() switch
        {
            "enrolledat" => query.OrderBy(e => e.EnrolledAt),
            "-enrolledat" => query.OrderByDescending(e => e.EnrolledAt),
            "status" => query.OrderBy(e => e.Status),
            "-status" => query.OrderByDescending(e => e.Status),
            _ => query.OrderByDescending(e => e.EnrolledAt)
        };
}
