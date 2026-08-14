using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/reading-passages")]
[ApiController]
[SupabaseAuthorize]
public class ReadingPassagesController : AcademicApiControllerBase
{
    private readonly IReadingPassageService _service;

    public ReadingPassagesController(IReadingPassageService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound(ApiResponse<object>.OnFail("Reading passage not found."));
            return OkSingle(result, "Reading passage retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateReadingPassageDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedSingle(result, "Reading passage created successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReadingPassageDto dto, [FromQuery] string? fields = null)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null) return NotFound(ApiResponse<object>.OnFail("Reading passage not found."));
            return OkSingle(result, "Reading passage updated successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("{id:guid}")]
    [SupabaseAuthorize(AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound(ApiResponse<object>.OnFail("Reading passage not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Reading passage deleted successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
