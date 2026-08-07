using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Filters;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : AcademicApiControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUser;

        public NotificationsController(INotificationService notificationService, ICurrentUserService currentUser)
        {
            _notificationService = notificationService;
            _currentUser = currentUser;
        }

        [HttpPost]
        [SupabaseAuthorize(AppRole.SuperAdmin)]
        public async Task<IActionResult> Create([FromBody] CreateNotificationDto dto)
        {
            try
            {
                var success = await _notificationService.SendNotificationAsync(dto);
                if (!success)
                    return BadRequest(ApiResponse<object>.OnFail("Unable to create notification."));

                return StatusCode(201, ApiResponse<object>.OnSuccess(null!, "Notification created successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpGet("me")]
        [SupabaseAuthorize]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize is < 1 or > 100)
                    return BadRequest(ApiResponse<object>.OnFail("Page must be greater than 0 and pageSize must be between 1 and 100."));

                var user = await _currentUser.GetRequiredUserAsync();
                var result = await _notificationService.GetUserNotificationsAsync(user.Id, page, pageSize);
                var totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

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
                        page,
                        pageSize,
                        totalItems = result.TotalItems,
                        totalPages
                    },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpGet("me/unread-count")]
        [SupabaseAuthorize]
        public async Task<IActionResult> GetMyUnreadCount()
        {
            try
            {
                var user = await _currentUser.GetRequiredUserAsync();
                var result = await _notificationService.GetUserNotificationsAsync(user.Id, 1, 1);
                return Ok(ApiResponse<object>.OnSuccess(new { unreadCount = result.UnreadCount }, "Unread count retrieved successfully."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpGet("user/{userId:guid}")]
        [SupabaseAuthorize]
        public async Task<IActionResult> GetUserNotifications(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1 || pageSize is < 1 or > 100)
                    return BadRequest(ApiResponse<object>.OnFail("Page must be greater than 0 and pageSize must be between 1 and 100."));

                var effectiveUserId = await ResolveNotificationUserIdAsync(userId);
                var result = await _notificationService.GetUserNotificationsAsync(effectiveUserId, page, pageSize);
                var totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

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
                        page,
                        pageSize,
                        totalItems = result.TotalItems,
                        totalPages
                    },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpPut("{id:guid}/read")]
        [SupabaseAuthorize]
        public async Task<IActionResult> MarkAsRead(Guid id, [FromQuery] Guid? userId)
        {
            try
            {
                var effectiveUserId = await ResolveNotificationUserIdAsync(userId);
                var success = await _notificationService.MarkAsReadAsync(id, effectiveUserId);
                if (!success)
                    return BadRequest(ApiResponse<object>.OnFail("Notification not found or already read."));

                return Ok(ApiResponse<object>.OnSuccess(null!, "Notification marked as read."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpPut("read-all")]
        [SupabaseAuthorize]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] Guid? userId)
        {
            try
            {
                var effectiveUserId = await ResolveNotificationUserIdAsync(userId);
                await _notificationService.MarkAllAsReadAsync(effectiveUserId);
                return Ok(ApiResponse<object>.OnSuccess(null!, "All notifications marked as read."));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private async Task<Guid> ResolveNotificationUserIdAsync(Guid? requestedUserId)
        {
            var currentUser = await _currentUser.GetRequiredUserAsync();
            if (!requestedUserId.HasValue || requestedUserId.Value == currentUser.Id)
                return currentUser.Id;

            if (currentUser.Role != AppRole.SuperAdmin)
                throw new UnauthorizedAccessException("You can only access your own notifications.");

            return requestedUserId.Value;
        }
    }
}
