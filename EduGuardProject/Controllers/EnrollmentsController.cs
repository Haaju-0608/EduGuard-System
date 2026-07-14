using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/enrollments")]
[ApiController]
public class EnrollmentsController : AcademicApiControllerBase
{
    private readonly IClassEnrollmentService _service;

    public EnrollmentsController(IClassEnrollmentService service) => _service = service;

    [HttpGet]
    [SupabaseAuthorize]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? studentId = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, expand, classId, studentId);
            return OkPaged(items, page, pageSize, total, "Enrollments retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("{classId:guid}/{studentId:guid}")]
    [SupabaseAuthorize]
    public async Task<IActionResult> GetByKey(
        Guid classId,
        Guid studentId,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null)
    {
        try
        {
            var item = await _service.GetByKeyAsync(classId, studentId, expand);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Enrollment not found."));
            return OkSingle(item, "Enrollment retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Create([FromBody] CreateClassEnrollmentDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Enrollment created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{classId:guid}/{studentId:guid}")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Update(Guid classId, Guid studentId, [FromBody] UpdateClassEnrollmentDto dto)
    {
        try
        {
            var success = await _service.UpdateAsync(classId, studentId, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Enrollment not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Enrollment updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("{classId:guid}/{studentId:guid}")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Delete(Guid classId, Guid studentId)
    {
        try
        {
            var success = await _service.DeleteAsync(classId, studentId);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Enrollment not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Enrollment deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
