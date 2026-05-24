using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Lấy danh sách thông báo của User
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserNotifications(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                    return BadRequest(new { success = false, message = "Page và PageSize phải lớn hơn 0." });

                var result = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);

                int totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách thông báo thành công.",
                    data = new
                    {
                        items = result.Data,
                        unreadCount = result.UnreadCount // Rất tiện cho Frontend hiển thị số đỏ chấm chuông
                    },
                    pagination = new
                    {
                        page = page,
                        pageSize = pageSize,
                        totalItems = result.TotalItems,
                        totalPages = totalPages
                    },
                    errors = (object)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, errors = ex.Message });
            }
        }

        // Đánh dấu 1 thông báo là đã đọc
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id, [FromQuery] Guid userId)
        {
            try
            {
                var success = await _notificationService.MarkAsReadAsync(id, userId);
                if (!success)
                    return BadRequest(new { success = false, message = "Thông báo không tồn tại hoặc đã được đọc." });

                return Ok(new { success = true, message = "Đã đánh dấu đọc thông báo.", data = (object)null, errors = (object)null });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, errors = ex.Message });
            }
        }

        // Đánh dấu tất cả là đã đọc
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] Guid userId)
        {
            try
            {
                await _notificationService.MarkAllAsReadAsync(userId);
                return Ok(new { success = true, message = "Đã đánh dấu đọc tất cả thông báo.", data = (object)null, errors = (object)null });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, errors = ex.Message });
            }
        }
    }
}
