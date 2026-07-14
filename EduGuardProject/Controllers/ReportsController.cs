using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/reports")]
[ApiController]
[SupabaseAuthorize]
public class ReportsController : AcademicApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("attendance")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Attendance(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? classId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetAttendanceReportAsync(institutionId, classId, from, to);
            return Ok(ApiResponse<object>.OnSuccess(result, "Attendance report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("violations")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Violations(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? examSlotId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetViolationReportAsync(institutionId, examSlotId, from, to);
            return Ok(ApiResponse<object>.OnSuccess(result, "Violation report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("wallet")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> Wallet(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? walletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetWalletReportAsync(institutionId, walletId, from, to);
            return Ok(ApiResponse<object>.OnSuccess(result, "Wallet report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("revenue")]
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = "day")
    {
        try
        {
            var result = await _reports.GetRevenueReportAsync(from, to, groupBy);
            return Ok(ApiResponse<object>.OnSuccess(result, "Revenue report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
