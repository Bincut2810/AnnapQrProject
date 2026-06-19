using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly string[] LegacyDemoMenuNames =
    [
        "Single Origin Espresso",
        "Cappuccino",
        "V60 Pour-Over",
        "Cardamom Latte",
        "Affogato",
        "Dura Farm",
        "Ginger's Singer"
    ];

    private static readonly string[] LegacyDemoIngredientNames =
    [
        "House espresso blend",
        "Whole milk"
    ];

    /// <summary>Seeds QR-identified service tables (idempotent).</summary>
    public static async Task EnsureVenueTablesAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.VenueTables.AnyAsync(cancellationToken))
            return;

        const string venue = "annap";
        var tables = new List<VenueTable>();
        for (var i = 1; i <= 36; i++)
        {
            var code = $"T{i:00}";
            tables.Add(new VenueTable
            {
                VenueCode = venue,
                DisplayCode = code,
                PublicSlug = $"{venue}-{code.ToLowerInvariant()}",
                DisplayLabel = $"Table {i}",
                IsActive = true
            });
        }

        db.VenueTables.AddRange(tables);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes legacy prototype menu rows when they are not referenced by any submitted order line.
    /// Safe for production: historical orders keep their menu rows until archived elsewhere.
    /// </summary>
    public static async Task TryRemoveLegacyDemoMenuIfUnusedAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var legacySet = LegacyDemoMenuNames.ToHashSet(StringComparer.Ordinal);
        var candidates = await db.MenuItems
            .Where(m => legacySet.Contains(m.Name))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var used = await db.OrderItems
            .Where(o => candidates.Contains(o.MenuItemId))
            .Select(o => o.MenuItemId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var usedSet = used.ToHashSet();

        foreach (var id in candidates)
        {
            if (usedSet.Contains(id))
                continue;
            var row = await db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (row is not null)
                db.MenuItems.Remove(row);
        }

        await db.SaveChangesAsync(cancellationToken);

        var emptyLegacyCategories = await db.MenuCategories
            .Where(c =>
                c.Name == "Espresso Bar"
                || c.Name == "Filter & Pour-Over"
                || c.Name == "Signature")
            .Where(c => !db.MenuItems.Any(m => m.CategoryId == c.Id))
            .ToListAsync(cancellationToken);

        if (emptyLegacyCategories.Count > 0)
        {
            db.MenuCategories.RemoveRange(emptyLegacyCategories);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var ingName in LegacyDemoIngredientNames)
        {
            var ing = await db.Ingredients.FirstOrDefaultAsync(i => i.Name == ingName, cancellationToken);
            if (ing is null)
                continue;
            var linked = await db.MenuItemIngredients.AnyAsync(m => m.IngredientId == ing.Id, cancellationToken);
            if (!linked)
                db.Ingredients.Remove(ing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
