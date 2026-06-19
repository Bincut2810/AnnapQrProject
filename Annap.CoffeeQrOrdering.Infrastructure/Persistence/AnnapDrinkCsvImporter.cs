using System.Globalization;
using System.Text;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

/// <summary>Imports the canonical Annap beverage list from <c>AnnapDrinks.csv</c>.</summary>
public static class AnnapDrinkCsvImporter
{
    private static readonly Dictionary<string, int> CategorySortHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Specialty Coffee"] = 0,
        ["Signature"] = 1,
        ["Espresso"] = 2,
        ["Tea"] = 3,
        ["Smoothie"] = 4,
        ["Juice"] = 5,
        ["Cold Brew"] = 6,
        ["Vietnamese Coffee"] = 7,
        ["Bánh"] = 8
    };

    private static readonly Dictionary<string, decimal> DefaultPriceByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Specialty Coffee"] = 80000m,
        ["Signature"] = 65000m,
        ["Espresso"] = 45000m,
        ["Tea"] = 42000m,
        ["Smoothie"] = 48000m,
        ["Juice"] = 45000m,
        ["Cold Brew"] = 49000m,
        ["Vietnamese Coffee"] = 35000m,
        ["Bánh"] = 35000m
    };

    public sealed record NormalizedDrinkRow(
        string Category,
        string Name,
        string? Ingredients,
        string? Origin,
        string? Taste,
        string CatalogKey,
        string? ImageWebUrl);

    public static async Task<int> TryImportFromCsvAsync(
        IApplicationDbContext db,
        string? absoluteCsvPath,
        Func<string?, string?, string?> resolveImageUrl,
        ILogger? log,
        CancellationToken cancellationToken = default,
        Func<string?, bool>? preserveExistingImageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(absoluteCsvPath) || !File.Exists(absoluteCsvPath))
        {
            log?.LogDebug("Annap drink CSV import skipped: file not found ({Path}).", absoluteCsvPath);
            return 0;
        }

        var rows = ParseCsv(absoluteCsvPath);
        if (rows.Count == 0)
        {
            log?.LogWarning("Annap drink CSV contained no rows.");
            return 0;
        }

        var catRows = await db.MenuCategories.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var categoryByKey = new Dictionary<string, MenuCategory>(StringComparer.Ordinal);
        foreach (var c in catRows)
        {
            var nk = NormalizeCategoryKey(c.Name);
            if (!string.IsNullOrEmpty(nk) && !categoryByKey.ContainsKey(nk))
                categoryByKey[nk] = c;
        }

        var touchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var touched = 0;

        foreach (var row in rows)
        {
            var nk = NormalizeCategoryKey(row.Category);
            if (string.IsNullOrEmpty(nk))
                continue;

            if (!categoryByKey.TryGetValue(nk, out var category))
            {
                var display = ResolveCategoryDisplayName(nk, row.Category);
                var sort = CategorySortHints.TryGetValue(display, out var hint)
                    ? hint
                    : 90 + categoryByKey.Count;
                category = new MenuCategory { Name = display, SortOrder = sort };
                db.MenuCategories.Add(category);
                categoryByKey[nk] = category;
            }

            var categoryId = category.Id;
            var catalogKey = row.CatalogKey;

            var entity = await db.MenuItems
                .FirstOrDefaultAsync(
                    m => m.CategoryId == categoryId && m.CatalogKey == catalogKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                entity = await db.MenuItems
                    .FirstOrDefaultAsync(
                        m => m.CategoryId == categoryId && m.Name == row.Name,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (entity is null)
            {
                entity = new MenuItem { CategoryId = categoryId };
                db.MenuItems.Add(entity);
            }

            entity.Name = row.Name;
            entity.CategoryId = categoryId;
            entity.CatalogKey = catalogKey;
            entity.IngredientBreakdown = NullIfEmpty(row.Ingredients);
            entity.Origin = NormalizeOrigin(row.Origin);
            entity.TastingNotes = NullIfEmpty(row.Taste);
            entity.MoodProfile = NullIfEmpty(row.Taste);
            entity.ShortStory = null;
            entity.Description = null;
            entity.FlavorTags = null;
            entity.Subtitle = null;
            entity.ItemType = null;

            var imageUrl = resolveImageUrl(row.Category, row.Name);
            if (!ShouldPreserveImage(entity.ImageUrl, preserveExistingImageUrl))
                entity.ImageUrl = imageUrl;
            if (!ShouldPreserveImage(entity.DetailPosterImagePath, preserveExistingImageUrl))
                entity.DetailPosterImagePath = imageUrl;

            entity.IsAvailable = true;
            entity.IsArchived = false;
            entity.IsSignature = row.Category.Equals("Signature", StringComparison.OrdinalIgnoreCase);
            entity.IsFeatured = entity.IsSignature;
            entity.IsSeasonalHighlight = false;
            entity.IsDiscoveryEligible = true;
            entity.DiscoveryWeight = entity.IsSignature ? 1.5m : 1m;

            if (entity.Price <= 0)
                entity.Price = DefaultPriceByCategory.TryGetValue(row.Category, out var p) ? p : 45000m;

            if (!sortByCategory.TryGetValue(row.Category, out var ord))
                sortByCategory[row.Category] = 0;
            entity.DisplaySortOrder = sortByCategory[row.Category]++;

            touchedKeys.Add(catalogKey);
            touched++;
        }

        if (touched > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await AlignCategoryOrderAsync(db, cancellationToken).ConfigureAwait(false);
            await PurgeDrinksNotInImportAsync(db, touchedKeys, cancellationToken).ConfigureAwait(false);
        }

        log?.LogInformation("Annap drink CSV import finished: {Count} beverages.", touched);
        return touched;
    }

    public static List<NormalizedDrinkRow> ParseCsv(string absoluteCsvPath)
    {
        using var reader = new StreamReader(absoluteCsvPath, Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };
        using var csv = new CsvReader(reader, config);
        var raw = csv.GetRecords<AnnapDrinkCsvRow>().ToList();
        var list = new List<NormalizedDrinkRow>(raw.Count);
        foreach (var r in raw)
        {
            if (string.IsNullOrWhiteSpace(r.TenMon) || string.IsNullOrWhiteSpace(r.Category))
                continue;
            var name = r.TenMon.Trim();
            var cat = r.Category.Trim();
            var key = DrinkSlugGeneratorShim.ToCatalogKey(cat, name);
            list.Add(new NormalizedDrinkRow(
                cat,
                name,
                NullIfEmpty(r.NguyenLieu),
                NullIfEmpty(r.NguonGoc),
                NullIfEmpty(r.Vi),
                key,
                null));
        }

        return list;
    }

    private static async Task PurgeDrinksNotInImportAsync(
        IApplicationDbContext db,
        HashSet<string> importCatalogKeys,
        CancellationToken cancellationToken)
    {
        var all = await db.MenuItems.ToListAsync(cancellationToken).ConfigureAwait(false);
        var toDelete = new List<MenuItem>();
        foreach (var item in all)
        {
            if (string.IsNullOrWhiteSpace(item.CatalogKey))
                continue;
            if (importCatalogKeys.Contains(item.CatalogKey))
                continue;
            if (AnnapSpecialtyCoffeeCatalog.IsProtectedCatalogKey(item.CatalogKey))
                continue;
            var hasOrders = await db.OrderItems
                .AnyAsync(o => o.MenuItemId == item.Id, cancellationToken)
                .ConfigureAwait(false);
            if (hasOrders)
            {
                item.IsArchived = true;
                item.IsAvailable = false;
            }
            else
            {
                toDelete.Add(item);
            }
        }

        if (toDelete.Count > 0)
            db.MenuItems.RemoveRange(toDelete);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AlignCategoryOrderAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        foreach (var kv in CategorySortHints)
        {
            await db.MenuCategories
                .Where(c => c.Name == kv.Key)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SortOrder, kv.Value), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string? NormalizeOrigin(string? origin)
    {
        var t = NullIfEmpty(origin);
        if (t is null)
            return null;
        if (t.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return null;
        return t;
    }

    private static string NormalizeCategoryKey(string? name)
    {
        var t = name?.Trim();
        if (string.IsNullOrEmpty(t))
            return "";
        return t.ToLowerInvariant();
    }

    private static string ResolveCategoryDisplayName(string normalizedKey, string? raw)
    {
        foreach (var kv in CategorySortHints)
        {
            if (NormalizeCategoryKey(kv.Key) == normalizedKey)
                return kv.Key;
        }

        return string.IsNullOrWhiteSpace(raw) ? normalizedKey : raw.Trim();
    }

    private static string? NullIfEmpty(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static bool ShouldPreserveImage(string? existingUrl, Func<string?, bool>? preserveExistingImageUrl)
        => preserveExistingImageUrl is not null && preserveExistingImageUrl(existingUrl);

    private sealed class AnnapDrinkCsvRow
    {
        public string Category { get; set; } = "";
        [CsvHelper.Configuration.Attributes.Name("Tên món")]
        public string TenMon { get; set; } = "";
        [CsvHelper.Configuration.Attributes.Name("Nguyên liệu")]
        public string? NguyenLieu { get; set; }
        [CsvHelper.Configuration.Attributes.Name("Nguồn gốc / Xuất xứ")]
        public string? NguonGoc { get; set; }
        [Name("Vị")]
        public string? Vi { get; set; }
    }

    /// <summary>Duplicate slug rules in Infrastructure without referencing Web project.</summary>
    private static class DrinkSlugGeneratorShim
    {
        public static string ToCatalogKey(string? category, string? drinkName)
        {
            var prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Signature"] = "sig",
                ["Espresso"] = "esp",
                ["Tea"] = "tea",
                ["Smoothie"] = "sm",
                ["Juice"] = "juice",
                ["Cold Brew"] = "cb",
                ["Vietnamese Coffee"] = "vnc"
            };
            var prefix = "annap";
            if (!string.IsNullOrWhiteSpace(category) && prefixes.TryGetValue(category.Trim(), out var p))
                prefix = p;
            var slug = ToSlug(drinkName);
            return string.IsNullOrEmpty(slug) ? prefix : $"{prefix}-{slug}";
        }

        private static string ToSlug(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            var t = text.Trim().ToLowerInvariant();
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
            t = RemoveDiacritics(t);
            t = System.Text.RegularExpressions.Regex.Replace(t, @"[^a-z0-9]+", "-").Trim('-');
            while (t.Contains("--", StringComparison.Ordinal))
                t = t.Replace("--", "-", StringComparison.Ordinal);
            return t;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
        }
    }
}
