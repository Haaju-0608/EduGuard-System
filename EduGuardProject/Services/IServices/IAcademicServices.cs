using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;

namespace EduGuardProject.Services.IServices;

public interface IClassService
{
    Task<(IEnumerable<ClassResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand);
    Task<(IEnumerable<ClassResponseDto> Items, int TotalCount)> GetMyClassesAsync(
        string? search, string? sort, int page, int pageSize, string? expand);
    Task<ClassResponseDto?> GetByIdAsync(Guid id, string? expand);
    Task<ClassResponseDto> CreateAsync(CreateClassDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateClassDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IClassEnrollmentService
{
    Task<(IEnumerable<ClassEnrollmentResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? classId = null, Guid? studentId = null);
    Task<ClassEnrollmentResponseDto?> GetByKeyAsync(Guid classId, Guid studentId, string? expand);
    Task<ClassEnrollmentResponseDto> CreateAsync(CreateClassEnrollmentDto dto);
    Task<bool> UpdateAsync(Guid classId, Guid studentId, UpdateClassEnrollmentDto dto);
    Task<bool> DeleteAsync(Guid classId, Guid studentId);
}

public interface IAttendanceSessionService
{
    Task<(IEnumerable<AttendanceSessionResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? classId = null);
    Task<AttendanceSessionResponseDto?> GetByIdAsync(Guid id, string? expand);
    Task<AttendanceSessionResponseDto> CreateAsync(CreateAttendanceSessionDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateAttendanceSessionDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IAttendanceRecordService
{
    Task<(IEnumerable<AttendanceRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand,
        Guid? sessionId = null, Guid? studentId = null);
    Task<AttendanceRecordResponseDto?> GetByIdAsync(Guid id, string? expand);
    Task<AttendanceRecordResponseDto> CreateAsync(CreateAttendanceRecordDto dto);
    Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkManualAsync(Guid sessionId, BulkManualAttendanceDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateAttendanceRecordDto dto);
    Task<bool> DeleteAsync(Guid id);

    Task<IEnumerable<AttendanceRecordResponseDto>> CreateBulkByAiVideoAsync(Guid sessionId, Stream videoStream, string fileName);
}

public interface IBiometricRequestService
{
    Task<(IEnumerable<BiometricRequestResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? studentId = null);
    Task<BiometricRequestResponseDto?> GetByIdAsync(Guid id, string? expand);
    Task<BiometricRequestResponseDto> CreateAsync(CreateBiometricRequestDto dto);
    Task<bool> ApproveAsync(Guid id, ReviewBiometricRequestDto? dto);
    Task<bool> RejectAsync(Guid id, ReviewBiometricRequestDto? dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IBiometricDatumService
{
    Task<(IEnumerable<BiometricDatumResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, string? expand, Guid? userId = null);
    Task<BiometricDatumResponseDto?> GetByIdAsync(Guid id, string? expand);
    Task<BiometricDatumResponseDto> CreateAsync(CreateBiometricDatumDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateBiometricDatumDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IExamParticipationService
{
    Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllExamparticipationsAsync(
        string? search, string? sort, int page, int pageSize, Guid? examSlotId = null);
    Task<ExamParticipationResponseDto?> GetByIdAsync(Guid id);
    Task<ExamParticipation> CreateAsync(CreateExamParticipationDto dto);

    Task<bool> UpdateAsync(Guid id, UpdateExamParticipationDto dto);
    Task<bool> UpdateAsyncOnlyExamPartipationStatus(Guid examSlotId, UpdateExamParticipationStatusDto dto);

    Task<bool> DeleteAsync(Guid id);
}

public interface IStudentExamRecordService
{
    Task<(IEnumerable<StudentExamRecordResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? examSlotId = null, Guid? studentId = null, StudentExamRecordStatus? status = null);
    Task<StudentExamRecordResponseDto?> GetByIdAsync(Guid id);
    Task<StudentExamRecordResponseDto> CreateAsync(CreateStudentExamRecordDto dto);
    Task<StudentExamRecordResponseDto> SubmitAsync(SubmitStudentExamRecordDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateStudentExamRecordDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IExamQuestionService
{
    Task<(IEnumerable<ExamQuestionResponseDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize, Guid? examSlotId = null);
    Task<ExamQuestionResponseDto?> GetByIdAsync(Guid id);
    Task<ExamQuestionResponseDto> CreateAsync(CreateExamQuestionDto dto);
    Task<ExamQuestionResponseDto?> UpdateAsync(Guid id, UpdateExamQuestionDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<QuestionOptionResponseDto> CreateOptionAsync(Guid questionId, CreateQuestionOptionDto dto);
    Task<QuestionOptionResponseDto?> UpdateOptionAsync(Guid optionId, UpdateQuestionOptionDto dto);
    Task<bool> DeleteOptionAsync(Guid optionId);
}

public interface IExamSlotServices
{
    Task<(IEnumerable<ExamslotReponseDto> Items, int TotalCount)> GetAllExamSlotsAsync(string? search, string? sort, int page, int pageSizel);

    Task<ExamslotReponseDto?> GetByIdAsync(Guid id);

    Task<ExamslotReponseDto?> GetByIdForStudentAsync(Guid id, Guid studentId);

    Task<IEnumerable<ExamslotReponseDto>> GetByClassIdAsync(Guid classId);

    Task<IEnumerable<ExamslotReponseDto>> GetByStudentIdAsync(Guid studentId);

    Task<IEnumerable<ExamslotReponseDto>> GetMyExamHistoryAsync();

    Task<ExamslotReponseDto> CreateAsync(CreateExamSlotDto dto);

    Task<ExamslotReponseDto?> UpdateAsync(Guid ExamId, UpdateExamSlotDto dto);

    Task<bool> DeleteAsync(Guid ExamId);
}
public interface IViolationLogService
{
    Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetAllAsync(
        string? search, string? sort, int page, int pageSize,
        Guid? participationId = null, bool? isReviewed = null);
    Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByExamSlotAsync(
        Guid examSlotId, string? search, string? sort, int page, int pageSize);
    Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByExamSlotAndStudentAsync(
        Guid examSlotId, Guid studentId, string? search, string? sort, int page, int pageSize);
    Task<(IEnumerable<ViolationlogResponeDto> Items, int TotalCount)> GetByStudentIdAsync(
        Guid studentId, string? search, string? sort, int page, int pageSize);

    Task<ViolationlogResponeDto?> GetByIdAsync(Guid id);

    Task<ViolationlogResponeDto> CreateAsync(CreateViolationLogDto dto);

    Task<bool> UpdateAsync(Guid id, UpdateViolationLogDto dto);

    Task<bool> DeleteAsync(Guid id);
}
