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
