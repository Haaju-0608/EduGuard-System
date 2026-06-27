using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EduGuardProject.Services.BackgroundJobs;

public class ExamReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExamReminderBackgroundService> _logger;

    public ExamReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<ExamReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "Exam reminder job failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var now = DateTime.UtcNow;
        var reminderWindowEnd = now.AddMinutes(15);
        var exams = await context.ExamSlots.AsNoTracking()
            .Where(e => e.Status == ExamSlotStatus.Scheduled && e.StartTime >= now && e.StartTime <= reminderWindowEnd)
            .ToListAsync(cancellationToken);

        foreach (var exam in exams)
        {
            var cacheKey = $"exam-reminder:{exam.Id}";
            if (_cache.TryGetValue(cacheKey, out _))
                continue;

            await notifications.SendToClassStudentsAsync(
                exam.ClassId,
                "Nhắc lịch thi",
                $"Nhắc nhở kỳ thi {exam.ExamName} sắp bắt đầu lúc {exam.StartTime:yyyy-MM-dd HH:mm}.",
                NotificationType.ExamReminder,
                ReferenceTypeEnum.ExamSlot,
                exam.Id,
                cancellationToken);

            _cache.Set(cacheKey, true, TimeSpan.FromHours(3));
        }
    }

    private void TryLogWarning(Exception ex, string message)
    {
        try
        {
            _logger.LogWarning(ex, message);
        }
        catch
        {
            // Logging providers can be unavailable in restricted Windows sandboxes.
        }
    }
}
