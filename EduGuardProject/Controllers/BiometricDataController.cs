using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/biometric-data")]
[ApiController]
public class BiometricDataController : AcademicApiControllerBase
{
    private readonly IBiometricDatumService _service;

    public BiometricDataController(IBiometricDatumService service) => _service = service;

    [HttpGet]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null,
        [FromQuery] Guid? userId = null)
    {
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");
        try
        {
            var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, expand, userId);
            return OkPaged(items, page, pageSize, total, "Biometric data retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("{id:guid}")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Student)]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null, [FromQuery] string? expand = null)
    {
        try
        {
            var item = await _service.GetByIdAsync(id, expand);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("Biometric data not found."));
            return OkSingle(item, "Biometric data retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost]
    [SupabaseAuthorize]
    public async Task<IActionResult> Create([FromBody] CreateBiometricDatumDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Biometric data created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{id:guid}")]
    [SupabaseAuthorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBiometricDatumDto dto)
    {
        try
        {
            var success = await _service.UpdateAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Biometric data not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Biometric data updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("{id:guid}")]
    [SupabaseAuthorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Biometric data not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Biometric data deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
