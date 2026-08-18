using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [SupabaseAuthorize(AppRole.SuperAdmin)]
    [Route("api/contact-requests")]
    [ApiController]
    public class ContactRequestsController : ControllerBase
    {
        private readonly IContactRequestService _service;

        public ContactRequestsController(IContactRequestService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                    return BadRequest(ApiResponse<object>.OnFail("Page and pageSize must be greater than 0."));

                var (items, total) = await _service.GetAllAsync(search, sort, page, pageSize, status);
                return Ok(ApiPagedResponse<ContactRequestResponseDto>.OnPagedSuccess(
                    items, page, pageSize, total, "Contact requests retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var item = await _service.GetByIdAsync(id);
                if (item == null)
                    return NotFound(ApiResponse<object>.OnFail("Contact request not found."));
                return Ok(ApiResponse<ContactRequestResponseDto>.OnSuccess(item, "Contact request retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateContactRequestStatusDto dto)
        {
            try
            {
                var success = await _service.UpdateStatusAsync(id, dto);
                if (!success)
                    return NotFound(ApiResponse<object>.OnFail("Contact request not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Status updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }
    }
}