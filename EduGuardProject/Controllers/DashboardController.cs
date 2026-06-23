using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/dashboard")]
[ApiController]
[SupabaseAuthorize]
public class DashboardController : AcademicApiControllerBase
{
    private readonly IDashboardStatsService _dashboard;

    public DashboardController(IDashboardStatsService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("system")]
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    public async Task<IActionResult> System([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _dashboard.GetSystemDashboardAsync(from, to);
            return Ok(ApiResponse<object>.OnSuccess(result, "System dashboard retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("institution")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> Institution([FromQuery] Guid? institutionId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _dashboard.GetInstitutionDashboardAsync(
                institutionId ?? await ResolveCurrentInstitutionIdAsync(),
                from,
                to);
            return Ok(ApiResponse<object>.OnSuccess(result, "Institution dashboard retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("lecturer")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Lecturer([FromQuery] Guid? lecturerId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _dashboard.GetLecturerDashboardAsync(lecturerId, from, to);
            return Ok(ApiResponse<object>.OnSuccess(result, "Lecturer dashboard retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    private async Task<Guid> ResolveCurrentInstitutionIdAsync()
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        var user = await currentUser.GetRequiredUserAsync();
        if (user.InstitutionId == null)
            throw new InvalidOperationException("InstitutionId is required for this user.");

        return user.InstitutionId.Value;
    }
}
