using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IRealtimeEventDispatcher _realtime;

        public NotificationService(AppDbContext context, IRealtimeEventDispatcher realtime)
        {
            _context = context;
            _realtime = realtime;
        }

        // ================= 1. BẮN THÔNG BÁO (INTERNAL) =================
        public async Task<bool> SendNotificationAsync(CreateNotificationDto dto)
        {
            if (dto.UserId == Guid.Empty)
                throw new InvalidOperationException("Notification recipient is required.");

            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body))
                throw new InvalidOperationException("Notification title and body are required.");

            var recipientExists = await _context.Users.AnyAsync(u =>
                u.Id == dto.UserId &&
                u.DeletedAt == null &&
                u.Status == UserStatus.Active);

            if (!recipientExists)
                throw new InvalidOperationException("Notification recipient not found.");

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = dto.UserId,
                Title = dto.Title.Trim(),
                Body = dto.Body.Trim(),
                Type = dto.Type,
                SentVia = dto.SentVia,
                ReferenceId = dto.ReferenceId,
                ReferenceType = dto.ReferenceType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var payload = new
            {
                notification.Id,
                notification.UserId,
                notification.Title,
                notification.Body,
                notification.Type,
                notification.SentVia,
                notification.ReferenceId,
                notification.ReferenceType,
                notification.IsRead,
                notification.CreatedAt
            };

            await _realtime.PushUserAsync(notification.UserId, HubEvents.NotificationCreated, payload);
            await _realtime.PublishDataChangedAsync(
                "notifications",
                "created",
                userId: notification.UserId,
                data: payload);
            return true;
        }

        // ================= 2. LẤY THÔNG BÁO CÓ PHÂN TRANG =================
        public async Task<(IEnumerable<NotificationResponseDto> Data, int TotalItems, int UnreadCount)> GetUserNotificationsAsync(Guid userId, int page, int pageSize)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);

            int totalItems = await query.CountAsync();
            int unreadCount = await query.CountAsync(n => !n.IsRead);

            var data = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationResponseDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Title = n.Title,
                    Body = n.Body, // Sửa thành Body
                    IsRead = n.IsRead,
                    Type = n.Type.ToString(),
                    SentVia = n.SentVia.ToString(),
                    ReferenceId = n.ReferenceId,
                    ReferenceType = n.ReferenceType != null ? n.ReferenceType.ToString() : null,
                    CreatedAt = n.CreatedAt
                }).ToListAsync();

            return (data, totalItems, unreadCount);
        }

        // (Các hàm MarkAsReadAsync và MarkAllAsReadAsync giữ nguyên như cũ nhé)
        public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null || notification.IsRead) return false;

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();

            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            var payload = new
            {
                notificationId,
                userId,
                unreadCount,
                readAt = notification.UpdatedAt
            };

            await _realtime.PushUserAsync(userId, HubEvents.NotificationRead, payload);
            await _realtime.PublishDataChangedAsync(
                "notifications",
                "read",
                userId: userId,
                data: payload);
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(Guid userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!unreadNotifications.Any()) return false;

            foreach (var n in unreadNotifications)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTime.UtcNow;
            }

            _context.Notifications.UpdateRange(unreadNotifications);
            await _context.SaveChangesAsync();

            var payload = new
            {
                userId,
                markedCount = unreadNotifications.Count,
                unreadCount = 0,
                readAt = DateTime.UtcNow
            };

            await _realtime.PushUserAsync(userId, HubEvents.NotificationsRead, payload);
            await _realtime.PublishDataChangedAsync(
                "notifications",
                "read-all",
                userId: userId,
                data: payload);
            return true;
        }
    }
}
