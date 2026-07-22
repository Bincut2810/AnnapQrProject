using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeShortcutTests
{
    private static IReadOnlyList<GuidedQuestionSeed> AllQuestions() =>
        GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);

    private static string[] SpecialtyDiscoveryIds(
        string triedOptionId = "q_sp_tried_first",
        string flavorOptionId = "q_sp_profile_floral",
        string experienceOptionId = "q_sp_adventure_safe",
        string formatOptionId = "q_sp_format_one") =>
    [
        GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId,
        triedOptionId,
        flavorOptionId,
        experienceOptionId,
        formatOptionId
    ];

    [Fact]
    public void ExpandSpecialtyCoffeeShortcut_is_noop_and_preserves_ids()
    {
        var questions = AllQuestions();
        var ids = SpecialtyDiscoveryIds();

        var expansion = GuidedSommelierExperienceCatalog.ExpandSpecialtyCoffeeShortcut(questions, ids);

        Assert.False(expansion.Applied);
        Assert.Empty(expansion.InjectedDefaults);
        Assert.Equal(ids, expansion.OptionIds);
    }

    [Fact]
    public void HasCompleteSpecialtyDiscovery_true_when_full_specialty_branch()
    {
        var ids = SpecialtyDiscoveryIds();

        Assert.True(GuidedSommelierExperienceCatalog.HasCompleteSpecialtyDiscovery(ids));
    }

    [Fact]
    public void HasCompleteSpecialtyDiscovery_false_when_only_entry()
    {
        var ids = new[] { GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId };

        Assert.False(GuidedSommelierExperienceCatalog.HasCompleteSpecialtyDiscovery(ids));
    }

    [Fact]
    public void TryResolveSommelierAnswers_succeeds_with_complete_specialty_branch()
    {
        var questions = AllQuestions();
        var ids = SpecialtyDiscoveryIds();

        Assert.True(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                questions,
                ids,
                out var resolved,
                out var error));
        Assert.Null(error);
        Assert.Equal(5, resolved.Count);
        Assert.Equal(GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId, resolved[0].OptionId);
        Assert.Equal("q_sp_tried_first", resolved[1].OptionId);
        Assert.Equal("q_sp_profile_floral", resolved[2].OptionId);
        Assert.Equal("q_sp_adventure_safe", resolved[3].OptionId);
        Assert.Equal("q_sp_format_one", resolved[4].OptionId);
        Assert.Equal("sc_flavor:floral", resolved[2].RefinementKey);
        Assert.Equal("sc_experience:soft", resolved[3].RefinementKey);
    }

    [Fact]
    public void TryResolveSommelierAnswers_fails_when_specialty_incomplete()
    {
        var questions = AllQuestions();
        var ids = new[] { GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId };

        Assert.False(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                questions,
                ids,
                out _,
                out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Tea_branch_maps_directly_to_menu_teas_without_fake_matcha_pure()
    {
        var teaPath = GuidedSommelierCatalog.QuestionsForBranch(GuidedSommelierCatalog.BranchTea);
        var optionIds = teaPath.SelectMany(q => q.Options).Select(o => o.OptionId).ToList();

        Assert.DoesNotContain(optionIds, id => id.Contains("caffeine", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(optionIds, id => id.Contains("q_ma_style_pure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(optionIds, id => id.StartsWith("q_te_pick_", StringComparison.Ordinal));
        Assert.Contains("q_te_pick_matcha", optionIds);
        Assert.Contains("q_te_pick_mulberry", optionIds);
    }

    [Fact]
    public void Specialty_rank_returns_one_coffee_when_no_signature_items_exist()
    {
        var latteId = Guid.NewGuid();
        var espressoId = Guid.NewGuid();
        var rows = new[]
        {
            Row(latteId, "Latte", "Espresso", new DrinkSensoryProfile { Body = "round", Energy = "focused" }),
            Row(espressoId, "Espresso", "Espresso", new DrinkSensoryProfile { Body = "tea_like", Energy = "intense", CaffeineIntensity = 5 })
        };

        Assert.True(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                AllQuestions(),
                SpecialtyDiscoveryIds(),
                out var resolved,
                out _));

        var familyKey = GuidedSommelierRecommendationEngine.ExtractBeverageFamilyKey(resolved);
        var filtered = rows
            .Where(r => BeverageFamilyGrounding.Matches(familyKey, r.CategoryName, r.Name))
            .ToList();

        Assert.Equal(2, filtered.Count);
        Assert.True(GuidedSommelierRecommendationEngine.IsSpecialtyCoffeePath(resolved));

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, filtered, take: 1);

        Assert.Single(ranked);
        Assert.Contains(ranked[0].MenuItemId, new[] { latteId, espressoId });
    }

    [Fact]
    public void Classic_espresso_branch_excludes_specialty_category()
    {
        Assert.True(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                AllQuestions(),
                ["q0_espresso", "q_es_body_milk", "q_es_detail_classic"],
                out var resolved,
                out _));

        Assert.True(GuidedSommelierRecommendationEngine.IsClassicCoffeePath(resolved));
        Assert.False(GuidedSommelierRecommendationEngine.IsSpecialtyCoffeePath(resolved));

        var rows = new[]
        {
            Row(Guid.NewGuid(), "Latte", "Espresso", new DrinkSensoryProfile { Body = "round" }),
            Row(Guid.NewGuid(), "Kinini Village — Dufatanye", "Specialty Coffee", new DrinkSensoryProfile { AromaFamily = "floral" })
        };
        var filtered = GuidedSommelierRecommendationEngine.ApplyClassicCoffeeFilter(
            GuidedSommelierRecommendationEngine.ApplyFamilyLock(rows, BeverageFamilyGrounding.Espresso));

        Assert.Single(filtered);
        Assert.Equal("Latte", filtered[0].Name);
    }

    [Fact]
    public void Specialty_compare_two_flag_detected()
    {
        Assert.True(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                AllQuestions(),
                SpecialtyDiscoveryIds(formatOptionId: "q_sp_format_compare"),
                out var resolved,
                out _));

        Assert.True(GuidedSommelierRecommendationEngine.WantsCompareTwo(resolved));
    }

    private static MenuItemScoringRow Row(
        Guid id,
        string name,
        string category,
        DrinkSensoryProfile sensory) =>
        new(
            id,
            name,
            45000m,
            "notes",
            null,
            "/images/menu-fallback.svg",
            "mood",
            sensory,
            category);
}
