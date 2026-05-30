using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class BiometricRequestRepository : IBiometricRequestRepository
{
    private readonly AppDbContext _context;

    public BiometricRequestRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<BiometricRequest> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? studentId = null, BiometricReqStatus? status = null)
    {
        var query = _context.BiometricRequests
            .AsNoTracking()
            .Where(r => r.Status != BiometricReqStatus.Rejected);

        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r => r.Reason.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<BiometricRequest?> GetByIdAsync(Guid id) =>
        _context.BiometricRequests.FirstOrDefaultAsync(r => r.Id == id);

    public async Task AddAsync(BiometricRequest entity)
    {
        await _context.BiometricRequests.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BiometricRequest entity)
    {
        _context.BiometricRequests.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(BiometricRequest entity)
    {
        entity.Status = BiometricReqStatus.Rejected;
        entity.ReviewedAt = DateTime.UtcNow;
        _context.BiometricRequests.Update(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<BiometricRequest> ApplySort(IQueryable<BiometricRequest> query, string? sort) =>
        (sort ?? "-createdAt").ToLower() switch
        {
            "createdat" => query.OrderBy(r => r.CreatedAt),
            "-createdat" => query.OrderByDescending(r => r.CreatedAt),
            "status" => query.OrderBy(r => r.Status),
            "-status" => query.OrderByDescending(r => r.Status),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };
}
