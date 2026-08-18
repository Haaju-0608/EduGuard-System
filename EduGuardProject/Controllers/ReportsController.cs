using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/reports")]
[ApiController]
[SupabaseAuthorize]
public class ReportsController : AcademicApiControllerBase
{
    private readonly IReportService _reports;
    private readonly IReportExportService _exporter;

    public ReportsController(IReportService reports, IReportExportService exporter)
    {
        _reports = reports;
        _exporter = exporter;
    }

    [HttpGet("export")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    public async Task<IActionResult> Export(
        [FromQuery] string reportType,
        [FromQuery] string format,
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? classId,
        [FromQuery] Guid? examSlotId,
        [FromQuery] Guid? walletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = "day",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reportType))
                throw new ArgumentException("reportType is required.");
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("format is required.");
            if (from > to)
                throw new ArgumentException("from must be earlier than or equal to to.");

            var report = reportType.ToLowerInvariant() switch
            {
                "attendance" => await _reports.GetAttendanceReportAsync(institutionId, classId, from, to, cancellationToken),
                "violations" => await _reports.GetViolationReportAsync(institutionId, examSlotId, from, to, cancellationToken),
                "wallet" => await _reports.GetWalletReportAsync(institutionId, walletId, from, to, cancellationToken),
                "revenue" => await _reports.GetRevenueReportAsync(from, to, groupBy, cancellationToken),
                _ => throw new ArgumentException("reportType must be attendance, violations, wallet, or revenue.")
            };

            var file = _exporter.Export(reportType, format, report);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("attendance")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceReportResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Attendance(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? classId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetAttendanceReportAsync(institutionId, classId, from, to);
            return Ok(ApiResponse<AttendanceReportResponseDto>.OnSuccess(result, "Attendance report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("violations")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)]
    [ProducesResponseType(typeof(ApiResponse<ViolationReportResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Violations(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? examSlotId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetViolationReportAsync(institutionId, examSlotId, from, to);
            return Ok(ApiResponse<ViolationReportResponseDto>.OnSuccess(result, "Violation report retrieved successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("wallet")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    [ProducesResponseType(typeof(ApiResponse<WalletReportResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Wallet(
        [FromQuery] Guid? institutionId,
        [FromQuery] Guid? walletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var result = await _reports.GetWalletReportAsync(institutionId, walletId, from, to);
            return Ok(ApiResponse<WalletReportResponseDto>.OnSuccess(result, "Wallet report retrieved successfully."));
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
