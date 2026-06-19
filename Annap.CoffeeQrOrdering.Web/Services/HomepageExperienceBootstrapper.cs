using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

public static class HomepageExperienceBootstrapper
{
    public static async Task EnsureDefaultsAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var id = HomepageExperienceSettingsConfiguration.SingletonId;
        if (await db.HomepageExperienceSettings.AnyAsync(x => x.Id == id, cancellationToken))
            return;

        db.HomepageExperienceSettings.Add(new HomepageExperienceSettings
        {
            Id = id,
            IsGroupEnabled = true,
            IsSoloEnabled = true,
            IsSommelierEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
