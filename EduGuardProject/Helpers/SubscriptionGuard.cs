using EduGuardProject.Models;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Helpers;

public static class SubscriptionGuard
{
    public static async Task EnsureInstitutionActiveAsync(AppDbContext context, Guid? institutionId)
    {
        if (institutionId is null) return;

        var status = await context.Institutions
            .Where(i => i.Id == institutionId.Value)
            .Select(i => (InstitutionStatus?)i.Status)
            .FirstOrDefaultAsync();

        if (status is not InstitutionStatus.Active)
            throw new UnauthorizedAccessException(
                "Your institution's subscription has expired or been suspended. Please contact your School Admin to renew.");
    }
}