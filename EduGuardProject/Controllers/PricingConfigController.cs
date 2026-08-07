using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace EduGuardProject.Controllers
{
    [Route("api/pricing-configs")]
    [ApiController]
    public class PricingConfigController : ControllerBase
    {
        private readonly IPricingConfigService _service;

        public PricingConfigController(IPricingConfigService service)
        {
            _service = service;
        }

        [HttpGet]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllConfigsAsync();
            return Ok(ApiResponse<object>.OnSuccess(result, "Get all pricing configs successfully."));
        }

        [HttpGet("{id}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetConfigByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<object>.OnFail("Pricing configuration not found."));

            return Ok(ApiResponse<object>.OnSuccess(result, "Pricing config details retrieved successfully."));
        }

        [HttpGet("active/{serviceType}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)]
        public async Task<IActionResult> GetActiveConfig(PricingServiceType serviceType)
        {
            var result = await _service.GetCurrentActiveConfigAsync(serviceType);
            if (result == null)
                return NotFound(ApiResponse<object>.OnFail("No active pricing configured for this service."));

            return Ok(ApiResponse<object>.OnSuccess(result, "Active pricing config retrieved successfully."));
        }

        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin)]
        public async Task<IActionResult> Create([FromBody] CreatePricingConfigDto dto)
        {
            try
            {
                if (HttpContext.Items["UserId"] is not Guid adminId)
                    return Unauthorized(ApiResponse<object>.OnFail("User is not authenticated."));

                var result = await _service.CreateConfigAsync(dto, adminId);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.OnSuccess(result, "Pricing configuration created successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
        }

        [HttpPut("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePricingConfigDto dto)
        {
            try
            {
                var success = await _service.UpdateConfigAsync(id, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Pricing config not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Pricing config updated successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }
    }
}
