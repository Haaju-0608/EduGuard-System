using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic.FileIO;
using System.Net.Mail;

namespace EduGuardProject.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly Supabase.Client _supabaseClient;
        private readonly IConfiguration _config;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly IStorageService _storage;
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;

        //  3. Tiêm IConfiguration vào để lấy Key tự động
        public UserService(
            IUserRepository repo,
            Supabase.Client supabaseClient,
            IConfiguration config,
            IRealtimeEventDispatcher realtime,
            IStorageService storage,
            AppDbContext context,
            IDistributedCache cache)
        {
            _repo = repo;
            _supabaseClient = supabaseClient;
            _config = config;
            _realtime = realtime;
            _storage = storage;
            _context = context;
            _cache = cache;
        }

        public async Task<(IEnumerable<UserResponseDto> Items, int TotalCount)> GetUsersAsync(
    Guid? institutionId, AppRole? excludeRole, string? search, string? sort, int page, int pageSize)
        {
            var (entities, totalCount) = await _repo.GetAllAsync(institutionId, excludeRole, search, sort, page, pageSize);
            var dtos = entities.Select(MapToResponseDto);
            return (dtos, totalCount);
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto)
        {
            var adminAttrs = new Supabase.Gotrue.AdminUserAttributes
            {
                Email = dto.Email,
                Password = dto.Password,
                EmailConfirm = true
            };

            var serviceKey = _config["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);

            // Supabase C# trả về thẳng User luôn, không lồng ghép rườm rà
            var authUser = await adminAuth.CreateUser(adminAttrs);

            if (authUser?.Id == null)
                throw new InvalidOperationException("Lỗi: Supabase không trả về ID người dùng.");

            var realUserId = Guid.Parse(authUser.Id); // Lấy thẳng ID

            // Vẫn chỉ định tên đầy đủ để không bị lú với Gotrue.User
            var entity = new EduGuardProject.Models.User
            {
                Id = realUserId,
                InstitutionId = dto.InstitutionId,
                StudentCode = dto.StudentCode,
                Email = dto.Email,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = dto.Role,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await Task.Delay(500);

            // 2. Kiểm tra xem Trigger dưới DB có "nhanh tay" tạo sẵn chưa
            var existingUser = await _repo.GetByIdAsync(realUserId);

            if (existingUser != null)
            {
                // Nếu Trigger đã tạo rồi -> Mình gọi lệnh Update đè thông tin lên
                existingUser.InstitutionId = dto.InstitutionId;
                existingUser.StudentCode = dto.StudentCode;
                existingUser.FullName = dto.FullName;
                existingUser.Phone = dto.Phone;
                existingUser.Role = dto.Role;
                existingUser.Status = dto.Status;
                existingUser.UpdatedAt = DateTime.UtcNow;

                await _repo.UpdateAsync(existingUser);
            }
            else
            {
                // Nếu chưa có (Tức là bạn đã tắt Trigger thành công) -> Mình tự Add mới
                await _repo.AddAsync(entity);
            }

            await PublishUserChangedAsync(entity, "created");
            return MapToResponseDto(entity);
        }

        public async Task<BulkImportUsersResponseDto> BulkImportUsersAsync(
            IFormFile file,
            Guid? forcedInstitutionId = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Import file is required.");
            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Import file must not exceed 5 MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".xlsx" and not ".csv")
                throw new ArgumentException("Only .xlsx and .csv files are supported.");

            await using var stream = file.OpenReadStream();
            var rows = extension == ".xlsx" ? ReadExcel(stream) : ReadCsv(stream);
            if (rows.Count == 0)
                throw new ArgumentException("The import file does not contain any data rows.");
            if (rows.Count > 500)
                throw new ArgumentException("A single import is limited to 500 data rows.");

            var existingEmailList = await _context.Users.AsNoTracking()
                .Select(u => u.Email)
                .ToListAsync(cancellationToken);
            var existingEmails = existingEmailList.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requestedInstitutionIds = forcedInstitutionId.HasValue
                ? new[] { forcedInstitutionId.Value }
                : rows.Select(r => Guid.TryParse(r.InstitutionId, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty).Distinct().ToArray();
            var requestedInstitutionIdSet = requestedInstitutionIds.ToHashSet();
            var validInstitutionIds = (await _context.Institutions.AsNoTracking()
                .Where(i => i.DeletedAt == null)
                .Select(i => i.Id)
                .ToListAsync(cancellationToken))
                .Where(requestedInstitutionIdSet.Contains)
                .ToHashSet();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var response = new BulkImportUsersResponseDto { Total = rows.Count };

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var dto = ValidateImportRow(row, forcedInstitutionId);
                    if (!dto.InstitutionId.HasValue || !validInstitutionIds.Contains(dto.InstitutionId.Value))
                        throw new ArgumentException("Institution does not exist or has been deleted.");
                    if (!seenEmails.Add(dto.Email))
                        throw new ArgumentException("Email is duplicated in the import file.");
                    if (existingEmails.Contains(dto.Email.ToLowerInvariant()))
                        throw new ArgumentException("Email already exists.");

                    var user = await CreateUserAsync(dto);
                    response.Results.Add(new BulkImportUserRowResultDto
                    {
                        Row = row.Number,
                        Email = dto.Email,
                        Success = true,
                        UserId = user.Id
                    });
                    response.Succeeded++;
                }
                catch (Exception ex)
                {
                    response.Results.Add(new BulkImportUserRowResultDto
                    {
                        Row = row.Number,
                        Email = row.Email,
                        Success = false,
                        Error = ex.Message
                    });
                    response.Failed++;
                }
            }

            return response;
        }

        private static List<ImportUserRow> ReadExcel(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new ArgumentException("The workbook does not contain a worksheet.");
            var headerRow = sheet.FirstRowUsed()
                ?? throw new ArgumentException("The workbook is empty.");
            var headers = headerRow.CellsUsed().ToDictionary(
                c => NormalizeHeader(c.GetString()), c => c.Address.ColumnNumber);
            EnsureHeaders(headers);

            return sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber())
                .Select(r => ToImportRow(r.RowNumber(), name => r.Cell(headers[name]).GetFormattedString()))
                .Where(r => !r.IsEmpty)
                .ToList();
        }

        private static List<ImportUserRow> ReadCsv(Stream stream)
        {
            using var parser = new TextFieldParser(stream)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");
            var headerValues = parser.ReadFields() ?? throw new ArgumentException("The CSV file is empty.");
            var headers = headerValues.Select((value, index) => (Name: NormalizeHeader(value), Index: index))
                .ToDictionary(x => x.Name, x => x.Index);
            EnsureHeaders(headers.ToDictionary(x => x.Key, x => x.Value + 1));

            var rows = new List<ImportUserRow>();
            var rowNumber = 1;
            while (!parser.EndOfData)
            {
                rowNumber++;
                var values = parser.ReadFields() ?? [];
                string Value(string name) => headers.TryGetValue(name, out var index) && index < values.Length ? values[index] : "";
                var row = ToImportRow(rowNumber, Value);
                if (!row.IsEmpty) rows.Add(row);
            }
            return rows;
        }

        private static void EnsureHeaders(IReadOnlyDictionary<string, int> headers)
        {
            var required = new[] { "email", "password", "fullname", "role" };
            var missing = required.Where(header => !headers.ContainsKey(header)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Missing required columns: {string.Join(", ", missing)}.");
        }

        private static ImportUserRow ToImportRow(int number, Func<string, string> value) => new(
            number,
            value("email").Trim(),
            value("password"),
            value("fullname").Trim(),
            value("role").Trim(),
            value("institutionid").Trim(),
            value("studentcode").Trim(),
            value("phone").Trim());

        private static CreateUserDto ValidateImportRow(ImportUserRow row, Guid? forcedInstitutionId)
        {
            if (string.IsNullOrWhiteSpace(row.Email)) throw new ArgumentException("Email is required.");
            try
            {
                var address = new MailAddress(row.Email);
                if (!address.Address.Equals(row.Email, StringComparison.OrdinalIgnoreCase))
                    throw new FormatException();
            }
            catch { throw new ArgumentException("Email format is invalid."); }
            if (row.Password.Length < 6) throw new ArgumentException("Password must be at least 6 characters.");
            if (string.IsNullOrWhiteSpace(row.FullName)) throw new ArgumentException("FullName is required.");

            var normalizedRole = row.Role.Replace("_", "").Replace(" ", "");
            var role = normalizedRole.Equals("student", StringComparison.OrdinalIgnoreCase)
                ? AppRole.Student
                : normalizedRole.Equals("lecturer", StringComparison.OrdinalIgnoreCase)
                    ? AppRole.Lecturer
                    : throw new ArgumentException("Role must be Student or Lecturer.");
            if (role == AppRole.Student && string.IsNullOrWhiteSpace(row.StudentCode))
                throw new ArgumentException("StudentCode is required for Student.");

            var institutionId = forcedInstitutionId;
            if (institutionId is null)
            {
                if (!Guid.TryParse(row.InstitutionId, out var parsedInstitutionId))
                    throw new ArgumentException("InstitutionId must be a valid GUID.");
                institutionId = parsedInstitutionId;
            }

            return new CreateUserDto
            {
                Email = row.Email.ToLowerInvariant(),
                Password = row.Password,
                FullName = row.FullName,
                Role = role,
                InstitutionId = institutionId,
                StudentCode = string.IsNullOrWhiteSpace(row.StudentCode) ? null : row.StudentCode,
                Phone = string.IsNullOrWhiteSpace(row.Phone) ? null : row.Phone,
                Status = UserStatus.Active
            };
        }

        private static string NormalizeHeader(string value) =>
            value.Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant();

        private sealed record ImportUserRow(
            int Number, string Email, string Password, string FullName, string Role,
            string InstitutionId, string StudentCode, string Phone)
        {
            public bool IsEmpty => string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(FullName);
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            if (dto.InstitutionId.HasValue)
                entity.InstitutionId = dto.InstitutionId.Value;

            if (dto.StudentCode is not null)
                entity.StudentCode = dto.StudentCode;

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                entity.FullName = dto.FullName;

            if (dto.Phone is not null)
                entity.Phone = dto.Phone;

            if (dto.Role.HasValue)
                entity.Role = dto.Role.Value;

            if (dto.Status.HasValue)
                entity.Status = dto.Status.Value;

            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));
            await PublishUserChangedAsync(entity, "updated");
            return true;
        }

        public async Task<bool> UpdateMyProfileAsync(Guid id, UpdateMyProfileDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            entity.FullName = dto.FullName.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));
            await PublishUserChangedAsync(entity, "profile-updated");
            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await DeleteStudentStorageAsync(entity);
            await _repo.DeleteAsync(entity);
            await _cache.RemoveAsync(CurrentUserService.ProfileCacheKey(entity.Id));

            var serviceKey = _config["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
            var adminAuth = _supabaseClient.AdminAuth(serviceKey);
            await adminAuth.DeleteUser(id.ToString());

            await PublishUserChangedAsync(entity, "deleted");
            return true;
        }

        private async Task DeleteStudentStorageAsync(EduGuardProject.Models.User user)
        {
            if (user.Role != AppRole.Student)
                return;

            var storageDeletes = new List<(string Bucket, string? Path)>();

            storageDeletes.AddRange(await _context.BiometricData
                .Where(b => b.UserId == user.Id && b.FaceImageUrl != null)
                .Select(b => new ValueTuple<string, string?>(StorageService.BiometricFacesBucket, b.FaceImageUrl))
                .ToListAsync());

            storageDeletes.AddRange(await _context.AttendanceRecords
                .Where(r => r.StudentId == user.Id && r.SnapshotPath != null)
                .Select(r => new ValueTuple<string, string?>(StorageService.AttendanceSnapshotsBucket, r.SnapshotPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ExamParticipations
                .Where(p => p.StudentId == user.Id && p.IdentitySnapshotPath != null)
                .Select(p => new ValueTuple<string, string?>(StorageService.ExamIdentityBucket, p.IdentitySnapshotPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ExamParticipations
                .Where(p => p.StudentId == user.Id && p.RecordingVideoPath != null)
                .Select(p => new ValueTuple<string, string?>(StorageService.ExamRecordingsBucket, p.RecordingVideoPath))
                .ToListAsync());

            storageDeletes.AddRange(await _context.ViolationLogs
                .Where(v => v.Participation.StudentId == user.Id && v.EvidencePath != null)
                .Select(v => new ValueTuple<string, string?>(StorageService.ExamEvidenceBucket, v.EvidencePath))
                .ToListAsync());

            foreach (var (bucket, path) in storageDeletes
                .Where(x => !string.IsNullOrWhiteSpace(x.Path))
                .Distinct())
            {
                await _storage.DeleteAsync(bucket, path!);
            }
        }

        private Task PublishUserChangedAsync(EduGuardProject.Models.User entity, string action) =>
            _realtime.PublishDataChangedAsync(
                "users",
                action,
                institutionId: entity.InstitutionId,
                userId: entity.Id,
                data: new
                {
                    userId = entity.Id,
                    entity.InstitutionId,
                    entity.Email,
                    entity.FullName,
                    entity.Role,
                    entity.Status
                });

        private static UserResponseDto MapToResponseDto(EduGuardProject.Models.User e) => new()
        {
            Id = e.Id,
            InstitutionId = e.InstitutionId,
            StudentCode = e.StudentCode,
            Email = e.Email,
            FullName = e.FullName,
            Phone = e.Phone,
            Role = e.Role,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };

        public async Task<StudentDetailResponseDto?> GetStudentDetailAsync(
    Guid studentId, Guid? requesterInstitutionId, bool isSuperAdmin)
        {
            var student = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == studentId && u.Role == AppRole.Student);
            if (student == null) return null;

            // SchoolAdmin chỉ xem được sinh viên cùng trường mình.
            if (!isSuperAdmin && student.InstitutionId != requesterInstitutionId)
                return null;

            var result = new StudentDetailResponseDto
            {
                Id = student.Id,
                FullName = student.FullName,
                Email = student.Email,
                StudentCode = student.StudentCode,
                Phone = student.Phone,
                Status = student.Status,
                InstitutionId = student.InstitutionId,
                CreatedAt = student.CreatedAt
            };

            // --- Trạng thái khuôn mặt ---
            var activeVectorCount = await _context.BiometricData
                .AsNoTracking()
                .CountAsync(b => b.UserId == studentId && b.IsActive);

            var latestRequest = await _context.BiometricRequests
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            result.Biometric = new BiometricStatusDto
            {
                HasActiveBiometric = activeVectorCount > 0,
                ActiveVectorCount = activeVectorCount,
                LatestRequestStatus = latestRequest?.Status,
                LatestRequestReviewedAt = latestRequest?.ReviewedAt,
                LatestRequestReason = latestRequest?.Reason
            };

            // --- Kết quả các bài thi ---
            result.ExamResults = await _context.StudentExamRecords
                .AsNoTracking()
                .Where(r => r.StudentId == studentId && r.Status != StudentExamRecordStatus.Deleted)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new StudentExamResultDto
                {
                    Id = r.Id,
                    ExamSlotId = r.ExamSlotId,
                    ExamName = r.ExamSlot.ExamName,
                    CourseName = r.ExamSlot.Class.CourseName,
                    FinalScore = r.FinalScore,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    DurationSeconds = r.DurationSeconds
                })
                .ToListAsync();

            // --- Các kỳ thi đã tham gia ---
            result.ExamParticipations = await _context.ExamParticipations
                .AsNoTracking()
                .Where(p => p.StudentId == studentId)
                .OrderByDescending(p => p.ActualStart)
                .Select(p => new StudentExamParticipationDto
                {
                    Id = p.Id,
                    ExamSlotId = p.ExamSlotId,
                    ExamName = p.ExamSlot.ExamName,
                    CourseName = p.ExamSlot.Class.CourseName,
                    Status = p.Status,
                    ActualStart = p.ActualStart,
                    ActualEnd = p.ActualEnd,
                    DisqualifiedReason = p.DisqualifiedReason,
                    IdentityVerified = p.IdentityVerifiedAt.HasValue
                })
                .ToListAsync();

            // --- Lịch sử điểm danh ---
            result.AttendanceHistory = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CheckinAt)
                .Select(r => new StudentAttendanceHistoryDto
                {
                    Id = r.Id,
                    SessionId = r.SessionId,
                    ClassId = r.Session.ClassId,
                    CourseName = r.Session.Class.CourseName,
                    Status = r.Status,
                    Method = r.Method,
                    CheckinAt = r.CheckinAt
                })
                .ToListAsync();

            return result;
        }
    }
}
