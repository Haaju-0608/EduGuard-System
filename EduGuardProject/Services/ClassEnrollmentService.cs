using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class ClassEnrollmentService : IClassEnrollmentService
{
    private readonly IClassEnrollmentRepository _repo;
    private readonly IClassRepository _classRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly AppDbContext _context;

    public ClassEnrollmentService(
        IClassEnrollmentRepository repo,
        IClassRepository classRepo,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        AppDbContext context)
    {
        _repo = repo;
        _classRepo = classRepo;
        _currentUser = currentUser;
        _realtime = realtime;
        _context = context;
    }

    public async Task<(IEnumerable<ClassEnrollmentResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? classId = null, Guid? studentId = null)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        Guid? institutionId = null;
        IReadOnlyCollection<Guid>? lecturerClassIds = null;

        if (role == AppRole.Student)
            studentId = user.Id;
        else if (role == AppRole.Lecturer)
        {
            lecturerClassIds = await _context.Classes.AsNoTracking()
                .Where(c => c.LecturerId == user.Id && c.DeletedAt == null)
                .Select(c => c.Id)
                .ToListAsync();
            if (!lecturerClassIds.Any())
                return ([], 0);
        }
        else if (role == AppRole.SchoolAdmin)
            institutionId = user.InstitutionId;

        var (items, total) = await _repo.GetAllAsync(
            search, sort, page, pageSize, classId, studentId,
            institutionId: institutionId, classIds: lecturerClassIds);
        var dtos = new List<ClassEnrollmentResponseDto>();
        foreach (var item in items)
        {
            if (role != AppRole.SuperAdmin)
                await EnsureEnrollmentAccessAsync(item);
            dtos.Add(await AcademicMapper.MapEnrollmentAsync(_context, item, expand));
        }
        return (dtos, total);
    }

    public async Task<ClassEnrollmentResponseDto?> GetByKeyAsync(Guid classId, Guid studentId, string? expand)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return null;
        await EnsureEnrollmentAccessAsync(entity);
        return await AcademicMapper.MapEnrollmentAsync(_context, entity, expand);
    }

    public async Task<ClassEnrollmentResponseDto> CreateAsync(CreateClassEnrollmentDto dto)
    {
        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);

        if (!Enum.IsDefined(dto.Status))
            throw new InvalidOperationException("Invalid enrollment status.");
        if (dto.Status == EnrollmentStatus.Dropped)
            throw new InvalidOperationException("New enrollments cannot start as DROPPED.");

        var cls = await _classRepo.GetByIdAsync(dto.ClassId);
        if (cls == null) throw new InvalidOperationException("Class not found.");

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("You can only enroll students in your own classes.");

        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        var student = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dto.StudentId && u.DeletedAt == null);
        if (student == null) throw new InvalidOperationException("Student not found.");
        if (student.Role != AppRole.Student)
            throw new InvalidOperationException("User is not a student.");

        var existing = await _repo.GetByKeyAsync(dto.ClassId, dto.StudentId);
        if (existing != null)
        {
            if (existing.Status == EnrollmentStatus.Dropped)
            {
                existing.Status = EnrollmentStatus.Active;
                existing.EnrolledAt = DateTime.UtcNow;
                await _repo.UpdateAsync(existing);
                await PublishEnrollmentChangedAsync(existing, "reactivated");
                return await AcademicMapper.MapEnrollmentAsync(_context, existing, null);
            }
            throw new InvalidOperationException("Student is already enrolled in this class.");
        }

        var entity = new ClassEnrollment
        {
            ClassId = dto.ClassId,
            StudentId = dto.StudentId,
            Status = dto.Status,
            EnrolledAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity);
        await PublishEnrollmentChangedAsync(entity, "created");
        return await AcademicMapper.MapEnrollmentAsync(_context, entity, null);
    }

    public async Task<ImportClassEnrollmentsResponseDto> ImportFromExcelAsync(
        Guid classId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Excel file is required.");
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("Excel file must not exceed 5 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .xlsx files are supported.");

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        var cls = await _classRepo.GetByIdAsync(classId)
            ?? throw new InvalidOperationException("Class not found.");
        var currentUser = await _currentUser.GetRequiredUserAsync();
        if (currentUser.Role == AppRole.Lecturer && cls.LecturerId != currentUser.Id)
            throw new UnauthorizedAccessException("You can only enroll students in your own classes.");
        await _currentUser.EnsureInstitutionAccessAsync(cls.InstitutionId);

        await using var stream = file.OpenReadStream();
        var rows = ReadClassEnrollmentRows(stream);
        if (rows.Count == 0)
            throw new ArgumentException("The Excel file does not contain any student rows.");
        if (rows.Count > 500)
            throw new ArgumentException("A single import is limited to 500 student rows.");

        var studentCodes = rows.Select(row => row.StudentCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var studentsByCode = (await _context.Users.AsNoTracking()
            .Where(user => user.InstitutionId == cls.InstitutionId &&
                           user.Role == AppRole.Student &&
                           user.DeletedAt == null &&
                           user.StudentCode != null &&
                           studentCodes.Contains(user.StudentCode))
            .ToListAsync(cancellationToken))
            .ToDictionary(user => user.StudentCode!, StringComparer.OrdinalIgnoreCase);

        var result = new ImportClassEnrollmentsResponseDto { Total = rows.Count };
        var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var rowResult = new ImportClassEnrollmentRowResultDto
            {
                Row = row.Row,
                StudentCode = row.StudentCode,
                FullName = row.FullName
            };

            if (!importedCodes.Add(row.StudentCode))
                rowResult.Error = "StudentCode is duplicated in the Excel file.";
            else if (!studentsByCode.TryGetValue(row.StudentCode, out var student))
                rowResult.Error = "Student was not found in this institution.";
            else if (!string.Equals(student.FullName.Trim(), row.FullName, StringComparison.OrdinalIgnoreCase))
                rowResult.Error = "FullName does not match the student code.";
            else
            {
                try
                {
                    await CreateAsync(new CreateClassEnrollmentDto
                    {
                        ClassId = classId,
                        StudentId = student.Id,
                        Status = EnrollmentStatus.Active
                    });
                    rowResult.Success = true;
                    result.Succeeded++;
                }
                catch (Exception ex)
                {
                    rowResult.Error = ex.Message;
                }
            }

            if (!rowResult.Success) result.Failed++;
            result.Results.Add(rowResult);
        }

        return result;
    }

    public async Task<bool> UpdateAsync(Guid classId, Guid studentId, UpdateClassEnrollmentDto dto)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureEnrollmentAccessAsync(entity);

        if (!Enum.IsDefined(dto.Status))
            throw new InvalidOperationException("Invalid enrollment status.");

        entity.Status = dto.Status;
        await _repo.UpdateAsync(entity);
        await PublishEnrollmentChangedAsync(entity, "updated");
        return true;
    }

    public async Task<bool> DeleteAsync(Guid classId, Guid studentId)
    {
        var entity = await _repo.GetByKeyAsync(classId, studentId);
        if (entity == null || entity.Status == EnrollmentStatus.Dropped) return false;

        await _currentUser.EnsureRoleAsync(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin);
        await EnsureEnrollmentAccessAsync(entity);

        await _repo.SoftDeleteAsync(entity);
        await PublishEnrollmentChangedAsync(entity, "deleted");
        return true;
    }

    private async Task PublishEnrollmentChangedAsync(ClassEnrollment entity, string action)
    {
        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        await _realtime.PublishDataChangedAsync(
            "class-enrollments",
            action,
            institutionId: cls?.InstitutionId,
            lecturerId: cls?.LecturerId,
            userId: entity.StudentId,
            data: new
            {
                entity.ClassId,
                entity.StudentId,
                entity.Status,
                entity.EnrolledAt
            });
    }

    private static List<ImportClassEnrollmentRow> ReadClassEnrollmentRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook does not contain a worksheet.");
        var headerRow = sheet.FirstRowUsed()
            ?? throw new ArgumentException("The workbook is empty.");
        var headers = headerRow.CellsUsed().ToDictionary(
            cell => NormalizeExcelHeader(cell.GetString()),
            cell => cell.Address.ColumnNumber);
        var requiredHeaders = new[] { "studentcode", "fullname" };
        var missingHeaders = requiredHeaders.Where(header => !headers.ContainsKey(header)).ToList();
        if (missingHeaders.Count > 0)
            throw new ArgumentException($"Missing required columns: {string.Join(", ", missingHeaders)}.");

        var rows = new List<ImportClassEnrollmentRow>();
        foreach (var row in sheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            var studentCode = row.Cell(headers["studentcode"]).GetFormattedString().Trim();
            var fullName = row.Cell(headers["fullname"]).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(fullName))
                continue;
            if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException($"Row {row.RowNumber()} must include StudentCode and FullName.");

            rows.Add(new ImportClassEnrollmentRow(row.RowNumber(), studentCode, fullName));
        }

        return rows;
    }

    private static string NormalizeExcelHeader(string value) =>
        value.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant();

    private sealed record ImportClassEnrollmentRow(int Row, string StudentCode, string FullName);

    private async Task EnsureEnrollmentAccessAsync(ClassEnrollment entity)
    {
        var user = await _currentUser.GetRequiredUserAsync();
        var role = user.Role.ToCanonical();
        if (role == AppRole.SuperAdmin) return;

        if (role == AppRole.Student && entity.StudentId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");

        var cls = await _classRepo.GetByIdAsync(entity.ClassId);
        if (cls == null) return;

        if (user.InstitutionId != cls.InstitutionId)
            throw new UnauthorizedAccessException("Access denied.");

        if (role == AppRole.Lecturer && cls.LecturerId != user.Id)
            throw new UnauthorizedAccessException("Access denied.");
    }
}
