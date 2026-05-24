using EduGuardProject.DTOs; 
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/auth")] // Định nghĩa rõ ràng theo số nhiều / quy tắc RESTful trong file 
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var loginResult = await _authService.LoginAsync(request);

                if (loginResult == null)
                {
                    // Trả về Form chuẩn khi Thất bại (Mã 401 Unauthorized)
                    var failResponse = ApiResponse<object>.OnFail("Wrong email or pass.");
                    return Unauthorized(failResponse);
                }

                // Trả về Form chuẩn khi Thành công (Mã 200 OK)
                var successResponse = ApiResponse<LoginResponseDto>.OnSuccess(loginResult, "Login Success!");
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                // Trả về Form chuẩn khi gặp lỗi Hệ thống / Bad Request (Mã 400 hoặc 500)
                var errorResponse = ApiResponse<object>.OnFail($"Fail to login: {ex.Message}");
                return BadRequest(errorResponse);
            }
        }
    }
}