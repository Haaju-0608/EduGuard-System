using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
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

        [HttpGet("user/{userId}")]
        [SupabaseAuthorize] // Ghi trơn thế này nghĩa là: Ai đăng nhập cũng gọi được
        public async Task<IActionResult> GetUserNotifications(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize < 1)
                    return BadRequest(ApiResponse<object>.OnFail("Page and PageSize must be greater than 0."));

                var result = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);
                int totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

                // 🛠️ ĐÃ FIX: Build object bằng tay để nhét được 'unreadCount' ra ngoài giống như cũ
                return Ok(new
                {
                    success = true,
                    message = "Notifications retrieved successfully.",
                    data = new
                    {
                        items = result.Data,
                        unreadCount = result.UnreadCount
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
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPut("{id}/read")]
        [SupabaseAuthorize]
        public async Task<IActionResult> MarkAsRead(Guid id, [FromQuery] Guid userId)
        {
            try
            {
                var success = await _notificationService.MarkAsReadAsync(id, userId);
                if (!success)
                    return BadRequest(ApiResponse<object>.OnFail("Notification not found or already read."));

                return Ok(ApiResponse<object>.OnSuccess(null, "Notification marked as read."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }

        [HttpPut("read-all")]
        [SupabaseAuthorize]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] Guid userId)
        {
            try
            {
                await _notificationService.MarkAllAsReadAsync(userId);
                return Ok(ApiResponse<object>.OnSuccess(null, "All notifications marked as read."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.OnFail($"System error: {ex.Message}"));
            }
        }
    }
}