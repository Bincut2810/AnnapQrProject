namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Resolves drink poster/card URLs from local <c>wwwroot/images/annap-drinks/*.webp</c> only.
/// No remote URLs, no placeholders.
/// </summary>
public sealed class DrinkAssetResolver
{
    public const string WebFolder = "images/annap-drinks";

    private readonly object _gate = new();
    private IReadOnlyList<AssetEntry> _entries = [];

    public void RefreshIndex(string webRootPath)
    {
        var dir = Path.Combine(webRootPath, WebFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dir))
        {
            lock (_gate)
                _entries = [];
            return;
        }

        var list = new List<AssetEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.webp", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(name))
                continue;
            list.Add(new AssetEntry(name, MenuMediaResolver.NormalizeWebRelative($"{WebFolder}/{Path.GetFileName(file)}")));
        }

        lock (_gate)
            _entries = list;
    }

    /// <summary>Web-relative URL e.g. <c>/images/annap-drinks/sig-coco-bo.webp</c>, or null.</summary>
    public string? ResolveWebUrl(string? category, string? drinkName)
    {
        IReadOnlyList<AssetEntry> entries;
        lock (_gate)
            entries = _entries;
        if (entries.Count == 0 || string.IsNullOrWhiteSpace(drinkName))
            return null;

        var catalogKey = DrinkSlugGenerator.ToCatalogKey(category, drinkName);
        var prefix = DrinkSlugGenerator.CategoryPrefix(category);
        var tokens = DrinkSlugGenerator.SignificantTokens(drinkName);

        AssetEntry? best = null;
        var bestScore = 0;

        foreach (var e in entries)
        {
            var stem = e.Stem;
            if (stem.Equals(catalogKey, StringComparison.OrdinalIgnoreCase))
                return e.WebUrl;

            var score = 0;
            if (stem.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase)
                || stem.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                score += 4;

            foreach (var tok in tokens)
            {
                if (stem.Contains(tok, StringComparison.OrdinalIgnoreCase))
                    score += 2;
            }

            if (tokens.Count > 0 && stem.Contains(DrinkSlugGenerator.ToSlug(drinkName), StringComparison.OrdinalIgnoreCase))
                score += 3;

            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        const int minAccept = 4;
        return bestScore >= minAccept ? best?.WebUrl : null;
    }

    private sealed record AssetEntry(string Stem, string WebUrl);
}
