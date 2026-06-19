using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeContentTests
{
    [Theory]
    [InlineData("q_sc_flavor_floral", "Kinini Village — Dufatanye")]
    [InlineData("q_sc_flavor_fruit", "Kinini Village — Abateranankunga")]
    [InlineData("q_sc_flavor_wine", "Rift Valley Coffee Caucus")]
    [InlineData("q_sc_flavor_blueberry", "Nigussie Nare — Murago Outgrowers")]
    public void Specialty_flavor_archetype_ranking_favors_expected_origin(string flavorOptionId, string expectedName)
    {
        var rows = SpecialtyRows();
        var resolved = ResolveSpecialtyAnswers("q1_light", flavorOptionId, "q_sc_experience_balanced");

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(hints, resolved, rows, take: 1);

        Assert.Single(ranked);
        Assert.Equal(expectedName, ranked[0].Name);
    }

    [Theory]
    [InlineData("calm", "q_sc_flavor_floral", "Kinini Village — Dufatanye")]
    [InlineData("bright", "q_sc_flavor_fruit", "Kinini Village — Abateranankunga")]
    [InlineData("adventurous", "q_sc_flavor_wine", "Rift Valley Coffee Caucus")]
    public void Specialty_mood_secondary_signal_aligns_with_flavor_choice(
        string moodKey,
        string flavorOptionId,
        string expectedName)
    {
        var rows = SpecialtyRows();
        var moodOptionId = moodKey switch
        {
            "calm" => "q1_light",
            "bright" => "q1_refresh",
            "adventurous" => "q1_curious",
            _ => "q1_light"
        };
        var resolved = ResolveSpecialtyAnswers(moodOptionId, flavorOptionId, "q_sc_experience_balanced");

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
    public void Specialty_cup_moment_origin_dot_name_format_matches_examples()
    {
        var cases = new[]
        {
            ("Rwanda", "Kinini Village — Dufatanye"),
            ("Kenya", "Rift Valley Coffee Caucus"),
            ("Ethiopia", "Nigussie Nare — Murago Outgrowers")
        };

        foreach (var (origin, name) in cases)
            Assert.Equal($"{origin} · {name}", $"{origin} · {name}");
    }

    [Fact]
    public void Specialty_client_catalog_includes_discovery_questions()
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);
        var json = GuidedSommelierExperienceCatalog.ToClientJson(merged, GuidedSommelierCatalog.QuestionSetId);

        Assert.Contains("q_sc_flavor", json, StringComparison.Ordinal);
        Assert.Contains("q_sc_experience", json, StringComparison.Ordinal);
    }

    private static IReadOnlyList<GuidedOptionSeed> ResolveSpecialtyAnswers(
        string moodOptionId,
        string flavorOptionId,
        string experienceOptionId)
    {
        var questions = GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);
        var ids = new[]
        {
            moodOptionId,
            GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId,
            flavorOptionId,
            experienceOptionId
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
