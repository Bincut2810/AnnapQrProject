using Annap.CoffeeQrOrdering.Web.GuestExperience;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Validates whether the live guided sommelier catalog supports AI Sommelier Lite on seated QR arrival.
/// atelier_v5 is category-branched; Lite remains gated off until preference maps are rewritten.
/// </summary>
public sealed record GuestSommelierLiteCompatibilityResult(
    bool IsCompatible,
    string? ReasonCode,
    IReadOnlyList<string> MissingOptionIds,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PreferenceMap)
{
    public object ToClientDto() => new
    {
        compatible = IsCompatible,
        reasonCode = ReasonCode,
        missingOptionIds = MissingOptionIds,
        preferenceMap = new
        {
            taste = PreferenceMap.GetValueOrDefault("taste"),
            q3 = PreferenceMap.GetValueOrDefault("q3"),
            caffeine = PreferenceMap.GetValueOrDefault("caffeine"),
            drinkFamily = PreferenceMap.GetValueOrDefault("drinkFamily")
        }
    };
}

public static class GuestSommelierLiteCompatibility
{
    public static readonly string[] CoreQuestionIds = ["q1", "q2", "q3", "q4"];

    public static readonly string[] RequiredOptionIds =
    [
        "q1_light", "q1_alert", "q1_curious", "q1_refresh",
        "q2_signature", "q2_smoothie", "q2_fruit", "q2_matcha", "q2_tea", "q2_coffee",
        "q3_semisweet", "q3_medium", "q3_low",
        "q4_medium", "q4_low", "q4_none"
    ];

    public static GuestSommelierLiteCompatibilityResult Assess(IReadOnlyList<GuidedQuestionSeed> loadedCatalog)
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(loadedCatalog);
        var hasV5Entry = merged.Any(q =>
            string.Equals(q.QuestionId, GuidedSommelierCatalog.EntryQuestionId, StringComparison.OrdinalIgnoreCase));
        if (hasV5Entry || string.Equals(GuidedSommelierCatalog.QuestionSetId, "atelier_v5", StringComparison.OrdinalIgnoreCase))
        {
            return Incompatible("core_questions_missing", ["q1", "q2", "q3", "q4"]);
        }

        var core = ExtractCoreQuestions(merged);
        if (core.Count != CoreQuestionIds.Length)
            return Incompatible("core_questions_missing", ["q1", "q2", "q3", "q4"]);

        for (var i = 0; i < CoreQuestionIds.Length; i++)
        {
            if (!string.Equals(core[i].QuestionId, CoreQuestionIds[i], StringComparison.OrdinalIgnoreCase))
                return Incompatible("core_questions_missing", [CoreQuestionIds[i]]);
        }

        var available = core
            .SelectMany(q => q.Options)
            .Select(o => o.OptionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var preferenceMap = GuestSommelierLiteOptionMapper.BuildValidatedPreferenceMap();
        var mappedIds = preferenceMap.Values
            .SelectMany(group => group.Values)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missing = mappedIds.Where(id => !available.Contains(id)).ToList();
        if (missing.Count > 0)
            return Incompatible("required_options_missing", missing);

        return new GuestSommelierLiteCompatibilityResult(
            true,
            null,
            [],
            preferenceMap);
    }

    public static IReadOnlyList<GuidedQuestionSeed> ExtractCoreQuestions(IReadOnlyList<GuidedQuestionSeed> loaded)
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(loaded);
        return merged
            .Where(q => !GuidedSommelierCatalog.IsSpecialtyDiscoveryQuestionId(q.QuestionId)
                        && !GuidedSommelierCatalog.IsBranchQuestionId(q.QuestionId)
                        && !string.Equals(q.QuestionId, GuidedSommelierCatalog.EntryQuestionId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(q => q.QuestionId switch
            {
                "q1" => 0,
                "q2" => 1,
                "q3" => 2,
                "q4" => 3,
                _ => 99
            })
            .Take(CoreQuestionIds.Length)
            .ToList();
    }

    private static GuestSommelierLiteCompatibilityResult Incompatible(string reason, IReadOnlyList<string> missing)
        => new(false, reason, missing, GuestSommelierLiteOptionMapper.BuildValidatedPreferenceMap());
}
