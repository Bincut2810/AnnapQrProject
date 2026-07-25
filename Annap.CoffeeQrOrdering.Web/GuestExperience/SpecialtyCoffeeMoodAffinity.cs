using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Internal specialty discovery affinity for ranking only — not exposed to guests.
/// Primary: flavor (~60%) · Secondary: body (~25%) · Tertiary: exploration (~15%).
/// </summary>
internal static class SpecialtyCoffeeMoodAffinity
{
    // 12 + 5 + 3 = 20 → 60% / 25% / 15%
    private const double FlavorMultiplier = 12.0;
    private const double BodyMultiplier = 5.0;
    private const double ExploreMultiplier = 3.0;

    public static double Score(
        string? drinkName,
        string? catalogKey,
        string? bodyKey,
        string? flavorKey,
        string? exploreKey)
    {
        var product = ResolveProductKey(drinkName, catalogKey);
        if (product is null)
            return 0;

        return FlavorAffinity(product, flavorKey) * FlavorMultiplier
            + BodyAffinity(product, bodyKey) * BodyMultiplier
            + ExploreAffinity(product, exploreKey) * ExploreMultiplier;
    }

    public static string? ParseRefinementKey(IReadOnlyList<GuidedOptionSeed> selectedAnswers, string prefix)
    {
        foreach (var opt in selectedAnswers)
        {
            var key = opt.RefinementKey;
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            return key[prefix.Length..].Trim();
        }

        return null;
    }

    private static double FlavorAffinity(string product, string? flavorKey) =>
        (flavorKey ?? "").Trim().ToLowerInvariant() switch
        {
            "floral_tea" or "floral" or "tea" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.45,
                "rift_valley" => 0.15,
                "nigussie" => 0.20,
                _ => 0
            },
            "stone_fruit" or "peach" => product switch
            {
                "abateranankunga" => 1.0,
                "dufatanye" => 0.50,
                "nigussie" => 0.35,
                "rift_valley" => 0.30,
                _ => 0
            },
            "chocolate" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.55,
                "abateranankunga" => 0.25,
                "dufatanye" => 0.15,
                _ => 0
            },
            "berry" => product switch
            {
                "nigussie" => 1.0,
                "rift_valley" => 0.50,
                "abateranankunga" => 0.35,
                "dufatanye" => 0.20,
                _ => 0
            },
            _ => 0
        };

    private static double BodyAffinity(string product, string? bodyKey) =>
        (bodyKey ?? "").Trim().ToLowerInvariant() switch
        {
            "light" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.70,
                "nigussie" => 0.35,
                "rift_valley" => 0.15,
                _ => 0
            },
            "balanced" => product switch
            {
                "abateranankunga" => 1.0,
                "dufatanye" => 0.85,
                "nigussie" => 0.55,
                "rift_valley" => 0.40,
                _ => 0
            },
            "bold" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.75,
                "abateranankunga" => 0.30,
                "dufatanye" => 0.15,
                _ => 0
            },
            _ => 0
        };

    /// <summary>
    /// Cup-structure exploration — distinct from Flavor (what tastes of) and Body (weight).
    /// Vectors intentionally differ from flavor/body so options do not echo other axes.
    /// </summary>
    private static double ExploreAffinity(string product, string? exploreKey) =>
        (exploreKey ?? "").Trim().ToLowerInvariant() switch
        {
            // Clear single line, tea-like path through the cup → Dufatanye
            "linear" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.40,
                "nigussie" => 0.25,
                "rift_valley" => 0.10,
                _ => 0
            },
            // Ripe sweetness that wraps the whole sip → Abateranankunga
            "juicy" => product switch
            {
                "abateranankunga" => 1.0,
                "nigussie" => 0.45,
                "dufatanye" => 0.35,
                "rift_valley" => 0.20,
                _ => 0
            },
            // Builds deeper toward the back of the cup → Rift Valley
            "deepening" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.40,
                "abateranankunga" => 0.25,
                "dufatanye" => 0.10,
                _ => 0
            },
            // Layers that shift as the cup cools → Nigussie
            "layered" => product switch
            {
                "nigussie" => 1.0,
                "abateranankunga" => 0.40,
                "rift_valley" => 0.30,
                "dufatanye" => 0.15,
                _ => 0
            },
            _ => 0
        };

    private static string? ResolveProductKey(string? drinkName, string? catalogKey)
    {
        if (!string.IsNullOrWhiteSpace(catalogKey))
        {
            return catalogKey.Trim() switch
            {
                AnnapSpecialtyCoffeeCatalog.DufatanyeKey => "dufatanye",
                AnnapSpecialtyCoffeeCatalog.AbateranankungaKey => "abateranankunga",
                AnnapSpecialtyCoffeeCatalog.RiftValleyKey => "rift_valley",
                AnnapSpecialtyCoffeeCatalog.NigussieKey => "nigussie",
                _ => null
            };
        }

        var n = (drinkName ?? "").ToLowerInvariant();
        if (n.Contains("dufatanye", StringComparison.Ordinal))
            return "dufatanye";
        if (n.Contains("abateranankunga", StringComparison.Ordinal))
            return "abateranankunga";
        if (n.Contains("rift valley", StringComparison.Ordinal))
            return "rift_valley";
        if (n.Contains("nigussie", StringComparison.Ordinal) || n.Contains("murago", StringComparison.Ordinal))
            return "nigussie";
        return null;
    }
}
