using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Internal specialty discovery affinity for ranking only — not exposed to guests.
/// Primary: food-taste archetype · Secondary: today's cup mood · Tertiary: everyday habit.
/// </summary>
internal static class SpecialtyCoffeeMoodAffinity
{
    private const double FlavorMultiplier = 16.0;
    private const double TodayMultiplier = 6.0;
    private const double HabitMultiplier = 4.0;

    public static double Score(
        string? drinkName,
        string? catalogKey,
        string? todayKey,
        string? flavorArchetypeKey,
        string? habitKey)
    {
        var product = ResolveProductKey(drinkName, catalogKey);
        if (product is null)
            return 0;

        var flavor = FlavorAffinity(product, flavorArchetypeKey) * FlavorMultiplier;
        var today = TodayAffinity(product, todayKey) * TodayMultiplier;
        var habit = HabitAffinity(product, habitKey) * HabitMultiplier;
        return flavor + today + habit;
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
            "tea" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.45,
                "rift_valley" => 0.15,
                "nigussie" => 0.20,
                _ => 0
            },
            "peach" => product switch
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

    private static double TodayAffinity(string product, string? todayKey) =>
        (todayKey ?? "").Trim().ToLowerInvariant() switch
        {
            "gentle" => product switch
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
            "rich" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.70,
                "abateranankunga" => 0.30,
                "dufatanye" => 0.20,
                _ => 0
            },
            "fruity" => product switch
            {
                "nigussie" => 1.0,
                "abateranankunga" => 0.85,
                "rift_valley" => 0.40,
                "dufatanye" => 0.25,
                _ => 0
            },
            _ => 0
        };

    private static double HabitAffinity(string product, string? habitKey) =>
        (habitKey ?? "").Trim().ToLowerInvariant() switch
        {
            "bold" => product switch
            {
                "rift_valley" => 1.0,
                "nigussie" => 0.70,
                "abateranankunga" => 0.30,
                "dufatanye" => 0.20,
                _ => 0
            },
            "sweet" => product switch
            {
                "abateranankunga" => 1.0,
                "nigussie" => 0.65,
                "dufatanye" => 0.45,
                "rift_valley" => 0.30,
                _ => 0
            },
            "light" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.70,
                "nigussie" => 0.35,
                "rift_valley" => 0.15,
                _ => 0
            },
            "milk" => product switch
            {
                "dufatanye" => 1.0,
                "abateranankunga" => 0.80,
                "nigussie" => 0.30,
                "rift_valley" => 0.20,
                _ => 0
            },
            "guide" => product switch
            {
                "dufatanye" => 0.85,
                "abateranankunga" => 0.90,
                "nigussie" => 0.55,
                "rift_valley" => 0.40,
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
