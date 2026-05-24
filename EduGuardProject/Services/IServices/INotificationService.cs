using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;

namespace EduGuardProject.Services.IServices
{
    public interface INotificationService
    {
        // 1. Dùng nội bộ: Bắn thông báo
        Task<bool> SendNotificationAsync(CreateNotificationDto dto);

        // 2. Lấy danh sách thông báo của User (Có phân trang)
        Task<(IEnumerable<NotificationResponseDto> Data, int TotalItems, int UnreadCount)> GetUserNotificationsAsync(Guid userId, int page, int pageSize);

        // 3. Đánh dấu 1 thông báo đã đọc
        Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);

        // 4. Đánh dấu TẤT CẢ thông báo đã đọc
        Task<bool> MarkAllAsReadAsync(Guid userId);
    }
}
