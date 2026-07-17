using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Repositories;

public class StudentExamRecordRepository : IStudentExamRecordRepository
{
    private readonly AppDbContext _context;

    public StudentExamRecordRepository(AppDbContext context) => _context = context;

    public async Task<(IEnumerable<StudentExamRecord> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? studentId = null, StudentExamRecordStatus? status = null,
        Guid? institutionId = null, Guid? lecturerId = null)
    {
        var query = BaseQuery().AsNoTracking();

        if (examSlotId.HasValue)
            query = query.Where(r => r.ExamSlotId == examSlotId.Value);
        if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (institutionId.HasValue)
            query = query.Where(r => r.ExamSlot.Class.InstitutionId == institutionId.Value);
        if (lecturerId.HasValue)
            query = query.Where(r => r.ExamSlot.Class.LecturerId == lecturerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                r.ExamSlot.ExamName.ToLower().Contains(s) ||
                r.Student.FullName.ToLower().Contains(s) ||
                (r.Student.StudentCode != null && r.Student.StudentCode.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();
        var items = await ApplySort(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<StudentExamRecord?> GetByIdAsync(Guid id) =>
        BaseQuery().FirstOrDefaultAsync(r => r.Id == id);

    public async Task AddAsync(StudentExamRecord entity)
    {
        await _context.StudentExamRecords.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(StudentExamRecord entity)
    {
        _context.StudentExamRecords.Update(entity);
        await _context.SaveChangesAsync();
    }

    private IQueryable<StudentExamRecord> BaseQuery() =>
        _context.StudentExamRecords
            .Include(r => r.ExamSlot)
            .ThenInclude(e => e.Class)
            .Include(r => r.Student);

    private static IQueryable<StudentExamRecord> ApplySort(IQueryable<StudentExamRecord> query, string? sort) =>
        (sort ?? "-createdAt").ToLower() switch
        {
            "createdat" => query.OrderBy(r => r.CreatedAt),
            "-createdat" => query.OrderByDescending(r => r.CreatedAt),
            "endedat" => query.OrderBy(r => r.EndedAt),
            "-endedat" => query.OrderByDescending(r => r.EndedAt),
            "examname" => query.OrderBy(r => r.ExamSlot.ExamName),
            "-examname" => query.OrderByDescending(r => r.ExamSlot.ExamName),
            "studentname" => query.OrderBy(r => r.Student.FullName),
            "-studentname" => query.OrderByDescending(r => r.Student.FullName),
            "status" => query.OrderBy(r => r.Status),
            "-status" => query.OrderByDescending(r => r.Status),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };
}
