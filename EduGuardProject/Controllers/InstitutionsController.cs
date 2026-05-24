using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/institutions")] // Đặt tên số nhiều chuẩn RESTful (Trang 2)
    [ApiController]
    public class InstitutionsController : ControllerBase
    {
        private readonly IInstitutionService _service;
        public InstitutionsController(IInstitutionService service) => _service = service;

        // 1. API LẤY DANH SÁCH (HỖ TRỢ PHÂN TRANG VÀ FORM JSON CHUẨN ĐỀ BÀI)
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (items, totalCount) = await _service.GetInstitutionsAsync(search, sort, page, pageSize);

                // ÁP DỤNG FORM PHÂN TRANG CHUẨN (Mục 5 & 6)
                var response = ApiPagedResponse<InstitutionResponseDto>.OnPagedSuccess(items, page, pageSize, totalCount, "Get a list of successful schools!");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        // 2. API LẤY CHI TIẾT THEO ID
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _service.GetInstitutionByIdAsync(id);
            if (item == null)
            {
                // Trả về 404 chuẩn form (Mục 4 trang 3)
                return NotFound(ApiResponse<object>.OnFail("No schools meeting the requirements were found."));
            }
            return Ok(ApiResponse<InstitutionResponseDto>.OnSuccess(item, "Details obtained successfully!"));
        }

        // 3. API TẠO MỚI TRƯỜNG HỌC
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInstitutionDto dto)
        {
            try
            {
                var result = await _service.CreateInstitutionAsync(dto);
                // Trả về HTTP 201 Created kèm dữ liệu mẫu (Trang 4)
                return StatusCode(201, ApiResponse<InstitutionResponseDto>.OnSuccess(result, "Create a successful school!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"Cannot create a school: {ex.Message}"));
            }
        }

        // 4. API CẬP NHẬT TRƯỜNG HỌC
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInstitutionDto dto)
        {
            var success = await _service.UpdateInstitutionAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("No schools were found to update."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "Information updated successfully!"));
        }

        // 5. API XÓA TRƯỜNG HỌC
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteInstitutionAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("No school found to delete."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "School demolition was successful!"));
        }
    }
}
