using EduGuardProject.DTOs.Request;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IAuthService _authService;

        public ContactController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("request-demo")]
        public async Task<IActionResult> RequestDemo([FromBody] ContactRequestDto request)
        {
            try
            {
                var isSaved = await _authService.SaveContactRequestAsync(request);

                if (!isSaved)
                {
                    return BadRequest(new { success = false, message = "Không thể lưu thông tin yêu cầu vào hệ thống." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Gửi yêu cầu thành công! Đội ngũ EduGuard sẽ sớm liên hệ với bạn."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
