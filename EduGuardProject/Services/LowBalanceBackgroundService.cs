using EduGuardProject.Models;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services.BackgroundJobs;

public class SubscriptionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionExpiryBackgroundService> _logger;

    public SubscriptionExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SubscriptionExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;
                var expiredInstitutions = await context.Institutions
                    .Where(i => i.Status == InstitutionStatus.Active
                        && i.SubscriptionExpiresAt.HasValue
                        && i.SubscriptionExpiresAt.Value < now)
                    .ToListAsync(stoppingToken);

                foreach (var institution in expiredInstitutions)
                {
                    institution.Status = InstitutionStatus.Suspended;
                    institution.UpdatedAt = now;
                    _logger.LogInformation(
                        "Institution {Id} ({Name}) bị tự động khoá do hết hạn subscription.",
                        institution.Id, institution.Name);
                }

                if (expiredInstitutions.Count > 0)
                    await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi quét subscription hết hạn.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}