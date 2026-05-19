using EduGuardProject.DTOs.Request;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/[controller]")]
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
                    return Unauthorized(new { success = false, message = "Sai email hoặc mật khẩu." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Đăng nhập thành công!",
                    data = loginResult
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Đăng nhập thất bại: " + ex.Message });
            }
        }
    }
}