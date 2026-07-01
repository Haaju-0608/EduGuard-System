using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ExamSlotRepository : IExamslotRepository
{
    private readonly AppDbContext _context;

    public ExamSlotRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize)
    {
        var query = _context.ExamSlots
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(es =>
                es.ExamName.ToLower().Contains(s) ||
                _context.Classes.Any(c => c.Id == es.ClassId && c.CourseName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items.Select(MapToResponseDto), totalCount);
    }
    private static ExamslotReponseDto MapToResponseDto(ExamSlot e) => new()
    {
        Id = e.Id,
        ClassId = e.ClassId,
        ExamName = e.ExamName,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        ExpectedDurationMinutes = e.ExpectedDurationMinutes,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
    public Task<ExamSlot?> GetByIdAsync(Guid id) =>
        _context.ExamSlots.FirstOrDefaultAsync(s => s.Id == id);

    public async Task AddAsync(ExamSlot entity)
    {
        await _context.ExamSlots.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ExamSlot entity)
    {
        _context.ExamSlots.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ExamSlot entity)
    {
        _context.ExamSlots.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<ExamSlot> ApplySort(IQueryable<ExamSlot> query, string? sort) =>
        (sort ?? "-startTime").ToLower() switch
        {
            "starttime" => query.OrderBy(s => s.StartTime),
            "-starttime" => query.OrderByDescending(s => s.StartTime),
            "examname" => query.OrderBy(s => s.ExamName),
            "-examname" => query.OrderByDescending(s => s.ExamName),
            "endtime" => query.OrderBy(s => s.EndTime),
            "-endtime" => query.OrderByDescending(s => s.EndTime),
            "createdat" => query.OrderBy(s => s.CreatedAt),
            "-createdat" => query.OrderByDescending(s => s.CreatedAt),
            "expecteddurationminutes" => query.OrderBy(s => s.ExpectedDurationMinutes),
            "-expecteddurationminutes" => query.OrderByDescending(s => s.ExpectedDurationMinutes),
            "status" => query.OrderBy(s => s.Status),
            "-status" => query.OrderByDescending(s => s.Status),
            _ => query.OrderByDescending(s => s.StartTime)
        };
}
