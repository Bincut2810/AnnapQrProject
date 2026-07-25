using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeContentTests
{
    [Theory]
    [InlineData("q_sp_body_light", "q_sp_flavor_floral", "q_sp_explore_linear", "Kinini Village — Dufatanye")]
    [InlineData("q_sp_body_balanced", "q_sp_flavor_fruit", "q_sp_explore_juicy", "Kinini Village — Abateranankunga")]
    [InlineData("q_sp_body_bold", "q_sp_flavor_chocolate", "q_sp_explore_deepening", "Rift Valley Coffee Caucus")]
    [InlineData("q_sp_body_balanced", "q_sp_flavor_berry", "q_sp_explore_layered", "Nigussie Nare — Murago Outgrowers")]
    public void Specialty_flavor_ranking_favors_expected_origin(
        string bodyOptionId,
        string flavorOptionId,
        string exploreOptionId,
        string expectedName)
    {
        var rows = SpecialtyRows();
        var resolved = ResolveSpecialtyAnswers(bodyOptionId, flavorOptionId, exploreOptionId);

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
    }

    [Theory]
    [InlineData("q_sp_body_light", "q_sp_flavor_floral", "Kinini Village — Dufatanye")]
    [InlineData("q_sp_body_balanced", "q_sp_flavor_fruit", "Kinini Village — Abateranankunga")]
    [InlineData("q_sp_body_bold", "q_sp_flavor_chocolate", "Rift Valley Coffee Caucus")]
    public void Specialty_body_secondary_signal_aligns_with_flavor(
        string bodyOptionId,
        string flavorOptionId,
        string expectedName)
    {
        var rows = SpecialtyRows();
        var resolved = ResolveSpecialtyAnswers(bodyOptionId, flavorOptionId, "q_sp_explore_linear");

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
    }

    [Theory]
    [InlineData("q_sp_explore_linear", "Kinini Village — Dufatanye")]
    [InlineData("q_sp_explore_juicy", "Kinini Village — Abateranankunga")]
    [InlineData("q_sp_explore_deepening", "Rift Valley Coffee Caucus")]
    [InlineData("q_sp_explore_layered", "Nigussie Nare — Murago Outgrowers")]
    public void Specialty_explore_option_changes_isolated_leader(string exploreOptionId, string expectedName)
    {
        var rows = SpecialtyRows();
        var resolved = exploreOptionId switch
        {
            "q_sp_explore_linear" => ResolveSpecialtyAnswers("q_sp_body_light", "q_sp_flavor_floral", exploreOptionId),
            "q_sp_explore_juicy" => ResolveSpecialtyAnswers("q_sp_body_balanced", "q_sp_flavor_fruit", exploreOptionId),
            "q_sp_explore_deepening" => ResolveSpecialtyAnswers("q_sp_body_bold", "q_sp_flavor_chocolate", exploreOptionId),
            _ => ResolveSpecialtyAnswers("q_sp_body_balanced", "q_sp_flavor_berry", exploreOptionId)
        };

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
    }

    [Fact]
    public void Specialty_naming_line_explains_why_in_plain_language()
    {
        var resolved = ResolveSpecialtyAnswers(
            "q_sp_body_light",
            "q_sp_flavor_floral",
            "q_sp_explore_linear");
        var line = GuidedSommelierRecommendationEngine.ComposeSpecialtyNamingLine(resolved);

        Assert.Contains("nhẹ", line.Vi, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acidity", line.En, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("adventurous", line.En, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Specialty_catalog_has_four_protected_keys()
    {
        Assert.Equal(4, AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys.Length);
        Assert.Contains(AnnapSpecialtyCoffeeCatalog.DufatanyeKey, AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys);
        Assert.Contains(AnnapSpecialtyCoffeeCatalog.AbateranankungaKey, AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys);
        Assert.Contains(AnnapSpecialtyCoffeeCatalog.RiftValleyKey, AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys);
        Assert.Contains(AnnapSpecialtyCoffeeCatalog.NigussieKey, AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys);
    }

    [Fact]
    public void Specialty_branch_has_exactly_three_questions()
    {
        Assert.Equal(
            ["q_sp_body", "q_sp_flavor", "q_sp_explore"],
            GuidedSommelierCatalog.Branches[GuidedSommelierCatalog.BranchSpecialty]);

        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
        var json = GuidedSommelierExperienceCatalog.ToClientJson(merged, GuidedSommelierCatalog.QuestionSetId);

        Assert.Contains("q_sp_body", json, StringComparison.Ordinal);
        Assert.Contains("q_sp_flavor", json, StringComparison.Ordinal);
        Assert.Contains("q_sp_explore", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sp_feel", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sp_finish", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sp_format", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sp_tried", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sc_flavor", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Specialty_compare_two_is_disabled()
    {
        var resolved = ResolveSpecialtyAnswers(
            "q_sp_body_light",
            "q_sp_flavor_floral",
            "q_sp_explore_linear");
        Assert.False(GuidedSommelierRecommendationEngine.WantsCompareTwo(resolved));
    }

    private static IReadOnlyList<GuidedOptionSeed> ResolveSpecialtyAnswers(
        string bodyOptionId,
        string flavorOptionId,
        string exploreOptionId)
    {
        var questions = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
        var ids = new[]
        {
            GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId,
            bodyOptionId,
            flavorOptionId,
            exploreOptionId
        };
        GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
            questions,
            ids,
            out var resolved,
            out _);
        return resolved;
    }

    private static IReadOnlyList<MenuItemScoringRow> SpecialtyRows() =>
    [
        Row("Kinini Village — Dufatanye", new DrinkSensoryProfile
        {
            Body = "tea_like",
            Acidity = "quiet",
            AromaFamily = "floral",
            Energy = "still",
            Finish = "clean",
            CaffeineIntensity = 2
        }),
        Row("Kinini Village — Abateranankunga", new DrinkSensoryProfile
        {
            Body = "round",
            Acidity = "balanced",
            AromaFamily = "stone_fruit",
            Energy = "playful",
            Finish = "clean",
            CaffeineIntensity = 2
        }),
        Row("Rift Valley Coffee Caucus", new DrinkSensoryProfile
        {
            Body = "syrupy",
            Acidity = "lifted",
            AromaFamily = "cocoa",
            Energy = "intense",
            Finish = "linger",
            CaffeineIntensity = 4
        }),
        Row("Nigussie Nare — Murago Outgrowers", new DrinkSensoryProfile
        {
            Body = "round",
            Acidity = "crystalline",
            AromaFamily = "floral",
            Energy = "focused",
            Finish = "linger",
            CaffeineIntensity = 3
        })
    ];

    private static MenuItemScoringRow Row(string name, DrinkSensoryProfile sensory) =>
        new(
            Guid.NewGuid(),
            name,
            80000m,
            "notes",
            "story",
            AnnapSpecialtyCoffeeBootstrap.FallbackImageUrl,
            "mood",
            sensory,
            AnnapSpecialtyCoffeeCatalog.CategoryName);
}
