using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/browser-violations")]
[ApiController]
[SupabaseAuthorize]
public class BrowserViolationController : AcademicApiControllerBase
{
    private readonly IBrowserViolationService _service;

    public BrowserViolationController(IBrowserViolationService service) => _service = service;

    // Get browser violations for one participation. The response uses the same paged
    // violation-log shape as the camera/AI review endpoint.
    [HttpGet]
    [SupabaseAuthorize(AppRole.Student, AppRole.Lecturer, AppRole.SchoolAdmin, AppRole.SuperAdmin)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid participationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? fields = null)
    {
        if (participationId == Guid.Empty)
            return BadRequest(ApiResponse<object>.OnFail("Participation id is required."));
        if (!ValidatePaging(page, pageSize)) return BadPagedRequest("Page and pageSize must be greater than 0.");

        try
        {
            var (items, total) = await _service.GetAllAsync(participationId, page, pageSize);
            return OkPaged(items, page, pageSize, total, "Browser violations retrieved successfully.", fields);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Report Browser Violation
    // Truyền dữ liệu: body participationId, violationType (TabSwitch, WindowBlur, ExitFullscreen).
    // Điều kiện: role Student; Student phải là chủ participation; participation phải đang ở trạng thái Joined.
    [HttpPost]
    [SupabaseAuthorize(AppRole.Student)]
    public async Task<IActionResult> Create([FromBody] BrowserViolationRequestDto dto)
    {
        try
        {
            var result = await _service.RecordAsync(dto);
            return CreatedSingle(result, "Browser violation recorded successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
