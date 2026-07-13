using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/violation-logs")]
[ApiController]
[SupabaseAuthorize]
public class ViolationLogController : AcademicApiControllerBase
{
    private readonly IViolationLogService _service;

    public ViolationLogController(IViolationLogService service) => _service = service;

    // Get All Violation Logs
    // Truyền dữ liệu: query search, sort, page, pageSize, fields, participationId, isReviewed.
    // Điều kiện: role SuperAdmin; page và pageSize phải lớn hơn 0.
    [HttpGet]
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] Guid? participationId = null,
        [FromQuery] bool? isReviewed = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(
                search, sort, page, pageSize, participationId, isReviewed);
            var response = ApiPagedResponse<ViolationlogResponeDto>.OnPagedSuccess(items, page, pageSize, total, "Violation retrieved successfully.");
            return Ok(response);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Get Violation Log By Id
    // Truyền dữ liệu: route id, query fields.
    // Điều kiện: user đã đăng nhập; SuperAdmin xem tất cả, SchoolAdmin cùng institution, Lecturer đúng class của exam.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Violation log not found."));
            return OkSingle(item, "Violation log retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Create Violation Log
    // Truyền dữ liệu: body participationId, severity, violationType, evidencePath, aiConfidence, reviewedBy, recordedAt; query fields.
    // Điều kiện: Lecturer đúng class, SchoolAdmin cùng institution hoặc SuperAdmin; exam participation phải tồn tại.
    [HttpPost]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateViolationLogDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Violation log created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Update Violation Log
    // Truyền dữ liệu: route id, body isReviewed, reviewedBy.
    // Điều kiện: Lecturer đúng class, SchoolAdmin cùng institution hoặc SuperAdmin; violation log phải tồn tại.
    [HttpPut("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateViolationLogDto dto)
    {
        try
        {
            var success = await _service.UpdateAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Violation log not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Violation log updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Delete Violation Log
    // Truyền dữ liệu: route id.
    // Điều kiện: Lecturer đúng class, SchoolAdmin cùng institution hoặc SuperAdmin; violation log phải tồn tại.
    [HttpDelete("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Violation log not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Violation log deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
