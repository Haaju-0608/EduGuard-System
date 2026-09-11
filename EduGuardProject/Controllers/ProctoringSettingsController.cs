using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

[Route("api/proctoring-settings")]
[ApiController]
[SupabaseAuthorize]
public class ProctoringSettingsController : AcademicApiControllerBase
{
    private readonly IProctoringSettingsService _service;
    private readonly ICurrentUserService _currentUser;

    public ProctoringSettingsController(IProctoringSettingsService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // Get All Proctoring Settings (SuperAdmin: all configs; SchoolAdmin: for auditing/history)
    [HttpGet]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var result = await _service.GetAllAsync();
            return OkSingle(result, "Proctoring settings retrieved successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("{id:guid}")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound(ApiResponse<object>.OnFail("Proctoring settings not found."));
            return OkSingle(result, "Proctoring settings retrieved successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Get Effective Proctoring Settings — what actually applies right now for an institution
    // (its own override, or the system-wide default). Readable by Lecturer/Student too so
    // both FE screens agree on the same thresholds/counts source of truth.
    [HttpGet("effective")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer, AppRole.Student)]
    public async Task<IActionResult> GetEffective([FromQuery] Guid? institutionId)
    {
        try
        {
            var user = await _currentUser.GetRequiredUserAsync();
            // Non-admins can only read the effective config for their own institution.
            var resolvedInstitutionId = user.Role is AppRole.SuperAdmin ? institutionId : user.InstitutionId;
            var result = await _service.GetEffectiveAsync(resolvedInstitutionId);
            return OkSingle(result, "Effective proctoring settings retrieved successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // Create Proctoring Settings — SchoolAdmin: own institution only; SuperAdmin: any institution or the system-wide default (institutionId = null)
    [HttpPost]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateProctoringSettingsDto dto)
    {
        try
        {
            var user = await _currentUser.GetRequiredUserAsync();
            var result = await _service.CreateAsync(dto, user.Id, user.Role, user.InstitutionId);
            return CreatedSingle(result, "Proctoring settings created successfully.");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("{id:guid}")]
    [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProctoringSettingsDto dto)
    {
        try
        {
            var user = await _currentUser.GetRequiredUserAsync();
            var success = await _service.UpdateAsync(id, dto, user.Id, user.Role, user.InstitutionId);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Proctoring settings not found."));
            return Ok(ApiResponse<object>.OnSuccess(null!, "Proctoring settings updated successfully."));
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
