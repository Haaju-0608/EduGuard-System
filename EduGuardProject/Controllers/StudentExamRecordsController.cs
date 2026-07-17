using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/student-exam-records")]
[ApiController]
[SupabaseAuthorize]
public class StudentExamRecordsController : AcademicApiControllerBase
{
    private readonly IStudentExamRecordService _service;

    public StudentExamRecordsController(IStudentExamRecordService service) => _service = service;

    // Roles: all authenticated roles; Student only sees their own records. Returns: paged StudentExamRecordResponseDto list.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] Guid? examSlotId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] StudentExamRecordStatus? status = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, examSlotId, studentId, status);
            return OkPaged(items, page, pageSize, total, "Student exam records retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: all authenticated roles with scoped access. Returns: one StudentExamRecordResponseDto.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Student exam record not found."));
            return OkSingle(item, "Student exam record retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: created StudentExamRecordResponseDto.
    [HttpPost]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateStudentExamRecordDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Student exam record created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Student. Returns: StudentExamRecordResponseDto with score/status; examRecord does not expose correct answers.
    [HttpPost("submit")]
    [SupabaseAuthorize(AppRole.Student)]
    public async Task<IActionResult> Submit([FromBody] SubmitStudentExamRecordDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.SubmitAsync(dto);
            return OkSingle(result, "Student exam record submitted successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: success message only.
    [HttpPut("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentExamRecordDto dto)
    {
        try
        {
            var success = await _service.UpdateAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Student exam record not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Student exam record updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Roles: Lecturer, SchoolAdmin, SuperAdmin. Returns: success message only.
    [HttpDelete("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Student exam record not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Student exam record deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
