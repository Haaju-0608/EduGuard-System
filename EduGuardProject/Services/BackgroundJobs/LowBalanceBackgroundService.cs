using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services.BackgroundJobs;

public class LowBalanceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowBalanceBackgroundService> _logger;

    public LowBalanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LowBalanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendLowBalanceAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "Low balance job failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task SendLowBalanceAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var today = DateTime.UtcNow.Date;

        var wallets = await context.Wallets
            .Where(w =>
                w.Balance <= w.LowBalanceThreshold &&
                (w.LowBalanceAlertSentAt == null || w.LowBalanceAlertSentAt.Value.Date < today))
            .ToListAsync(cancellationToken);

        foreach (var wallet in wallets)
        {
            await notifications.SendToInstitutionAdminsAsync(
                wallet.InstitutionId,
                "Số dư ví thấp",
                $"Số dư ví hiện còn {wallet.Balance:N0} {wallet.Currency}, dưới hoặc bằng ngưỡng {wallet.LowBalanceThreshold:N0}.",
                NotificationType.LowBalanceAlert,
                ReferenceTypeEnum.Institution,
                wallet.InstitutionId,
                cancellationToken);

            wallet.LowBalanceAlertSentAt = DateTime.UtcNow;
            wallet.UpdatedAt = DateTime.UtcNow;
        }

        if (wallets.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
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
