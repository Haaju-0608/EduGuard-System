using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Authorize]
[Route("api/attendance-records")]
[ApiController]
public class AttendanceRecordsController : AcademicApiControllerBase
{
    private readonly IAttendanceRecordService _service;

    public AttendanceRecordsController(IAttendanceRecordService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null,
        [FromQuery] Guid? sessionId = null,
        [FromQuery] Guid? studentId = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, expand, sessionId, studentId);
            return OkPaged(items, page, pageSize, total, "Attendance records retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null, [FromQuery] string? expand = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id, expand);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Attendance record not found."));
            return OkSingle(item, "Attendance record retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceRecordDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Attendance record created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("~/api/attendance-sessions/{sessionId:guid}/records/bulk")]
    public async Task<IActionResult> CreateBulkManual(Guid sessionId, [FromBody] BulkManualAttendanceDto dto)
    {
        try
        {
            var results = await _service.CreateBulkManualAsync(sessionId, dto);
            return StatusCode(201, ApiResponse<IEnumerable<AttendanceRecordResponseDto>>.OnSuccess(
                results, "Manual attendance recorded successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAttendanceRecordDto dto)
    {
        try
        {
            var success = await _service.UpdateAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Attendance record not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Attendance record updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Attendance record not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Attendance record deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
