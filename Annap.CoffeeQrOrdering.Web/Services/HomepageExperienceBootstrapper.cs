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

    /// <summary>
    /// Development-only: repair CMS flags when ritual experience was disabled in local DB.
    /// Production CMS remains admin-controlled.
    /// </summary>
    public static async Task EnsureDevelopmentRitualFlagsAsync(
        IApplicationDbContext db,
        bool isDevelopment,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
            return;

        var id = HomepageExperienceSettingsConfiguration.SingletonId;
        var home = await db.HomepageExperienceSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (home is null)
            return;

        if (home.IsSoloEnabled && home.IsSommelierEnabled)
            return;

        home.IsSoloEnabled = true;
        home.IsSommelierEnabled = true;
        home.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
