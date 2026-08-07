using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters; 
using EduGuardProject.Models; 
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace EduGuardProject.Controllers
{
    [Route("api/institutions")]
    [ApiController]
    public class InstitutionsController : ControllerBase
    {
        private readonly IInstitutionService _service;
        public InstitutionsController(IInstitutionService service) => _service = service;

        // 1. API LẤY DANH SÁCH TẤT CẢ TRƯỜNG HỌC
        [HttpGet]
        [SupabaseAuthorize(AppRole.SuperAdmin)] // CHỈ Chủ hệ thống SaaS mới được xem toàn bộ danh sách khách hàng (trường học)
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize is < 1 or > 100)
                return BadRequest(ApiResponse<object>.OnFail("Page must be greater than 0 and pageSize must be between 1 and 100."));

            try
            {
                var (items, totalCount) = await _service.GetInstitutionsAsync(search, sort, page, pageSize);
                var response = ApiPagedResponse<InstitutionResponseDto>.OnPagedSuccess(items, page, pageSize, totalCount, "Institutions retrieved successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        // 2. API LẤY CHI TIẾT TRƯỜNG HỌC THEO ID
        [HttpGet("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] // Chủ hệ thống và Admin của trường đó được xem chi tiết
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _service.GetInstitutionByIdAsync(id);
            if (item == null)
            {
                return NotFound(ApiResponse<object>.OnFail("Institution not found."));
            }
            return Ok(ApiResponse<InstitutionResponseDto>.OnSuccess(item, "Institution details retrieved successfully."));
        }

        // 3. API TẠO MỚI TRƯỜNG HỌC
        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin)] // CHỈ Chủ hệ thống (SuperAdmin) mới được tạo/đăng ký trường học mới vào hệ thống
        public async Task<IActionResult> Create([FromBody] CreateInstitutionDto dto)
        {
            try
            {
                var result = await _service.CreateInstitutionAsync(dto);
                return StatusCode(201, ApiResponse<InstitutionResponseDto>.OnSuccess(result, "Institution created successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"Failed to create institution: {ex.Message}"));
            }
        }

        // 4. API CẬP NHẬT TRƯỜNG HỌC
        [HttpPut("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] // Admin trường học có thể tự sửa thông tin trường của mình
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInstitutionDto dto)
        {
            var success = await _service.UpdateInstitutionAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Institution not found to update."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "Institution updated successfully."));
        }

        // 5. API XÓA TRƯỜNG HỌC
        [HttpDelete("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin)] // CHỈ SuperAdmin mới có quyền xóa dữ liệu của cả một trường học
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteInstitutionAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Institution not found to delete."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "Institution deleted successfully."));
        }

        [HttpPost("{id:guid}/renew-subscription")]
        [SupabaseAuthorize(AppRole.SchoolAdmin)]
        public async Task<IActionResult> RenewSubscription(Guid id, [FromBody] RenewSubscriptionDto dto)
        {
            try
            {
                var success = await _service.RenewSubscriptionAsync(id, dto.BillingModel);
                if (!success) return NotFound(ApiResponse<object>.OnFail("Institution not found."));
                return Ok(ApiResponse<object>.OnSuccess(null!, "Gia hạn subscription thành công."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.OnFail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.OnFail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }
    }
}
