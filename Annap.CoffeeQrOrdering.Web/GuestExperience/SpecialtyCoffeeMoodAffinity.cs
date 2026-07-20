using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Internal specialty discovery affinity for ranking only — not exposed to guests.
/// Primary: flavor archetype · Secondary: Q1 mood · Tertiary: cup personality.
/// </summary>
internal static class SpecialtyCoffeeMoodAffinity
{
    private const double FlavorMultiplier = 16.0;
    private const double MoodMultiplier = 6.0;
    private const double ExperienceMultiplier = 4.0;

    public static double Score(
        string? drinkName,
        string? catalogKey,
        string? moodKey,
        string? flavorArchetypeKey,
        string? experienceKey)
    {
        var product = ResolveProductKey(drinkName, catalogKey);
        if (product is null)
            return 0;

        var flavor = FlavorAffinity(product, flavorArchetypeKey) * FlavorMultiplier;
        var mood = MoodAffinity(product, moodKey) * MoodMultiplier;
        var experience = ExperienceAffinity(product, experienceKey) * ExperienceMultiplier;
        return flavor + mood + experience;
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

    private static double FlavorAffinity(string product, string? flavorArchetypeKey) =>
        (flavorArchetypeKey ?? "").Trim().ToLowerInvariant() switch
        {
            "floral" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.45,
                "rift_valley" => 0.15,
                "nigussie" => 0.20,
                _ => 0
            },
            "fruit_forward" => product switch
            {
                "abateranankunga" => 1.0,
                "dufatanye" => 0.50,
                "nigussie" => 0.35,
                "rift_valley" => 0.30,
                _ => 0
            },
            "wine_like" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.55,
                "abateranankunga" => 0.25,
                "dufatanye" => 0.15,
                _ => 0
            },
            "blueberry_honey" or "surprise" => product switch
            {
                "nigussie" => 1.0,
                "rift_valley" => 0.50,
                "abateranankunga" => 0.35,
                "dufatanye" => 0.20,
                _ => 0
            },
            "chocolate" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.70,
                "abateranankunga" => 0.25,
                "dufatanye" => 0.15,
                _ => 0
            },
            _ => 0
        };

    private static double MoodAffinity(string product, string? moodKey) =>
        (moodKey ?? "").Trim().ToLowerInvariant() switch
        {
            "calm" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.45,
                "rift_valley" => 0.15,
                "nigussie" => 0.20,
                _ => 0
            },
            "bright" => product switch
            {
                "abateranankunga" => 1.0,
                "dufatanye" => 0.55,
                "nigussie" => 0.35,
                "rift_valley" => 0.25,
                _ => 0
            },
            "focus" => product switch
            {
                "nigussie" => 0.85,
                "rift_valley" => 0.70,
                "dufatanye" => 0.35,
                "abateranankunga" => 0.30,
                _ => 0
            },
            "adventurous" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.90,
                "abateranankunga" => 0.40,
                "dufatanye" => 0.20,
                _ => 0
            },
            _ => 0
        };

    private static double ExperienceAffinity(string product, string? experienceKey) =>
        (experienceKey ?? "").Trim().ToLowerInvariant() switch
        {
            "soft" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.70,
                "nigussie" => 0.25,
                "rift_valley" => 0.15,
                _ => 0
            },
            "balanced" => product switch
            {
                "dufatanye" => 0.85,
                "abateranankunga" => 0.90,
                "nigussie" => 0.35,
                "rift_valley" => 0.25,
                _ => 0
            },
            "complex" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.85,
                "abateranankunga" => 0.30,
                "dufatanye" => 0.20,
                _ => 0
            },
            "surprising" => product switch
            {
                "nigussie" => 1.0,
                "rift_valley" => 0.90,
                "abateranankunga" => 0.35,
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
