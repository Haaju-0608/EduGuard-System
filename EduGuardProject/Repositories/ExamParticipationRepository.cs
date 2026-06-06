using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class ExamParticipationRepository : IExamParticipationRepository
{
    private readonly AppDbContext _context;

    public ExamParticipationRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllAsync(string? search, string? sort, int page, int pageSize)
    {
        var query = _context.ExamParticipations
            .AsNoTracking()
            .AsQueryable();

        var totalCount = await query.CountAsync();
        query = ApplySort(query, sort);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items.Select(MapToResponseDto), totalCount);
    }

    private static ExamParticipationResponseDto MapToResponseDto(ExamParticipation e) => new()
    {
        Id = e.Id,
        ExamSlotId = e.ExamSlotId,
        StudentId = e.StudentId,
        BillingTransId = e.BillingTransId,
        ActualStart = e.ActualStart,
        ActualEnd = e.ActualEnd,
        Status = e.Status,
        RecordingVideoPath = e.RecordingVideoPath,
        IdentitySnapshotPath = e.IdentitySnapshotPath
    };
    public Task<ExamParticipation?> GetByIdAsync(Guid examSlotId) =>
        _context.ExamParticipations.FirstOrDefaultAsync(p => p.ExamSlotId == examSlotId);

    public Task<ExamParticipation?> GetByExamSlotAndStudentAsync(Guid examSlotId, Guid studentId) =>
        _context.ExamParticipations.FirstOrDefaultAsync(p => p.ExamSlotId == examSlotId && p.StudentId == studentId);

    public async Task AddAsync(ExamParticipation entity)
    {
        await _context.ExamParticipations.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<ExamParticipation> entities)
    {
        await _context.ExamParticipations.AddRangeAsync(entities);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ExamParticipation entity)
    {
        _context.ExamParticipations.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ExamParticipation entity)
    {
        _context.ExamParticipations.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static IQueryable<ExamParticipation> ApplySort(IQueryable<ExamParticipation> query, string? sort) =>
        (sort ?? "-actualStart").ToLower() switch
        {
            "actualstart" => query.OrderBy(p => p.ActualStart),
            "-actualstart" => query.OrderByDescending(p => p.ActualStart),
            "actualend" => query.OrderBy(p => p.ActualEnd),
            "-actualend" => query.OrderByDescending(p => p.ActualEnd),
            "status" => query.OrderBy(p => p.Status),
            "-status" => query.OrderByDescending(p => p.Status),
            _ => query.OrderByDescending(p => p.ActualStart)
        };
}
