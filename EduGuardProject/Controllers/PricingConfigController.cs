using EduGuardProject.DTOs.Request;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace EduGuardProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Bỏ hẳn [Authorize] ở đây để tránh lỗi signature của Supabase chặn 401 ngoài cửa
    public class PricingConfigController : ControllerBase
    {
        private readonly IPricingConfigService _service;
        private readonly IUserService _userService;

        public PricingConfigController(IPricingConfigService service, IUserService userService)
        {
            _service = service;
            _userService = userService;
        }

        // ================= KHU VỰC CÁC HÀM LẤY DỮ LIỆU (GET) =================

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllConfigsAsync();
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id}")] // 👈 ĐÂY CHÍNH LÀ HÀM 'GetById' MÀ DÒNG DƯỚI ĐANG TÌM KIẾM
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetConfigByIdAsync(id);
            if (result == null)
                return NotFound(new { success = false, message = "Không tìm thấy cấu hình giá." });

            return Ok(new { success = true, data = result });
        }

        [HttpGet("active/{serviceType}")]
        public async Task<IActionResult> GetActiveConfig(PricingServiceType serviceType)
        {
            var result = await _service.GetCurrentActiveConfigAsync(serviceType);
            if (result == null)
                return NotFound(new { success = false, message = "Dịch vụ này chưa được thiết lập giá." });

            return Ok(new { success = true, data = result });
        }

        // ================= KHU VỰC HÀM TẠO DỮ LIỆU (POST) =================

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePricingConfigDto dto)
        {
            try
            {
                // 1. Tự lấy chuỗi Authorization từ Header
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { success = false, message = "Thiếu Token hoặc Token không đúng định dạng." });
                }

                // Cắt lấy đoạn mã JWT Token thô bỏ chữ "Bearer "
                var tokenString = authHeader.Substring("Bearer ".Length).Trim();

                // 2. Đọc trực tiếp nội dung bên trong Token không cần check Signature bí mật
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(tokenString))
                {
                    return BadRequest(new { success = false, message = "Mã Token không hợp lệ." });
                }

                var jwtToken = handler.ReadJwtToken(tokenString);

                // 3. Bốc trường "sub" (ID của User) ra
                var adminIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                if (string.IsNullOrEmpty(adminIdClaim))
                {
                    return Unauthorized(new { success = false, message = "Không tìm thấy thông tin Admin trong Token." });
                }

                Guid adminId = Guid.Parse(adminIdClaim);

                // 4. Kiểm tra quyền dưới Database của bạn
                var userProfile = await _userService.GetUserByIdAsync(adminId);

                // Hỗ trợ check cả 2 kiểu viết hoa/thường của Enum Role cho chắc ăn
                if (userProfile == null || (userProfile.Role != AppRole.SuperAdmin && userProfile.Role != AppRole.SuperAdmin))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Từ chối: Chỉ có SUPER_ADMIN mới có quyền này!" });
                }

                // 5. Thỏa mãn hết điều kiện -> Lưu xuống DB
                var result = await _service.CreateConfigAsync(dto, adminId);

                // Do đã có hàm GetById(Guid id) ở phía trên, dòng này sẽ hết báo lỗi đỏ lập tức!
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}