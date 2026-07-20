using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

public sealed record MenuItemScoringRow(
    Guid Id,
    string Name,
    decimal Price,
    string? TastingNotes,
    string? ShortStory,
    string? ImageUrl,
    string? MoodProfile,
    DrinkSensoryProfile Sensory,
    string CategoryName = "");

public sealed record GuidedRecommendationRow(
    Guid MenuItemId,
    string Name,
    decimal Price,
    string? TastingNotes,
    string? ShortStory,
    string? ImageUrl,
    int CompatibilityPercent,
    string EmotionalExplanation,
    string PalateAlignmentLabel,
    string Direction,
    string[] ExplanationTags,
    string ConfidenceLevel);

/// <summary>
/// Deterministic, weight-aware ranking using existing <see cref="FlavorAffinityEngine"/>.
/// </summary>
public static class GuidedSommelierRecommendationEngine
{
    public static IReadOnlyList<GuidedRecommendationRow> Rank(
        DrinkSensoryProfile guestHints,
        IReadOnlyList<GuidedOptionSeed> selectedAnswers,
        IReadOnlyList<MenuItemScoringRow> menu,
        int take = 5,
        IReadOnlyDictionary<Guid, decimal>? affinityBoostByMenuId = null)
    {
        if (menu.Count == 0)
            return [];

        var ambient = ComposeAmbientLine(selectedAnswers);
        var familyKey = ExtractBeverageFamilyKey(selectedAnswers);
        var beverageIntent = BuildBeverageIntent(familyKey, guestHints, selectedAnswers);
        var scopedMenu = ApplyFamilyLock(menu, familyKey);
        if (scopedMenu.Count == 0)
            return [];

        var categoryDirectionLine = ComposeCategoryDirectionLine(familyKey, selectedAnswers);
        var boost = affinityBoostByMenuId ?? new Dictionary<Guid, decimal>();
        var optionBlend = selectedAnswers.Count == 0
            ? 1.0
            : selectedAnswers.Average(o =>
                (double)Math.Clamp(o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier, 0.25m, 4m));
        var scored = scopedMenu
            .Select(m =>
            {
                var cup = m.Sensory;
                var raw = FlavorAffinityEngine.ScoreHintsVsCup(guestHints, cup);
                var profile = BeverageIntelligence.Classify(m.CategoryName, m.Name, cup);
                raw += BeverageIntelligence.SpecialtyScore(profile, beverageIntent) * 0.56;
                if (IsSpecialtyCoffeePath(selectedAnswers))
                {
                    var moodKey = selectedAnswers
                        .FirstOrDefault(o => o.OptionId.StartsWith("q_sp_tried_", StringComparison.OrdinalIgnoreCase))
                        ?.MoodKey
                        ?? selectedAnswers.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.MoodKey))?.MoodKey;
                    var flavorKey = SpecialtyCoffeeMoodAffinity.ParseRefinementKey(selectedAnswers, "sc_flavor:");
                    var experienceKey = SpecialtyCoffeeMoodAffinity.ParseRefinementKey(selectedAnswers, "sc_experience:");
                    raw += SpecialtyCoffeeMoodAffinity.Score(m.Name, null, moodKey, flavorKey, experienceKey);
                }

                if (boost.TryGetValue(m.Id, out var w) && w > 0)
                    raw += (double)w * 18.0 * optionBlend;
                return (m, raw);
            })
            .OrderByDescending(x => x.raw)
            .ThenBy(x => x.m.Id)
            .ToList();

        var n = Math.Min(take, scored.Count);
        var top = scored.Take(n).ToList();
        var r0 = top[0].raw;
        var rLast = top[^1].raw;
        var spanTop = r0 - rLast;
        var pcts = new int[n];
        if (spanTop < 1e-6)
        {
            for (var i = 0; i < n; i++)
                pcts[i] = Math.Clamp(94 - i * 6, 58, 96);
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                var raw = top[i].raw;
                var t = (raw - rLast) / spanTop;
                var curved = Math.Pow(Math.Clamp(t, 0, 1), 0.72);
                pcts[i] = (int)Math.Round(64 + curved * 34 - i * 2.2);
            }

            for (var i = n - 2; i >= 0; i--)
            {
                if (pcts[i] - pcts[i + 1] < 4)
                    pcts[i] = pcts[i + 1] + 4;
            }

            for (var i = 0; i < n; i++)
                pcts[i] = Math.Clamp(pcts[i], 58, 98);
        }

        var leadSensory = top[0].m.Sensory;

        var rows = new List<GuidedRecommendationRow>(n);
        for (var i = 0; i < n; i++)
        {
            var (m, _) = top[i];
            var pct = pcts[i];
            var explain = ExplainForRank(i, pct, m.Name, ambient, categoryDirectionLine, m.TastingNotes);
            var label = GuestDiscoveryRitualComposer.PalateAlignmentLabel(pct, i);
            var direction = i == 0 ? "lead" : ComputeDirection(m.Sensory, leadSensory);
            var explanationTags = ComputeExplanationTags(m.Sensory);
            var confidenceLevel = ComputeConfidenceLevel(pct);
            rows.Add(new GuidedRecommendationRow(
                m.Id,
                m.Name,
                m.Price,
                m.TastingNotes,
                m.ShortStory,
                m.ImageUrl,
                pct,
                explain,
                label,
                direction,
                explanationTags,
                confidenceLevel));
        }

        return rows;
    }

    public static string? ExtractBranchKey(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        foreach (var opt in selectedAnswers)
        {
            if (!string.IsNullOrWhiteSpace(opt.BranchKey))
                return opt.BranchKey.Trim();
            var fromId = GuidedSommelierCatalog.ResolveBranchKey(opt.OptionId);
            if (fromId is not null)
                return fromId;
        }

        return null;
    }

    public static bool IsSpecialtyCoffeePath(IReadOnlyList<GuidedOptionSeed> selectedAnswers) =>
        string.Equals(ExtractBranchKey(selectedAnswers), GuidedSommelierCatalog.BranchSpecialty, StringComparison.OrdinalIgnoreCase)
        || selectedAnswers.Any(o =>
            string.Equals(o.OptionId, "q0_specialty", StringComparison.OrdinalIgnoreCase));

    public static bool WantsCompareTwo(IReadOnlyList<GuidedOptionSeed> selectedAnswers) =>
        selectedAnswers.Any(o =>
            string.Equals(o.OptionId, "q_sp_format_compare", StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.RefinementKey, "sc_format:compare", StringComparison.OrdinalIgnoreCase));

    public static bool IsClassicCoffeePath(IReadOnlyList<GuidedOptionSeed> selectedAnswers) =>
        string.Equals(ExtractBranchKey(selectedAnswers), GuidedSommelierCatalog.BranchCoffee, StringComparison.OrdinalIgnoreCase);

    public static string ComposeSpecialtyNamingLine(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var profile = selectedAnswers
            .FirstOrDefault(o => o.OptionId.StartsWith("q_sp_profile_", StringComparison.OrdinalIgnoreCase))
            ?.EmotionalFragment.Trim() ?? "";
        var adventure = selectedAnswers
            .FirstOrDefault(o => o.OptionId.StartsWith("q_sp_adventure_", StringComparison.OrdinalIgnoreCase))
            ?.EmotionalFragment.Trim() ?? "";
        if (profile.Length > 0 && adventure.Length > 0)
            return $"Với hướng {profile} và mức {adventure} — quầy mở một nguồn cho bàn bạn.";
        if (profile.Length > 0)
            return $"Với hướng {profile} — quầy gọi tên nguồn này cho bàn bạn.";
        return "Quầy gọi tên nguồn này cho bàn bạn hôm nay.";
    }

    public static string ComposePersonalityReflection(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        if (IsSpecialtyCoffeePath(selectedAnswers))
            return ComposeSpecialtyNamingLine(selectedAnswers);

        if (selectedAnswers.Count == 0)
            return "Annap đang chọn một ly hợp với ghi chú này.";

        var family = selectedAnswers[0].Label.Trim();
        var detail = selectedAnswers.Count > 1 ? selectedAnswers[1].Label.Trim().ToLowerInvariant() : "";
        if (family.Length > 0 && detail.Length > 0)
            return $"Vì bạn chọn {family.ToLowerInvariant()} và nghiêng về {detail}, Annap chọn cho bạn.";
        if (family.Length > 0)
            return $"Vì bạn chọn {family.ToLowerInvariant()}, Annap chọn cho bạn.";

        return "Annap chọn cho bạn.";
    }

    public static string ComposeAmbientLine(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        if (selectedAnswers.Count == 0)
            return "Tìm theo sở thích của bạn trong menu.";

        var family = selectedAnswers[0].EmotionalFragment.Trim();
        var detail = selectedAnswers.Count >= 2 ? selectedAnswers[1].EmotionalFragment.Trim() : "";

        if (family.Length == 0 && detail.Length == 0)
            return "Tìm theo sở thích của bạn trong menu.";

        if (detail.Length > 0 && family.Length > 0)
            return $"{Capitalize(family)} · {detail} — tìm trong menu theo hướng đó.";

        var primary = family.Length > 0 ? family : detail;
        return $"{Capitalize(primary)} — tìm trong menu theo hướng đó.";
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : string.Concat(s[..1].ToUpperInvariant(), s[1..]);

    private static string ExplainForRank(int rank, int pct, string drinkName, string ambient, string categoryDirectionLine, string? tastingNotes = null)
    {
        _ = pct;
        var name = string.IsNullOrWhiteSpace(drinkName) ? "Ly này" : drinkName.Trim();
        var dir = (categoryDirectionLine ?? "").Trim().TrimEnd('—', ' ', '-');
        var taste = string.IsNullOrWhiteSpace(tastingNotes) ? "" : tastingNotes.Trim().TrimEnd('.');
        if (rank == 0)
        {
            if (dir.Length > 0 && taste.Length > 0)
                return $"{dir} {name} sẽ hợp với buổi hôm nay: {taste}.";
            if (dir.Length > 0)
                return $"{dir} {name} là lựa chọn Annap gửi cho bạn hôm nay.";
            if (!string.IsNullOrWhiteSpace(ambient))
                return $"{ambient.Trim()} {name} là lựa chọn Annap gửi cho bạn hôm nay.";
            if (taste.Length > 0)
                return $"Annap chọn {name} cho gu hôm nay của bạn — {taste}.";
            return $"Annap chọn {name} cho gu hôm nay của bạn.";
        }

        if (rank == 1)
            return $"{name} cũng là một hướng khác đáng thử.";

        return $"{name} là một lựa chọn dịu hơn nếu bạn muốn đổi nhịp.";
    }

    private static string ComputeDirection(DrinkSensoryProfile cup, DrinkSensoryProfile lead)
    {
        var cafDiff = cup.CaffeineIntensity - lead.CaffeineIntensity;
        if (cafDiff <= -1)
            return "softer";
        if (cafDiff >= 1)
            return "bolder";
        var isCupIntense = cup.Energy is "intense" or "playful" or "lifted";
        var isLeadIntense = lead.Energy is "intense" or "playful" or "lifted";
        if (!isCupIntense && isLeadIntense)
            return "softer";
        if (isCupIntense && !isLeadIntense)
            return "bolder";
        return "softer";
    }

    private static string[] ComputeExplanationTags(DrinkSensoryProfile cup)
    {
        var tags = new List<string>(6);

        switch (cup.AromaFamily)
        {
            case "citrus": tags.Add("citrus"); tags.Add("bright"); break;
            case "cocoa": tags.Add("chocolate"); tags.Add("deep"); break;
            case "floral": tags.Add("floral"); break;
            case "spice": tags.Add("earthy"); break;
            case "tropical": tags.Add("tropical"); tags.Add("juicy"); break;
        }

        switch (cup.Texture)
        {
            case "velvet": tags.Add("velvety"); break;
            case "satin": tags.Add("silky"); break;
            case "crisp": tags.Add("crisp"); break;
            case "syrupy": tags.Add("dense"); tags.Add("rich"); break;
        }

        switch (cup.Energy)
        {
            case "still": tags.Add("calm"); break;
            case "lifted": tags.Add("uplifting"); break;
            case "focused": tags.Add("focused"); break;
            case "playful": tags.Add("creative"); break;
            case "intense": tags.Add("energetic"); break;
        }

        if (cup.CaffeineIntensity is >= 1 and <= 2)
            tags.Add("low-caffeine");
        else if (cup.CaffeineIntensity == 3)
            tags.Add("medium-caffeine");
        else if (cup.CaffeineIntensity is >= 4 and <= 5)
            tags.Add("strong-caffeine");

        if (cup.TemperatureEmotion == "warming")
            tags.Add("warming");
        else if (cup.TemperatureEmotion is "cooling" or "temperate")
            tags.Add("cooling");

        if (cup.SocialMood is "gathered")
            tags.Add("social");
        else if (cup.SocialMood is "solitary" or "quiet")
            tags.Add("solo");

        if (cup.Sweetness is "luscious")
            tags.Add("rich");
        else if (cup.Sweetness is "gentle" or "rounded")
            tags.Add("smooth");

        if (cup.Finish is "clean" || cup.Acidity is "crystalline" or "lifted")
            tags.Add("refreshing");

        return tags.Distinct().Take(4).ToArray();
    }

    private static string ComputeConfidenceLevel(int pct) => pct switch
    {
        >= 88 => "Strong",
        >= 74 => "Good",
        _ => "Worthy"
    };

    public static string? ExtractBeverageFamilyKey(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        foreach (var opt in selectedAnswers)
        {
            var key = BeverageFamilyGrounding.ResolveFamilyKey(opt.CategoryIntentKey);
            if (key is not null)
                return key;
        }

        // Fallback: infer from option IDs and labels for DB-backed options that predate CategoryIntentKey.
        foreach (var opt in selectedAnswers)
        {
            var key = BeverageFamilyGrounding.ResolveFamilyKey(opt.OptionId, opt.Label, opt.EmotionalFragment);
            if (key is not null)
                return key;
        }

        return null;
    }

    public static BeverageIntent BuildBeverageIntent(
        string? familyKey,
        DrinkSensoryProfile guestHints,
        IReadOnlyList<GuidedOptionSeed> selectedAnswers) =>
        BeverageIntelligence.BuildIntent(
            familyKey,
            guestHints,
            selectedAnswers.SelectMany(o => new[] { o.OptionId, o.Label, o.EmotionalFragment, o.MoodKey, o.RefinementKey, o.CategoryIntentKey }));

    public static IReadOnlyList<MenuItemScoringRow> ApplyFamilyLock(
        IReadOnlyList<MenuItemScoringRow> menu,
        string? familyKey) =>
        BeverageFamilyGrounding.NormalizeFamilyKey(familyKey) is null
            ? menu
            : menu
                .Where(m => BeverageFamilyGrounding.Matches(familyKey, m.CategoryName, m.Name))
                .ToList();

    public static string ComposeCategoryDirectionLine(
        string? familyKey,
        IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var family = BeverageFamilyGrounding.NormalizeFamilyKey(familyKey);
        if (family is null)
            return "";

        var cafLevel = 0;
        var sweetnessKey = "";
        foreach (var opt in selectedAnswers)
        {
            if (cafLevel == 0 && opt.SensoryHints.CaffeineIntensity is >= 1 and <= 5)
                cafLevel = opt.SensoryHints.CaffeineIntensity;
            if (sweetnessKey.Length == 0 && !string.IsNullOrWhiteSpace(opt.SensoryHints.Sweetness))
                sweetnessKey = opt.SensoryHints.Sweetness.Trim().ToLowerInvariant();
        }

        var isSweet = sweetnessKey is "luscious" or "gentle";
        var isLow = sweetnessKey is "restrained";

        return family switch
        {
            BeverageFamilyGrounding.Coffee when cafLevel >= 4 && isSweet =>
                "Vì bạn muốn một ly rõ vị và đủ ngọt,",
            BeverageFamilyGrounding.Coffee when cafLevel >= 4 =>
                "Vì bạn muốn một ly rõ vị và tỉnh táo,",
            BeverageFamilyGrounding.Coffee when cafLevel <= 2 && isLow =>
                "Vì bạn muốn một ly cà phê nhẹ và ít ngọt,",
            BeverageFamilyGrounding.Coffee when cafLevel <= 2 =>
                "Vì bạn muốn một ly cà phê êm hơn,",
            BeverageFamilyGrounding.Coffee when isLow =>
                "Vì bạn muốn cà phê ít ngọt,",
            BeverageFamilyGrounding.Coffee when isSweet =>
                "Vì bạn thích vị cà phê mềm hơn,",
            BeverageFamilyGrounding.Coffee =>
                "Vì bạn đang nghiêng về cà phê,",
            BeverageFamilyGrounding.Tea =>
                "Vì bạn chọn trà,",
            BeverageFamilyGrounding.Juice when isLow =>
                "Vì bạn muốn một ly mát và ít ngọt,",
            BeverageFamilyGrounding.Juice =>
                "Vì bạn muốn một ly mát và dễ uống,",
            BeverageFamilyGrounding.Smoothie when isLow =>
                "Vì bạn muốn sinh tố ít ngọt,",
            BeverageFamilyGrounding.Smoothie =>
                "Vì bạn muốn một ly mềm và dễ uống,",
            BeverageFamilyGrounding.Matcha =>
                "Vì bạn chọn matcha,",
            BeverageFamilyGrounding.Fruit when isLow =>
                "Vì bạn muốn trái cây nhẹ và ít ngọt,",
            BeverageFamilyGrounding.Fruit =>
                "Vì bạn muốn một ly mát và dễ uống,",
            BeverageFamilyGrounding.Signature =>
                "Vì bạn muốn thử signature của quán,",
            _ => ""
        };
    }

    /// <summary>Classic coffee menu (espresso / VN / cold brew) — exclude specialty lots.</summary>
    public static IReadOnlyList<MenuItemScoringRow> ApplyClassicCoffeeFilter(
        IReadOnlyList<MenuItemScoringRow> menu) =>
        menu.Where(m =>
        {
            var cat = (m.CategoryName ?? "").Trim();
            return !cat.Equals("Specialty Coffee", StringComparison.OrdinalIgnoreCase);
        }).ToList();
}
