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
    GuestLocalizedText EmotionalExplanation,
    string PalateAlignmentLabel,
    string Direction,
    string[] ExplanationTags,
    string ConfidenceLevel);

/// <summary>Guest-facing copy authored natively in Vietnamese and English (not machine-translated).</summary>
public sealed record GuestLocalizedText(string Vi, string En)
{
    public static GuestLocalizedText Of(string vi, string? en = null) =>
        new((vi ?? "").Trim(), string.IsNullOrWhiteSpace(en) ? (vi ?? "").Trim() : en.Trim());

    public object ToClientDto() => new { vi = Vi, en = En };
}

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
        scopedMenu = ApplyMenuTargetFilter(scopedMenu, selectedAnswers);
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

    public static bool IsClassicCoffeePath(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var branch = ExtractBranchKey(selectedAnswers);
        return string.Equals(branch, GuidedSommelierCatalog.BranchEspresso, StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, GuidedSommelierCatalog.BranchVietnamese, StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, GuidedSommelierCatalog.BranchColdBrew, StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, GuidedSommelierCatalog.BranchCoffee, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When leaf answers name exact menu drinks, hard-filter to those drinks (within family).
    /// </summary>
    public static IReadOnlyList<MenuItemScoringRow> ApplyMenuTargetFilter(
        IReadOnlyList<MenuItemScoringRow> menu,
        IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var targets = GuidedSommelierCatalog.CollectMenuTargets(selectedAnswers);
        if (targets.Count == 0 || menu.Count == 0)
            return menu;

        var hit = menu
            .Where(m => targets.Any(t => MenuNamesMatch(m.Name, t)))
            .ToList();
        return hit.Count > 0 ? hit : menu;
    }

    private static bool MenuNamesMatch(string? menuName, string? target)
    {
        if (string.IsNullOrWhiteSpace(menuName) || string.IsNullOrWhiteSpace(target))
            return false;
        return string.Equals(menuName.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static GuestLocalizedText ComposeSpecialtyNamingLine(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var profileVi = FragVi(selectedAnswers.FirstOrDefault(o =>
            o.OptionId.StartsWith("q_sp_profile_", StringComparison.OrdinalIgnoreCase)));
        var profileEn = FragEn(selectedAnswers.FirstOrDefault(o =>
            o.OptionId.StartsWith("q_sp_profile_", StringComparison.OrdinalIgnoreCase)));
        var adventureVi = FragVi(selectedAnswers.FirstOrDefault(o =>
            o.OptionId.StartsWith("q_sp_adventure_", StringComparison.OrdinalIgnoreCase)));
        var adventureEn = FragEn(selectedAnswers.FirstOrDefault(o =>
            o.OptionId.StartsWith("q_sp_adventure_", StringComparison.OrdinalIgnoreCase)));

        if (profileVi.Length > 0 && adventureVi.Length > 0)
            return GuestLocalizedText.Of(
                $"Với hướng {profileVi} và mức {adventureVi} — quầy mở một nguồn cho bàn bạn.",
                $"With a {profileEn} direction and {adventureEn} curiosity — the bar opens an origin for your table.");
        if (profileVi.Length > 0)
            return GuestLocalizedText.Of(
                $"Với hướng {profileVi} — quầy gọi tên nguồn này cho bàn bạn.",
                $"With a {profileEn} direction — the bar names this origin for your table.");
        return GuestLocalizedText.Of(
            "Quầy gọi tên nguồn này cho bàn bạn hôm nay.",
            "The bar names this origin for your table today.");
    }

    public static GuestLocalizedText ComposePersonalityReflection(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        if (IsSpecialtyCoffeePath(selectedAnswers))
            return ComposeSpecialtyNamingLine(selectedAnswers);

        if (selectedAnswers.Count == 0)
            return GuestLocalizedText.Of(
                "Annap đang chọn một ly hợp với ghi chú này.",
                "Annap is choosing a cup to match this note.");

        var familyVi = LabelVi(selectedAnswers[0]);
        var familyEn = LabelEn(selectedAnswers[0]);
        var detailVi = selectedAnswers.Count > 1 ? LabelVi(selectedAnswers[1]).ToLowerInvariant() : "";
        var detailEn = selectedAnswers.Count > 1 ? LabelEn(selectedAnswers[1]).ToLowerInvariant() : "";
        if (familyVi.Length > 0 && detailVi.Length > 0)
            return GuestLocalizedText.Of(
                $"Vì bạn chọn {familyVi.ToLowerInvariant()} và nghiêng về {detailVi}, Annap chọn cho bạn.",
                $"Because you chose {familyEn.ToLowerInvariant()} and leaned toward {detailEn}, Annap chose for you.");
        if (familyVi.Length > 0)
            return GuestLocalizedText.Of(
                $"Vì bạn chọn {familyVi.ToLowerInvariant()}, Annap chọn cho bạn.",
                $"Because you chose {familyEn.ToLowerInvariant()}, Annap chose for you.");

        return GuestLocalizedText.Of("Annap chọn cho bạn.", "Annap chooses for you.");
    }

    public static GuestLocalizedText ComposeAmbientLine(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        if (selectedAnswers.Count == 0)
            return GuestLocalizedText.Of(
                "Tìm theo sở thích của bạn trong menu.",
                "Browse the menu for what suits you.");

        var familyVi = FragVi(selectedAnswers[0]);
        var familyEn = FragEn(selectedAnswers[0]);
        var detailVi = selectedAnswers.Count >= 2 ? FragVi(selectedAnswers[1]) : "";
        var detailEn = selectedAnswers.Count >= 2 ? FragEn(selectedAnswers[1]) : "";

        if (familyVi.Length == 0 && detailVi.Length == 0)
            return GuestLocalizedText.Of(
                "Tìm theo sở thích của bạn trong menu.",
                "Browse the menu for what suits you.");

        if (detailVi.Length > 0 && familyVi.Length > 0)
            return GuestLocalizedText.Of(
                $"{Capitalize(familyVi)} · {detailVi} — tìm trong menu theo hướng đó.",
                $"{Capitalize(familyEn)} · {detailEn} — finding that direction on the menu.");

        var primaryVi = familyVi.Length > 0 ? familyVi : detailVi;
        var primaryEn = familyEn.Length > 0 ? familyEn : detailEn;
        return GuestLocalizedText.Of(
            $"{Capitalize(primaryVi)} — tìm trong menu theo hướng đó.",
            $"{Capitalize(primaryEn)} — finding that direction on the menu.");
    }

    private static string FragVi(GuidedOptionSeed? o) =>
        o is null ? "" : (o.EmotionalFragment ?? "").Trim();

    private static string FragEn(GuidedOptionSeed? o)
    {
        if (o is null) return "";
        return string.IsNullOrWhiteSpace(o.EmotionalFragmentEn)
            ? FragVi(o)
            : o.EmotionalFragmentEn.Trim();
    }

    private static string LabelVi(GuidedOptionSeed? o) =>
        o is null ? "" : (o.Label ?? "").Trim();

    private static string LabelEn(GuidedOptionSeed? o)
    {
        if (o is null) return "";
        return string.IsNullOrWhiteSpace(o.LabelEn) ? LabelVi(o) : o.LabelEn.Trim();
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : string.Concat(s[..1].ToUpperInvariant(), s[1..]);

    private static GuestLocalizedText ExplainForRank(
        int rank,
        int pct,
        string drinkName,
        GuestLocalizedText ambient,
        GuestLocalizedText categoryDirectionLine,
        string? tastingNotes = null)
    {
        _ = pct;
        var name = string.IsNullOrWhiteSpace(drinkName) ? "this cup" : drinkName.Trim();
        var nameVi = string.IsNullOrWhiteSpace(drinkName) ? "Ly này" : drinkName.Trim();
        var dirVi = (categoryDirectionLine.Vi ?? "").Trim().TrimEnd('—', ' ', '-');
        var dirEn = (categoryDirectionLine.En ?? "").Trim().TrimEnd('—', ' ', '-');
        var taste = string.IsNullOrWhiteSpace(tastingNotes) ? "" : tastingNotes.Trim().TrimEnd('.');
        if (rank == 0)
        {
            if (dirVi.Length > 0 && taste.Length > 0)
                return GuestLocalizedText.Of(
                    $"{dirVi} {nameVi} sẽ hợp với buổi hôm nay: {taste}.",
                    $"{dirEn} {name} will suit today: {taste}.");
            if (dirVi.Length > 0)
                return GuestLocalizedText.Of(
                    $"{dirVi} {nameVi} là lựa chọn Annap gửi cho bạn hôm nay.",
                    $"{dirEn} {name} is the cup Annap sends for you today.");
            if (!string.IsNullOrWhiteSpace(ambient.Vi))
                return GuestLocalizedText.Of(
                    $"{ambient.Vi.Trim()} {nameVi} là lựa chọn Annap gửi cho bạn hôm nay.",
                    $"{ambient.En.Trim()} {name} is the cup Annap sends for you today.");
            if (taste.Length > 0)
                return GuestLocalizedText.Of(
                    $"Annap chọn {nameVi} cho gu hôm nay của bạn — {taste}.",
                    $"Annap chose {name} for today's palate — {taste}.");
            return GuestLocalizedText.Of(
                $"Annap chọn {nameVi} cho gu hôm nay của bạn.",
                $"Annap chose {name} for today's palate.");
        }

        if (rank == 1)
            return GuestLocalizedText.Of(
                $"{nameVi} cũng là một hướng khác đáng thử.",
                $"{name} is another direction worth tasting.");

        return GuestLocalizedText.Of(
            $"{nameVi} là một lựa chọn dịu hơn nếu bạn muốn đổi nhịp.",
            $"{name} is a softer choice if you want to change pace.");
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
        string? found = null;
        foreach (var opt in selectedAnswers)
        {
            var key = BeverageFamilyGrounding.ResolveFamilyKey(opt.CategoryIntentKey);
            if (key is not null)
                found = key;
        }

        if (found is not null)
            return found;

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

    public static GuestLocalizedText ComposeCategoryDirectionLine(
        string? familyKey,
        IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var family = BeverageFamilyGrounding.NormalizeFamilyKey(familyKey);
        if (family is null)
            return GuestLocalizedText.Of("", "");

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
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly rõ vị và đủ ngọt,",
                    "Because you want a clear cup with enough sweetness,"),
            BeverageFamilyGrounding.Coffee when cafLevel >= 4 =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly rõ vị và tỉnh táo,",
                    "Because you want a clear, wakeful cup,"),
            BeverageFamilyGrounding.Coffee when cafLevel <= 2 && isLow =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly cà phê nhẹ và ít ngọt,",
                    "Because you want a light coffee with restrained sweetness,"),
            BeverageFamilyGrounding.Coffee when cafLevel <= 2 =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly cà phê êm hơn,",
                    "Because you want a gentler coffee,"),
            BeverageFamilyGrounding.Coffee when isLow =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn cà phê ít ngọt,",
                    "Because you want coffee with less sweetness,"),
            BeverageFamilyGrounding.Coffee when isSweet =>
                GuestLocalizedText.Of(
                    "Vì bạn thích vị cà phê mềm hơn,",
                    "Because you prefer a softer coffee tone,"),
            BeverageFamilyGrounding.Coffee =>
                GuestLocalizedText.Of(
                    "Vì bạn đang nghiêng về cà phê,",
                    "Because you're leaning toward coffee,"),
            BeverageFamilyGrounding.Espresso =>
                GuestLocalizedText.Of(
                    "Vì bạn chọn espresso,",
                    "Because you chose espresso,"),
            BeverageFamilyGrounding.ColdBrew =>
                GuestLocalizedText.Of(
                    "Vì bạn chọn cold brew,",
                    "Because you chose cold brew,"),
            BeverageFamilyGrounding.Vietnamese =>
                GuestLocalizedText.Of(
                    "Vì bạn chọn cà phê Việt,",
                    "Because you chose Vietnamese coffee,"),
            BeverageFamilyGrounding.Tea =>
                GuestLocalizedText.Of(
                    "Vì bạn chọn trà,",
                    "Because you chose tea,"),
            BeverageFamilyGrounding.Juice when isLow =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly mát và ít ngọt,",
                    "Because you want something cool with restrained sweetness,"),
            BeverageFamilyGrounding.Juice =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly mát và dễ uống,",
                    "Because you want something cool and easy to sip,"),
            BeverageFamilyGrounding.Smoothie when isLow =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn sinh tố ít ngọt,",
                    "Because you want a smoothie with less sweetness,"),
            BeverageFamilyGrounding.Smoothie =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly mềm và dễ uống,",
                    "Because you want something soft and easy to sip,"),
            BeverageFamilyGrounding.Matcha =>
                GuestLocalizedText.Of(
                    "Vì bạn chọn matcha,",
                    "Because you chose matcha,"),
            BeverageFamilyGrounding.Fruit when isLow =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn trái cây nhẹ và ít ngọt,",
                    "Because you want fruit that is light and less sweet,"),
            BeverageFamilyGrounding.Fruit =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn một ly mát và dễ uống,",
                    "Because you want something cool and easy to sip,"),
            BeverageFamilyGrounding.Signature =>
                GuestLocalizedText.Of(
                    "Vì bạn muốn thử signature của quán,",
                    "Because you want to try the house signature,"),
            _ => GuestLocalizedText.Of("", "")
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
