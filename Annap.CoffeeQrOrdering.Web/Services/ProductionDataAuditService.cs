using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Read-only production data integrity audit (Phase 8.9B).</summary>
public sealed class ProductionDataAuditService(
    IApplicationDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IMenuInventoryGate inventoryGate)
{
    public async Task<ProductionDataAuditReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var dbTarget = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
        var appUrlPublic = configuration["AppUrl:PublicBaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(appUrlPublic))
            appUrlPublic = null;

        var dbOverride = await db.AppNetworkSettings.AsNoTracking()
            .Where(x => x.Id == AppNetworkSettings.SingletonId)
            .Select(x => x.PublicBaseUrlOverride)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var publicBaseUrlOverride = string.IsNullOrWhiteSpace(dbOverride) ? null : dbOverride.Trim();

        var categories = await BuildCategoryRowsAsync(cancellationToken).ConfigureAwait(false);
        var bakery = await BuildBakeryAuditAsync(cancellationToken).ConfigureAwait(false);
        var specialty = await BuildSpecialtyAuditAsync(cancellationToken).ConfigureAwait(false);
        var media = BuildMediaAudit();
        var pairing = await BuildPairingSampleAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<DataAuditFinding>();
        AppendCategoryFindings(categories, findings);
        AppendBakeryFindings(bakery, findings);
        AppendSpecialtyFindings(specialty.PoolCount, specialty.Rows, findings);
        AppendMediaFindings(media.MissingImagePaths, media.MissingPosterPaths, findings);
        AppendPairingFindings(pairing, findings);

        var overall = findings.Count == 0
            ? DataAuditLevel.Pass
            : findings.Any(f => f.Level == DataAuditLevel.Fail)
                ? DataAuditLevel.Fail
                : DataAuditLevel.Warn;

        if (overall == DataAuditLevel.Pass)
            findings.Add(new DataAuditFinding(DataAuditLevel.Pass, "All audited checks passed."));

        return new ProductionDataAuditReport(
            categories,
            bakery.CategoryExists,
            bakery.CategoryName,
            bakery.ItemCount,
            bakery.ItemNames,
            specialty.PoolCount,
            specialty.Rows,
            media.Rows,
            media.MissingImagePaths,
            media.MissingPosterPaths,
            pairing,
            dbTarget.Host,
            dbTarget.Database,
            appUrlPublic,
            publicBaseUrlOverride,
            InfrastructureEnvironment.IsRenderDeployment,
            overall,
            findings);
    }

    private async Task<IReadOnlyList<DataAuditCategoryRow>> BuildCategoryRowsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Name,
                c.SortOrder,
                TotalItems = c.Items.Count,
                AvailableItems = c.Items.Count(i => i.IsAvailable && !i.IsArchived),
                ArchivedItems = c.Items.Count(i => i.IsArchived)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new DataAuditCategoryRow(
                r.Name,
                r.SortOrder,
                r.TotalItems,
                r.AvailableItems,
                r.ArchivedItems))
            .ToList();
    }

    private async Task<(bool CategoryExists, string? CategoryName, int ItemCount, IReadOnlyList<string> ItemNames)> BuildBakeryAuditAsync(
        CancellationToken cancellationToken)
    {
        var bakeryCategory = await db.MenuCategories.AsNoTracking()
            .Where(c => c.Name == BakeryPairingService.BakeryCategoryName || c.Name == "Banh")
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bakeryCategory is null)
            return (false, null, 0, Array.Empty<string>());

        var items = await db.MenuItems.AsNoTracking()
            .Include(i => i.Category)
            .Where(i =>
                i.Category != null
                && (i.Category.Name == BakeryPairingService.BakeryCategoryName || i.Category.Name == "Banh"))
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (true, bakeryCategory, items.Count, items);
    }

    private async Task<(int PoolCount, IReadOnlyList<DataAuditSpecialtyRow> Rows)> BuildSpecialtyAuditAsync(
        CancellationToken cancellationToken)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);
        var raw = await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
            .Select(m => new
            {
                m.Name,
                m.ItemType,
                m.IngredientBreakdown,
                m.FlavorTags,
                CategoryName = m.Category!.Name,
                m.IsSignature
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        const string familyKey = BeverageFamilyGrounding.Coffee;
        var filtered = raw
            .Where(m => BeverageFamilyGrounding.Matches(
                familyKey,
                m.CategoryName,
                m.Name,
                m.ItemType,
                m.IngredientBreakdown,
                m.FlavorTags))
            .ToList();

        var signatureOnly = filtered.Where(m => m.IsSignature).ToList();
        if (signatureOnly.Count > 0)
            filtered = signatureOnly;

        var specialtyRows = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.CatalogKey != null && AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys.Contains(m.CatalogKey))
            .OrderBy(m => m.CatalogKey)
            .Select(m => new DataAuditSpecialtyRow(
                m.CatalogKey!,
                m.Name,
                m.IsAvailable,
                m.IsArchived,
                m.IsSignature))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (filtered.Count, specialtyRows);
    }

    private (IReadOnlyList<DataAuditMediaRow> Rows, int MissingImagePaths, int MissingPosterPaths) BuildMediaAudit()
    {
        var items = db.MenuItems.AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new { i.Name, i.ImageUrl, i.DetailPosterImagePath })
            .ToList();

        var rows = new List<DataAuditMediaRow>();
        var missingImages = 0;
        var missingPosters = 0;

        foreach (var item in items)
        {
            var imageExists = ResolveLocalFileExists(item.ImageUrl);
            var posterExists = ResolveLocalFileExists(item.DetailPosterImagePath);

            if (imageExists == false)
                missingImages++;
            if (posterExists == false)
                missingPosters++;

            rows.Add(new DataAuditMediaRow(
                item.Name,
                item.ImageUrl,
                imageExists,
                item.DetailPosterImagePath,
                posterExists,
                imageExists == false || posterExists == false));
        }

        return (rows, missingImages, missingPosters);
    }

    private async Task<DataAuditPairingSample?> BuildPairingSampleAsync(CancellationToken cancellationToken)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);

        var bakeryPoolCount = await db.MenuItems.AsNoTracking()
            .Include(i => i.Category)
            .CountAsync(i =>
                i.Category != null
                && (i.Category.Name == BakeryPairingService.BakeryCategoryName || i.Category.Name == "Banh")
                && i.IsAvailable
                && !i.IsArchived
                && !blocked.Contains(i.Id),
                cancellationToken)
            .ConfigureAwait(false);

        var sampleDrink = await db.MenuItems.AsNoTracking()
            .Include(i => i.Category)
            .Where(i =>
                i.IsAvailable
                && !i.IsArchived
                && i.Category != null
                && !BakeryPairingService.IsBakeryCategory(i.Category.Name))
            .OrderBy(i => i.Category!.SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, CategoryName = i.Category!.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sampleDrink is null)
            return null;

        var pairings = await BakeryPairingService.GetSuggestionsForDrinkAsync(
            db,
            inventoryGate,
            sampleDrink.Id,
            sampleDrink.CategoryName,
            cancellationToken).ConfigureAwait(false);

        return new DataAuditPairingSample(
            sampleDrink.Name,
            sampleDrink.Id,
            bakeryPoolCount,
            pairings.Count,
            pairings.Select(p => p.Name).ToList());
    }

    private bool? ResolveLocalFileExists(string? webRelativeUrl)
    {
        if (string.IsNullOrWhiteSpace(webRelativeUrl))
            return null;

        var trimmed = webRelativeUrl.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("//", StringComparison.Ordinal))
            return null;

        if (MenuImagePaths.IsManagedUrl(trimmed))
        {
            var normalized = MenuImagePaths.NormalizeToWebpUrl(trimmed) ?? trimmed;
            var physical = MenuImagePaths.ToPhysicalPath(environment, normalized);
            return physical is not null && File.Exists(physical);
        }

        if (!trimmed.StartsWith('/'))
            trimmed = "/" + trimmed;

        var rel = trimmed.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (rel.Contains("..", StringComparison.Ordinal))
            return null;

        var path = Path.Combine(environment.WebRootPath, rel);
        return File.Exists(path);
    }

    private static void AppendCategoryFindings(
        IReadOnlyList<DataAuditCategoryRow> categories,
        List<DataAuditFinding> findings)
    {
        if (categories.Count == 0)
            findings.Add(new DataAuditFinding(DataAuditLevel.Fail, "No menu categories found."));
    }

    private static void AppendBakeryFindings(
        (bool CategoryExists, string? CategoryName, int ItemCount, IReadOnlyList<string> ItemNames) bakery,
        List<DataAuditFinding> findings)
    {
        if (!bakery.CategoryExists)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Fail,
                "Bakery category missing (expected \"Bánh\")."));
            return;
        }

        if (bakery.ItemCount == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"Bakery category \"{bakery.CategoryName}\" exists but contains 0 items."));
        }
    }

    private static void AppendPairingFindings(DataAuditPairingSample? pairing, List<DataAuditFinding> findings)
    {
        if (pairing is null)
            return;

        if (pairing.BakeryPoolCount == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                "Pairing audit: bakery pool has 0 available items — drink detail pairings will be empty."));
        }
        else if (pairing.PairingsReturned == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"Pairing audit: sample drink \"{pairing.DrinkName}\" returned 0 pairings despite bakery pool of {pairing.BakeryPoolCount}."));
        }
    }

    private static void AppendSpecialtyFindings(
        int poolCount,
        IReadOnlyList<DataAuditSpecialtyRow> rows,
        List<DataAuditFinding> findings)
    {
        foreach (var key in AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys)
        {
            if (rows.All(r => !string.Equals(r.CatalogKey, key, StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(new DataAuditFinding(
                    DataAuditLevel.Fail,
                    $"Specialty catalog key {key} is missing from menu_items."));
            }
        }

        if (poolCount != 4)
        {
            findings.Add(new DataAuditFinding(
                poolCount == 0 ? DataAuditLevel.Fail : DataAuditLevel.Warn,
                $"specialty_pool_count={poolCount}, expected 4 (production specialty recommendation pool)."));
        }
    }

    private static void AppendMediaFindings(
        int missingImagePaths,
        int missingPosterPaths,
        List<DataAuditFinding> findings)
    {
        if (missingImagePaths > 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{missingImagePaths} ImageUrl path(s) reference missing files on disk."));
        }

        if (missingPosterPaths > 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{missingPosterPaths} DetailPosterImagePath value(s) reference missing files on disk."));
        }
    }
}
