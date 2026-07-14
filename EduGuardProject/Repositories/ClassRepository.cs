using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ClassRepository : IClassRepository
{
    private readonly AppDbContext _context;

    public ClassRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<Class> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, Guid? institutionId = null, Guid? lecturerId = null)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Where(c => c.DeletedAt == null);

        if (institutionId.HasValue)
            query = query.Where(c => c.InstitutionId == institutionId.Value);
        if (lecturerId.HasValue)
            query = query.Where(c => c.LecturerId == lecturerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.CourseName.ToLower().Contains(s) ||
                (c.CourseCode != null && c.CourseCode.ToLower().Contains(s)) ||
                c.Semester.ToLower().Contains(s) ||
                c.AcademicYear.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Class?> GetByIdAsync(Guid id, bool includeDeleted = false)
    {
        var query = _context.Classes.AsQueryable();
        if (!includeDeleted)
            query = query.Where(c => c.DeletedAt == null);
        return await query.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Class entity)
    {
        await _context.Classes.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Class entity)
    {
        _context.Classes.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(Class entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Classes.Update(entity);
        await _context.SaveChangesAsync();
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
