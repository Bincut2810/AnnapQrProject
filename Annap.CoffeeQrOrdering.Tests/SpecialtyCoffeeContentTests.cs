using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeContentTests
{
    [Theory]
    [InlineData("q_sp_profile_floral", "Kinini Village — Dufatanye")]
    [InlineData("q_sp_profile_fruit", "Kinini Village — Abateranankunga")]
    [InlineData("q_sp_profile_chocolate", "Rift Valley Coffee Caucus")]
    [InlineData("q_sp_profile_surprise", "Nigussie Nare — Murago Outgrowers")]
    public void Specialty_flavor_archetype_ranking_favors_expected_origin(string flavorOptionId, string expectedName)
    {
        var rows = SpecialtyRows();
        var resolved = ResolveSpecialtyAnswers("q_sp_tried_first", flavorOptionId, "q_sp_adventure_balanced");

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
    }

    [Theory]
    [InlineData("calm", "q_sp_profile_floral", "Kinini Village — Dufatanye")]
    [InlineData("bright", "q_sp_profile_fruit", "Kinini Village — Abateranankunga")]
    [InlineData("adventurous", "q_sp_profile_chocolate", "Rift Valley Coffee Caucus")]
    public void Specialty_tried_mood_secondary_signal_aligns_with_profile(
        string moodKey,
        string flavorOptionId,
        string expectedName)
    {
        var rows = SpecialtyRows();
        var triedOptionId = moodKey switch
        {
            "calm" => "q_sp_tried_first",
            "bright" => "q_sp_tried_occasional",
            "adventurous" => "q_sp_tried_regular",
            _ => "q_sp_tried_first"
        };
        var resolved = ResolveSpecialtyAnswers(triedOptionId, flavorOptionId, "q_sp_adventure_balanced");

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
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
    public void Specialty_client_catalog_includes_branch_tree()
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
        var json = GuidedSommelierExperienceCatalog.ToClientJson(merged, GuidedSommelierCatalog.QuestionSetId);

        Assert.Contains("atelier_v6", json, StringComparison.Ordinal);
        Assert.Contains("q0", json, StringComparison.Ordinal);
        Assert.Contains("q_sp_profile", json, StringComparison.Ordinal);
        Assert.Contains("q_sp_adventure", json, StringComparison.Ordinal);
        Assert.Contains("\"specialty\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("q_sc_flavor", json, StringComparison.Ordinal);
    }

    private static IReadOnlyList<GuidedOptionSeed> ResolveSpecialtyAnswers(
        string triedOptionId,
        string flavorOptionId,
        string experienceOptionId)
    {
        var questions = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
        var ids = new[]
        {
            GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId,
            triedOptionId,
            flavorOptionId,
            experienceOptionId,
            "q_sp_format_one"
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
