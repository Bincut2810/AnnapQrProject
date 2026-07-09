using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Maps slim seated sommelier UI choices to guided sommelier <c>optionIds</c>.
/// AI Lite targets stable <c>ExternalKey</c> values (q1_*, q2_*, …) seeded from
/// <see cref="GuidedSommelierCatalog"/>; compatibility is validated against the live CMS catalog
/// before mapping so label/prompt edits are safe but key removal disables Lite gracefully.
/// </summary>
public static class GuestSommelierLiteOptionMapper
{
    private static readonly HashSet<string> KnownTastes = new(StringComparer.OrdinalIgnoreCase)
        { "sweet", "balanced", "strong", "refreshing" };

    private static readonly HashSet<string> KnownMilk = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "no", "either" };

    private static readonly HashSet<string> KnownCaffeine = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "low", "no" };

    private static readonly HashSet<string> KnownTemperature = new(StringComparer.OrdinalIgnoreCase)
        { "iced", "hot", "either" };

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildValidatedPreferenceMap()
    {
        var drinkFamily = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var milk in KnownMilk)
        {
            foreach (var temperature in KnownTemperature)
            {
                drinkFamily[$"{milk}|{temperature}"] = MapDrinkFamily(milk, temperature);
            }
        }

        return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["taste"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sweet"] = "q1_light",
                ["balanced"] = "q1_alert",
                ["strong"] = "q1_curious",
                ["refreshing"] = "q1_refresh"
            },
            ["q3"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sweet"] = "q3_semisweet",
                ["balanced"] = "q3_medium",
                ["strong"] = "q3_low",
                ["refreshing"] = "q3_low"
            },
            ["caffeine"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["yes"] = "q4_medium",
                ["low"] = "q4_low",
                ["no"] = "q4_none"
            },
            ["drinkFamily"] = drinkFamily
        };
    }

    public static GuestSommelierLiteMapResult TryMap(
        IReadOnlyList<GuidedQuestionSeed> catalogQuestions,
        string? taste,
        string? milk,
        string? caffeine,
        string? temperature)
    {
        var compatibility = GuestSommelierLiteCompatibility.Assess(catalogQuestions);
        if (!compatibility.IsCompatible)
        {
            return GuestSommelierLiteMapResult.CatalogIncompatible(
                compatibility.ReasonCode,
                "Guided sommelier catalog is not compatible with AI Lite.");
        }

        if (!TryNormalizePreference(taste, KnownTastes, out var tasteKey)
            || !TryNormalizePreference(milk, KnownMilk, out var milkKey)
            || !TryNormalizePreference(caffeine, KnownCaffeine, out var caffeineKey)
            || !TryNormalizePreference(temperature, KnownTemperature, out var temperatureKey))
        {
            return GuestSommelierLiteMapResult.InvalidPreference("Unknown AI Lite preference value.");
        }

        var optionIds = MapNormalized(tasteKey, milkKey, caffeineKey, temperatureKey);
        var core = GuestSommelierLiteCompatibility.ExtractCoreQuestions(catalogQuestions);
        if (!GuidedSommelierExperienceCatalog.TryResolveOptions(core, optionIds, out _, out var resolveError))
        {
            return GuestSommelierLiteMapResult.CatalogIncompatible("resolve_failed", resolveError);
        }

        if (optionIds.Any(id => id.StartsWith("q_sc_", StringComparison.OrdinalIgnoreCase)))
        {
            return GuestSommelierLiteMapResult.CatalogIncompatible("specialty_path_blocked");
        }

        return GuestSommelierLiteMapResult.Ok(optionIds);
    }

    private static bool TryNormalizePreference(
        string? value,
        HashSet<string> known,
        out string normalized)
    {
        normalized = Normalize(value);
        return !string.IsNullOrEmpty(normalized) && known.Contains(normalized);
    }

    private static IReadOnlyList<string> MapNormalized(
        string taste,
        string milk,
        string caffeine,
        string temperature)
    {
        var map = BuildValidatedPreferenceMap();
        var tasteMap = map["taste"];
        var q3Map = map["q3"];
        var caffeineMap = map["caffeine"];
        var drinkFamilyMap = map["drinkFamily"];
        var familyKey = $"{milk}|{temperature}";

        return
        [
            tasteMap[taste],
            drinkFamilyMap[familyKey],
            q3Map[taste],
            caffeineMap[caffeine]
        ];
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

    private static string MapDrinkFamily(string milk, string temperature)
    {
        if (milk == "either" && temperature == "either")
            return "q2_signature";

        if (temperature == "iced")
            return milk == "yes" ? "q2_smoothie" : "q2_fruit";

        if (temperature == "hot")
        {
            return milk switch
            {
                "yes" => "q2_matcha",
                "no" => "q2_tea",
                _ => "q2_coffee"
            };
        }

        return milk switch
        {
            "yes" => "q2_smoothie",
            "no" => "q2_tea",
            _ => "q2_signature"
        };
    }
}
