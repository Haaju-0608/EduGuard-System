using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly AppDbContext _context;
    private readonly IRealtimeEventDispatcher _realtime;

    public NotificationDispatcher(
        AppDbContext context,
        IRealtimeEventDispatcher realtime)
    {
        _context = context;
        _realtime = realtime;
    }

    public async Task<NotificationResponseDto> SendToUserAsync(
        Guid userId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = CreateNotification(userId, title, body, type, referenceType, referenceId);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = Map(notification);
        await _realtime.PushUserAsync(userId, HubEvents.NotificationCreated, dto, cancellationToken);
        await _realtime.PublishDataChangedAsync(
            "notifications",
            "created",
            userId: userId,
            data: dto,
            cancellationToken: cancellationToken);

        return dto;
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinctIds.Length == 0) return [];

        var notifications = distinctIds
            .Select(userId => CreateNotification(userId, title, body, type, referenceType, referenceId))
            .ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);

        var dtos = notifications.Select(Map).ToList();
        foreach (var dto in dtos)
        {
            await _realtime.PushUserAsync(dto.UserId, HubEvents.NotificationCreated, dto, cancellationToken);
            await _realtime.PublishDataChangedAsync(
                "notifications",
                "created",
                userId: dto.UserId,
                data: dto,
                cancellationToken: cancellationToken);
        }

        return dtos;
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> SendToClassStudentsAsync(
        Guid classId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var studentIds = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken);

        return await SendToUsersAsync(studentIds, title, body, type, referenceType, referenceId, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> SendToInstitutionAdminsAsync(
        Guid institutionId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var adminIds = await _context.Users
            .AsNoTracking()
            .Where(u =>
                u.InstitutionId == institutionId &&
                u.Role == AppRole.SchoolAdmin &&
                u.Status == UserStatus.Active &&
                u.DeletedAt == null)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return await SendToUsersAsync(adminIds, title, body, type, referenceType, referenceId, cancellationToken);
    }

    public async Task PushContactRequestAsync(ContactRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            contactRequestId = request.Id,
            request.SchoolName,
            request.ContactPersonName,
            request.Email,
            request.PhoneNumber,
            request.Status,
            request.CreatedAt
        };

        await _realtime.PushSuperAdminsAsync(HubEvents.NewSchoolRegistrationRequest, payload, cancellationToken);
    }

    private static Notification CreateNotification(
        Guid userId,
        string title,
        string body,
        NotificationType type,
        ReferenceTypeEnum? referenceType,
        Guid? referenceId)
    {
        var now = DateTime.UtcNow;
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            SentVia = NotificationChannel.Dashboard,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static NotificationResponseDto Map(Notification notification) => new()
    {
        Id = notification.Id,
        UserId = notification.UserId,
        Title = notification.Title,
        Body = notification.Body,
        IsRead = notification.IsRead,
        Type = notification.Type.ToString(),
        SentVia = notification.SentVia.ToString(),
        ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType?.ToString(),
        CreatedAt = notification.CreatedAt
    };
}
