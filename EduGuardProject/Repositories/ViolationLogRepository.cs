using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ViolationLogRepository : IViolationLogRepository
{
    private readonly AppDbContext _context;

    public ViolationLogRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<ViolationLog> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,Guid? participationId = null)
    {
        var query = _context.ViolationLogs
            .AsNoTracking()
            .AsQueryable();

        if (participationId.HasValue)
            query = query.Where(v => v.ParticipationId == participationId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                (v.EvidencePath != null && v.EvidencePath.ToLower().Contains(s)) ||
                (v.ReviewedBy != null && _context.Users.Any(u => u.Id == v.ReviewedBy && u.FullName.ToLower().Contains(s))));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<ViolationLog?> GetByIdAsync(Guid id) =>
        _context.ViolationLogs.FirstOrDefaultAsync(v => v.Id == id);

    public async Task AddViolationLog(ViolationLog entity)
    {
        await _context.ViolationLogs.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ViolationLog entity)
    {
        _context.ViolationLogs.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ViolationLog entity)
    {
        _context.ViolationLogs.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<ViolationLog> ApplySort(IQueryable<ViolationLog> query, string? sort) =>
        (sort ?? "-recordedAt").ToLower() switch
        {
            "recordedat" => query.OrderBy(v => v.RecordedAt),
            "-recordedat" => query.OrderByDescending(v => v.RecordedAt),
            "aiconfidence" => query.OrderBy(v => v.AiConfidence),
            "-aiconfidence" => query.OrderByDescending(v => v.AiConfidence),
            "isreviewed" => query.OrderBy(v => v.IsReviewed),
            "-isreviewed" => query.OrderByDescending(v => v.IsReviewed),
            _ => query.OrderByDescending(v => v.RecordedAt)
        };
}
