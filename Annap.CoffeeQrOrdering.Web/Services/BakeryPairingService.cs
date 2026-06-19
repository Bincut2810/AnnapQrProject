using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Loads bakery (Bánh) suggestions for drink detail pairings.</summary>
public static class BakeryPairingService
{
    public const string BakeryCategoryName = "Bánh";

    public static bool IsBakeryCategory(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;
        var t = categoryName.Trim();
        return t.Equals(BakeryCategoryName, StringComparison.OrdinalIgnoreCase)
               || t.Equals("Banh", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task EnsureBakeryCategoryAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.MenuCategories
            .AnyAsync(c => c.Name == BakeryCategoryName, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        var maxSort = await db.MenuCategories
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        db.MenuCategories.Add(new Domain.Entities.MenuCategory
        {
            Name = BakeryCategoryName,
            SortOrder = (maxSort ?? 7) + 1
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<PairingSuggestionDto>> GetSuggestionsForDrinkAsync(
        IApplicationDbContext db,
        IMenuInventoryGate inventoryGate,
        Guid drinkId,
        string? drinkCategoryName,
        CancellationToken cancellationToken = default)
    {
        if (IsBakeryCategory(drinkCategoryName))
            return Array.Empty<PairingSuggestionDto>();

        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);

        var bakeryItems = await db.MenuItems
            .AsNoTracking()
            .Include(i => i.Category)
            .Where(i =>
                i.Category != null
                && (i.Category.Name == BakeryCategoryName || i.Category.Name == "Banh")
                && i.IsAvailable
                && !i.IsArchived
                && !blocked.Contains(i.Id))
            .OrderBy(i => i.DisplaySortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.ImageUrl, i.Price, CategoryName = i.Category!.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bakeryItems.Count == 0)
            return Array.Empty<PairingSuggestionDto>();

        var offset = Math.Abs(drinkId.GetHashCode()) % bakeryItems.Count;
        var rotated = bakeryItems.Skip(offset).Concat(bakeryItems.Take(offset)).Take(3).ToList();

        return rotated.Select(i =>
        {
            var card = MenuMediaResolver.TryResolveCardImageUrl(null, null, i.ImageUrl, null, i.Name, i.CategoryName) ?? "";
            return new PairingSuggestionDto
            {
                Id = i.Id,
                Name = i.Name,
                Image = card,
                Price = i.Price,
                PriceDisplay = i.Price.ToString("N0") + " đ"
            };
        }).ToList();
    }
}
