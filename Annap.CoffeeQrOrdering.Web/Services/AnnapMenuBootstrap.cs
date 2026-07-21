using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Real-data menu bootstrap: sync local .webp assets, import AnnapDrinks.csv, purge legacy demo rows.</summary>
public static class AnnapMenuBootstrap
{
    public static async Task EnsureRealMenuAsync(
        IApplicationDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        DrinkAssetResolver assetResolver,
        ILogger log,
        CancellationToken cancellationToken = default)
    {
        var assetsSource = MenuCatalogBootstrapPaths.ResolveAnnapAssetsSourcePath(configuration, environment);
        MenuImageMigrationService.MigrateCatalogSourceToWebp(assetsSource, environment.WebRootPath, log);
        DrinkAssetPublisher.SyncFromSource(assetsSource, environment.WebRootPath, log);
        assetResolver.RefreshIndex(environment.WebRootPath);

        await MenuImageMigrationService.MigrateManagedUploadsAsync(
            environment, db, log, cancellationToken).ConfigureAwait(false);
        await MenuImageMigrationService.MigrateStaticAssetsAsync(environment, log, cancellationToken)
            .ConfigureAwait(false);

        await DbInitializer.TryRemoveLegacyDemoMenuIfUnusedAsync(db, cancellationToken).ConfigureAwait(false);

        var csvPath = MenuCatalogBootstrapPaths.ResolveAnnapDrinksCsvPath(configuration, environment);
        var imported = await AnnapDrinkCsvImporter.TryImportFromCsvAsync(
            db,
            csvPath,
            (cat, name) => assetResolver.ResolveWebUrl(cat, name),
            log,
            cancellationToken,
            IsManagedOrCloudinaryUrl).ConfigureAwait(false);

        if (imported > 0)
        {
            await PurgeUnsupportedRemoteImageUrlsAsync(db, cancellationToken).ConfigureAwait(false);
            log.LogInformation("Real data mode: {Count} drinks loaded from CSV; JSON catalog import skipped.", imported);
        }
        else
        {
            log.LogWarning(
                "AnnapDrinks.csv import produced 0 rows ({Path}). JSON catalog import is disabled in real-data mode.",
                csvPath);
        }

        await BakeryPairingService.EnsureBakeryCategoryAsync(db, cancellationToken).ConfigureAwait(false);
        await AnnapSpecialtyCoffeeBootstrap.EnsureSpecialtyCoffeesAsync(
            db, assetResolver, log, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PurgeUnsupportedRemoteImageUrlsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = false;
        foreach (var item in items)
        {
            if (IsUnsupportedRemoteUrl(item.ImageUrl))
            {
                item.ImageUrl = null;
                changed = true;
            }

            if (IsUnsupportedRemoteUrl(item.DetailPosterImagePath))
            {
                item.DetailPosterImagePath = null;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsManagedOrCloudinaryUrl(string? url) =>
        MenuMediaResolver.IsDurableMediaUrl(url);

    internal static bool IsUnsupportedRemoteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (IsManagedOrCloudinaryUrl(url))
            return false;

        var t = url.Trim();
        return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("//", StringComparison.Ordinal);
    }
}
