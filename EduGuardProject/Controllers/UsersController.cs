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
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        public UsersController(IUserService service) => _service = service;

        // ================= BAN QUẢN TRỊ & GIẢNG VIÊN (QUẢN LÝ CHUNG) =================

        [HttpGet]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)] // Mở quyền cho Giảng viên xem danh sách học sinh
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var (items, totalCount) = await _service.GetUsersAsync(search, sort, page, pageSize);
                return Ok(ApiPagedResponse<UserResponseDto>.OnPagedSuccess(items, page, pageSize, totalCount, "Users retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpGet("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin, AppRole.Lecturer)] // Giảng viên được xem thông tin chi tiết của học sinh
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _service.GetUserByIdAsync(id);
            if (item == null) return NotFound(ApiResponse<object>.OnFail("User not found."));

            return Ok(ApiResponse<UserResponseDto>.OnSuccess(item, "User details retrieved successfully."));
        }

        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] // Chỉ Admin mới được quyền tạo User mới
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                var result = await _service.CreateUserAsync(dto);
                return StatusCode(201, ApiResponse<UserResponseDto>.OnSuccess(result, "User created successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"Failed to create user: {ex.Message}"));
            }
        }

        [HttpPut("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] // Chỉ Admin mới được chỉnh sửa thông tin của User khác (đổi role, khóa tài khoản...)
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
        {
            var success = await _service.UpdateUserAsync(id, dto);
            if (!success) return NotFound(ApiResponse<object>.OnFail("User not found to update."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "User updated successfully."));
        }

        [HttpDelete("{id:guid}")]
        [SupabaseAuthorize(AppRole.SuperAdmin, AppRole.SchoolAdmin)] // Quyền tối cao: Chỉ Admin mới được xóa User
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteUserAsync(id);
            if (!success) return NotFound(ApiResponse<object>.OnFail("User not found to delete."));

            return Ok(ApiResponse<object>.OnSuccess(null!, "User deleted successfully."));
        }


        // ================= KHU VỰC CÁ NHÂN (MỌI ROLE ĐỀU ĐƯỢC PHÉP TRUY CẬP) =================

        [HttpGet("me")]
        [SupabaseAuthorize] // Trống ngoặc = Bất kỳ ai đăng nhập hợp lệ (Student, Lecturer, Admin) đều dùng được
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                if (HttpContext.Items["UserId"] is not Guid myUserId)
                    return Unauthorized(ApiResponse<object>.OnFail("User is not authenticated."));

                var item = await _service.GetUserByIdAsync(myUserId);
                if (item == null) return NotFound(ApiResponse<object>.OnFail("User profile not found."));

                return Ok(ApiResponse<UserResponseDto>.OnSuccess(item, "My profile retrieved successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPut("me")]
        [SupabaseAuthorize] // Tự sinh viên/giảng viên tự sửa hồ sơ của CHÍNH HỌ
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileDto dto)
        {
            try
            {
                if (HttpContext.Items["UserId"] is not Guid myUserId)
                    return Unauthorized(ApiResponse<object>.OnFail("User is not authenticated."));

                var success = await _service.UpdateMyProfileAsync(myUserId, dto);
                if (!success) return NotFound(ApiResponse<object>.OnFail("User profile not found to update."));

                return Ok(ApiResponse<object>.OnSuccess(null!, "My profile updated successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.OnFail($"Failed to update profile: {ex.Message}"));
            }
        }
    }
}
