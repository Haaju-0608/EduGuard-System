using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Authorize]
[Route("api/biometric-requests")]
[ApiController]
public class BiometricRequestsController : AcademicApiControllerBase
{
    private readonly IBiometricRequestService _service;

    public BiometricRequestsController(IBiometricRequestService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null,
        [FromQuery] Guid? studentId = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, expand, studentId);
            return OkPaged(items, page, pageSize, total, "Biometric requests retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null, [FromQuery] string? expand = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id, expand);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Biometric request not found."));
            return OkSingle(item, "Biometric request retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBiometricRequestDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Biometric request submitted successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewBiometricRequestDto? dto)
    {
        try
        {
            var success = await _service.ApproveAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Biometric request not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Biometric request approved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewBiometricRequestDto? dto)
    {
        try
        {
            var success = await _service.RejectAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Biometric request not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Biometric request rejected successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Biometric request not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Biometric request deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
