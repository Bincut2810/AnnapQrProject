using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Tests;

public class SpecialtyCoffeeShortcutTests
{
    private static IReadOnlyList<GuidedQuestionSeed> AllQuestions() =>
        GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

    private static string[] SpecialtyDiscoveryIds(
        string moodOptionId = "q1_light",
        string flavorOptionId = "q_sc_flavor_floral",
        string experienceOptionId = "q_sc_experience_soft") =>
        [moodOptionId, GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId, flavorOptionId, experienceOptionId];

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
    public void HasCompleteSpecialtyDiscovery_true_when_four_specialty_answers()
    {
        var ids = SpecialtyDiscoveryIds();

        Assert.True(GuidedSommelierExperienceCatalog.HasCompleteSpecialtyDiscovery(ids));
    }

    [Fact]
    public void HasCompleteSpecialtyDiscovery_false_when_only_mood_and_coffee()
    {
        var ids = new[] { "q1_light", GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId };

        Assert.False(GuidedSommelierExperienceCatalog.HasCompleteSpecialtyDiscovery(ids));
    }

    [Fact]
    public void TryResolveSommelierAnswers_succeeds_with_complete_specialty_discovery()
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
        Assert.Equal(4, resolved.Count);
        Assert.Equal("q1_light", resolved[0].OptionId);
        Assert.Equal(GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId, resolved[1].OptionId);
        Assert.Equal("q_sc_flavor_floral", resolved[2].OptionId);
        Assert.Equal("q_sc_experience_soft", resolved[3].OptionId);
        Assert.Equal("sc_flavor:floral", resolved[2].RefinementKey);
        Assert.Equal("sc_experience:soft", resolved[3].RefinementKey);
    }

    [Fact]
    public void TryResolveSommelierAnswers_fails_when_coffee_without_calibration()
    {
        var questions = AllQuestions();
        var ids = new[] { "q1_light", GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId };

        Assert.False(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                questions,
                ids,
                out _,
                out var error));
        Assert.NotNull(error);
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
    public void Specialty_signature_filter_keeps_coffee_pool_when_no_signature_coffees_exist()
    {
        var coffeePool = new[]
        {
            ("Espresso", false),
            ("Cold Brew", false),
            ("Signature", true)
        };

        var familyKey = BeverageFamilyGrounding.Coffee;
        var filtered = coffeePool
            .Where(p => BeverageFamilyGrounding.Matches(familyKey, p.Item1, "item"))
            .ToList();
        var signatureOnly = filtered.Where(p => p.Item2).ToList();
        if (signatureOnly.Count > 0)
            filtered = signatureOnly;

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, p => Assert.False(p.Item2));
    }

    [Fact]
    public void Specialty_verification_pipeline_end_to_end()
    {
        Assert.True(
            GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(
                AllQuestions(),
                SpecialtyDiscoveryIds(),
                out var resolved,
                out _));

        var mood = resolved[0];
        var family = resolved[1];
        var familyKey = GuidedSommelierRecommendationEngine.ExtractBeverageFamilyKey(resolved);
        Assert.Equal(BeverageFamilyGrounding.Coffee, familyKey);

        var raw = CoffeeVerificationRows();
        var familyFiltered = raw
            .Where(m => BeverageFamilyGrounding.Matches(familyKey, m.CategoryName, m.Row.Name))
            .ToList();
        var signatureCandidates = familyFiltered.Count(m => m.IsSignature);
        var pool = familyFiltered;
        if (GuidedSommelierRecommendationEngine.IsSpecialtyCoffeePath(resolved))
        {
            var signatureOnly = pool.Where(m => m.IsSignature).ToList();
            if (signatureOnly.Count > 0)
                pool = signatureOnly;
        }

        var hints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var ranked = GuidedSommelierRecommendationEngine.Rank(
            hints,
            resolved,
            pool.Select(m => m.Row).ToList(),
            take: 1);

        Assert.Equal(33, raw.Count);
        Assert.Equal(15, familyFiltered.Count);
        Assert.Equal(0, signatureCandidates);
        Assert.Equal(15, pool.Count);
        Assert.Single(ranked);

        var winner = raw.First(m => m.Row.Id == ranked[0].MenuItemId);
        Assert.Equal("Nhẹ và không vội", mood.Label);
        Assert.Equal(GuidedSommelierExperienceCatalog.SpecialtyCoffeeOptionId, family.OptionId);
        Assert.Contains("Cà phê", family.Label);
        Assert.False(winner.IsSignature);
        Assert.False(string.IsNullOrWhiteSpace(winner.Origin));
    }

    private static IReadOnlyList<VerificationMenuRow> CoffeeVerificationRows() =>
    [
        V("Espresso", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "tea_like", Energy = "intense", CaffeineIntensity = 5 }),
        V("Americano", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "tea_like", Energy = "focused", CaffeineIntensity = 5 }),
        V("Latte", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "round", Texture = "velvet", Sweetness = "gentle", Energy = "still", CaffeineIntensity = 2 }),
        V("Capuchino", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "round", Texture = "satin", Sweetness = "rounded", Energy = "focused", CaffeineIntensity = 3 }),
        V("Salted Caramel Latte", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Energy = "still", CaffeineIntensity = 2 }),
        V("Mocha", "Espresso", "Brazil", false, new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Energy = "still", CaffeineIntensity = 3 }),
        V("Hibicus Tea", "Tea", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Trà Dâu Tằm", "Tea", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Matcha Muối", "Tea", "IMO Matcha", false, new DrinkSensoryProfile { Energy = "focused" }),
        V("Sinh Tố Xoài", "Smoothie", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Sinh Tố Bơ", "Smoothie", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Sinh Tố Dâu", "Smoothie", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Sinh Tố Dâu Chuối", "Smoothie", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Daugurt Kefir", "Smoothie", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Nước Ép Cam", "Juice", "Bình Dương", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Nước Ép Thơm", "Juice", "Nghệ An", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Nước Ép Chanh Vàng", "Juice", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Nước Ép Táo Xanh", "Juice", "Mỹ", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Nước Ép Dưa Hấu", "Juice", "", false, new DrinkSensoryProfile { Energy = "still" }),
        V("Cold Brew", "Cold Brew", "Brazil", false, new DrinkSensoryProfile { Body = "round", Energy = "focused", CaffeineIntensity = 4 }),
        V("Cold Brew Cam", "Cold Brew", "Brazil", false, new DrinkSensoryProfile { Body = "round", Sweetness = "rounded", Energy = "lifted", CaffeineIntensity = 4 }),
        V("Cold Brew Táo", "Cold Brew", "Brazil", false, new DrinkSensoryProfile { Body = "round", Energy = "playful", CaffeineIntensity = 4 }),
        V("Cold Brew Dâu Tằm", "Cold Brew", "Brazil", false, new DrinkSensoryProfile { Body = "round", Energy = "still", CaffeineIntensity = 4 }),
        V("Cà Phê Đen", "Vietnamese Coffee", "Gia Lai", false, new DrinkSensoryProfile { Body = "round", Energy = "intense", CaffeineIntensity = 5 }),
        V("Cà Phê Sữa", "Vietnamese Coffee", "Gia Lai", false, new DrinkSensoryProfile { Body = "round", Sweetness = "luscious", Energy = "still", CaffeineIntensity = 3 }),
        V("Đen Đá Sài Gòn", "Vietnamese Coffee", "Gia Lai", false, new DrinkSensoryProfile { Body = "round", Energy = "intense", CaffeineIntensity = 5 }),
        V("Sữa Đá Sài Gòn", "Vietnamese Coffee", "Gia Lai", false, new DrinkSensoryProfile { Body = "round", Sweetness = "luscious", Energy = "still", CaffeineIntensity = 3 }),
        V("Bạc Xỉu", "Vietnamese Coffee", "Gia Lai", false, new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Energy = "still", CaffeineIntensity = 1 }),
        V("Coco Bơ", "Signature", "", true, new DrinkSensoryProfile { Energy = "still" }),
        V("Three Kick", "Signature", "", true, new DrinkSensoryProfile { Energy = "still" }),
        V("Ginger Singer", "Signature", "Mỹ", true, new DrinkSensoryProfile { Energy = "still" }),
        V("Sunrise", "Signature", "", true, new DrinkSensoryProfile { Energy = "still" }),
        V("Dưa Fame", "Signature", "Nghệ An", true, new DrinkSensoryProfile { Energy = "still" })
    ];

    private sealed record VerificationMenuRow(MenuItemScoringRow Row, string CategoryName, string Origin, bool IsSignature);

    private static VerificationMenuRow V(
        string name,
        string category,
        string origin,
        bool isSignature,
        DrinkSensoryProfile sensory)
    {
        var id = Guid.NewGuid();
        return new VerificationMenuRow(
            new MenuItemScoringRow(id, name, 45000m, "notes", null, "/img.svg", "mood", sensory, category),
            category,
            origin,
            isSignature);
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
