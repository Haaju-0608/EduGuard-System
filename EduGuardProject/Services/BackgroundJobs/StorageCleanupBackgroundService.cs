using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services.BackgroundJobs;

public class StorageCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StorageCleanupBackgroundService> _logger;

    public StorageCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<StorageCleanupBackgroundService> logger)
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
                using var scope = _scopeFactory.CreateScope();
                var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                await storage.CleanupLocalTempAsync(TimeSpan.FromDays(1), stoppingToken);
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "Storage cleanup job failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
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
