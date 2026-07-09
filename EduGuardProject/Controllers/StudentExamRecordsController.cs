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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentExamRecordDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Student exam record created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{id:guid}")]
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

    [HttpDelete("{id:guid}")]
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
