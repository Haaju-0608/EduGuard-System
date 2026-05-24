using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        public UsersController(IUserService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var (items, totalCount) = await _service.GetUsersAsync(search, sort, page, pageSize);
                // Metadata phân trang chuẩn [cite: 66, 67, 72]
                return Ok(ApiPagedResponse<UserResponseDto>.OnPagedSuccess(items, page, pageSize, totalCount, "Get the list of successful users!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _service.GetUserByIdAsync(id);
            // Trả về 404 Not Found chuẩn form [cite: 50, 88, 89]
            if (item == null) return NotFound(ApiResponse<object>.OnFail("No user found."));

            return Ok(ApiResponse<UserResponseDto>.OnSuccess(item, "Details obtained successfully!"));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                var result = await _service.CreateUserAsync(dto);
                // HTTP 201 Created [cite: 84, 85]
                return StatusCode(201, ApiResponse<UserResponseDto>.OnSuccess(result, "Successfully created a user!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"Unable to create a user: {ex.Message}"));
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
        {
            var success = await _service.UpdateUserAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("No user found to update."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "Information updated successfully!"));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteUserAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("Không tìm thấy người dùng để xóa."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "User deletion successful!"));
        }
    }
}
