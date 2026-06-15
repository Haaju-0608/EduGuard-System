using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace EduGuardProject.Repositories;

public class BiometricDatumRepository : IBiometricDatumRepository
{
    private readonly AppDbContext _context;

    public BiometricDatumRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<BiometricDatum> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? userId = null, bool? isActive = null)
    {
        var query = _context.BiometricData.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(b => b.UserId == userId.Value);
        if (isActive.HasValue)
            query = query.Where(b => b.IsActive == isActive.Value);
        

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(b =>
                b.ModelVersion.ToLower().Contains(s) ||
                (b.FaceImageUrl != null && b.FaceImageUrl.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<BiometricDatum?> GetByIdAsync(Guid id) =>
        _context.BiometricData.FirstOrDefaultAsync(b => b.Id == id);

    public async Task AddAsync(BiometricDatum entity)
    {
        await _context.BiometricData.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BiometricDatum entity)
    {
        _context.BiometricData.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(BiometricDatum entity)
    {
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.BiometricData.Update(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<BiometricDatum> ApplySort(IQueryable<BiometricDatum> query, string? sort) =>
        (sort ?? "-createdAt").ToLower() switch
        {
            "createdat" => query.OrderBy(b => b.CreatedAt),
            "-createdat" => query.OrderByDescending(b => b.CreatedAt),
            "modelversion" => query.OrderBy(b => b.ModelVersion),
            "-modelversion" => query.OrderByDescending(b => b.ModelVersion),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

    public async Task<BiometricDatum?> FindClosestMatchAsync(Pgvector.Vector currentFaceVector, double threshold)
    {
        var matched = await _context.BiometricData
            .FromSqlRaw(@"
            SELECT * FROM biometric_data 
            WHERE is_active = true AND (face_vector <-> {0}) < {1}
            ORDER BY face_vector <-> {0} 
            LIMIT 1", currentFaceVector, threshold)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return matched;
    }
}
