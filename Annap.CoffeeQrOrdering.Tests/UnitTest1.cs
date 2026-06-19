using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;

namespace Annap.CoffeeQrOrdering.Tests;

public class SommelierBeverageFamilyGroundingTests
{
    [Theory]
    [InlineData(BeverageFamilyGrounding.Coffee, "Espresso", "Latte", true)]
    [InlineData(BeverageFamilyGrounding.Coffee, "Cold Brew", "Cold Brew Táo", true)]
    [InlineData(BeverageFamilyGrounding.Coffee, "Vietnamese Coffee", "Bạc Xỉu", true)]
    [InlineData(BeverageFamilyGrounding.Coffee, "Smoothie", "Sinh Tố Bơ", false)]
    [InlineData(BeverageFamilyGrounding.Coffee, "Juice", "Nước Ép Cam", false)]
    [InlineData(BeverageFamilyGrounding.Tea, "Tea", "Hibicus Tea", true)]
    [InlineData(BeverageFamilyGrounding.Tea, "Espresso", "Americano", false)]
    [InlineData(BeverageFamilyGrounding.Tea, "Tea", "Matcha Muối", false)]
    [InlineData(BeverageFamilyGrounding.Matcha, "Tea", "Matcha Muối", true)]
    [InlineData(BeverageFamilyGrounding.Matcha, "Smoothie", "Sinh Tố Bơ", false)]
    [InlineData(BeverageFamilyGrounding.Juice, "Juice", "Nước Ép Cam", true)]
    [InlineData(BeverageFamilyGrounding.Juice, "Smoothie", "Sinh Tố Xoài", false)]
    [InlineData(BeverageFamilyGrounding.Smoothie, "Smoothie", "Sinh Tố Bơ", true)]
    [InlineData(BeverageFamilyGrounding.Smoothie, "Juice", "Nước Ép Táo Xanh", false)]
    public void Family_matcher_enforces_hard_boundaries(
        string family,
        string category,
        string name,
        bool expected)
    {
        Assert.Equal(expected, BeverageFamilyGrounding.Matches(family, category, name));
    }

    [Fact]
    public void Guided_rank_never_returns_smoothie_for_coffee_choice()
    {
        var coffeeId = Guid.NewGuid();
        var smoothieId = Guid.NewGuid();
        var rows = new[]
        {
            Row(coffeeId, "Latte", "Espresso", new DrinkSensoryProfile { Body = "round", Energy = "focused" }),
            Row(smoothieId, "Sinh Tố Bơ", "Smoothie", new DrinkSensoryProfile { Body = "round", Texture = "velvet", Energy = "still" })
        };
        var selected = new[]
        {
            new GuidedOptionSeed(
                "q2_coffee",
                "Cà phê",
                "cà phê",
                new DrinkSensoryProfile { Body = "round", Texture = "velvet", Energy = "still" },
                CategoryIntentKey: BeverageFamilyGrounding.Coffee)
        };

        var ranked = GuidedSommelierRecommendationEngine.Rank(
            new DrinkSensoryProfile { Body = "round", Texture = "velvet", Energy = "still" },
            selected,
            rows,
            take: 5);

        Assert.NotEmpty(ranked);
        Assert.DoesNotContain(ranked, r => r.MenuItemId == smoothieId);
        Assert.All(ranked, r => Assert.True(BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Coffee, rows.First(x => x.Id == r.MenuItemId).CategoryName, r.Name)));
    }

    [Fact]
    public void Guided_rank_keeps_matcha_inside_matcha_family_even_when_smoothie_scores_well()
    {
        var matchaId = Guid.NewGuid();
        var smoothieId = Guid.NewGuid();
        var rows = new[]
        {
            Row(matchaId, "Matcha Muối", "Tea", new DrinkSensoryProfile { Texture = "satin", Body = "round", Energy = "focused" }),
            Row(smoothieId, "Sinh Tố Bơ", "Smoothie", new DrinkSensoryProfile { Texture = "satin", Body = "round", Energy = "focused" })
        };
        var selected = new[]
        {
            new GuidedOptionSeed(
                "q2_matcha",
                "Matcha",
                "matcha",
                new DrinkSensoryProfile { Texture = "satin", Body = "round", Energy = "focused" },
                CategoryIntentKey: BeverageFamilyGrounding.Matcha)
        };

        var ranked = GuidedSommelierRecommendationEngine.Rank(
            new DrinkSensoryProfile { Texture = "satin", Body = "round", Energy = "focused" },
            selected,
            rows,
            take: 5);

        Assert.Single(ranked);
        Assert.Equal(matchaId, ranked[0].MenuItemId);
        Assert.DoesNotContain(ranked, r => r.MenuItemId == smoothieId);
    }

    [Fact]
    public void Coffee_focus_strong_caffeine_prioritizes_black_and_cold_brew_over_latte()
    {
        var espressoId = Guid.NewGuid();
        var coldBrewId = Guid.NewGuid();
        var latteId = Guid.NewGuid();
        var bacXiuId = Guid.NewGuid();
        var rows = new[]
        {
            Row(latteId, "Latte", "Espresso", new DrinkSensoryProfile { Body = "round", Texture = "velvet", Energy = "focused", CaffeineIntensity = 3 }),
            Row(bacXiuId, "Bạc Xỉu", "Vietnamese Coffee", new DrinkSensoryProfile { Body = "round", Texture = "velvet", Energy = "still", CaffeineIntensity = 2 }),
            Row(espressoId, "Espresso", "Espresso", new DrinkSensoryProfile { Body = "tea_like", Acidity = "balanced", Finish = "clean", Energy = "intense", CaffeineIntensity = 5 }),
            Row(coldBrewId, "Cold Brew", "Cold Brew", new DrinkSensoryProfile { Body = "round", Acidity = "balanced", Finish = "clean", Energy = "focused", CaffeineIntensity = 4 })
        };
        var selected = new[]
        {
            new GuidedOptionSeed("q1_alert", "Tỉnh táo", "tỉnh táo", new DrinkSensoryProfile { Energy = "focused", CaffeineIntensity = 3 }, MoodKey: "focus"),
            new GuidedOptionSeed("q2_coffee", "Cà phê", "cà phê", new DrinkSensoryProfile { AromaFamily = "cocoa", CaffeineIntensity = 3 }, MoodKey: "focus", CategoryIntentKey: BeverageFamilyGrounding.Coffee),
            new GuidedOptionSeed("q3_medium", "Vừa phải", "ngọt vừa", new DrinkSensoryProfile { Sweetness = "rounded" }),
            new GuidedOptionSeed("q4_strong", "Càng mạnh càng tốt", "caffeine mạnh", new DrinkSensoryProfile { CaffeineIntensity = 5, Energy = "intense" })
        };

        var ranked = GuidedSommelierRecommendationEngine.Rank(
            new DrinkSensoryProfile { Energy = "intense", CaffeineIntensity = 5, Body = "round", Texture = "velvet" },
            selected,
            rows,
            take: 4);

        Assert.Contains(ranked[0].MenuItemId, new[] { espressoId, coldBrewId });
        Assert.True(
            Array.FindIndex(ranked.Select(r => r.MenuItemId).ToArray(), id => id == latteId)
            > Array.FindIndex(ranked.Select(r => r.MenuItemId).ToArray(), id => id == espressoId));
        Assert.True(
            Array.FindIndex(ranked.Select(r => r.MenuItemId).ToArray(), id => id == bacXiuId)
            > Array.FindIndex(ranked.Select(r => r.MenuItemId).ToArray(), id => id == coldBrewId));
    }

    [Fact]
    public void Beverage_intelligence_classifies_latte_as_comfort_not_peak_focus()
    {
        var espresso = BeverageIntelligence.Classify("Espresso", "Espresso", new DrinkSensoryProfile { CaffeineIntensity = 5 });
        var latte = BeverageIntelligence.Classify("Espresso", "Latte", new DrinkSensoryProfile { CaffeineIntensity = 3, Texture = "velvet" });
        var intent = new BeverageIntent(BeverageFamilyGrounding.Coffee, WantsFocus: true, WantsStrongCaffeine: true, WantsLowCaffeine: false, WantsComfort: false, WantsSweetness: false, WantsRefreshing: false);

        Assert.True(espresso.CoffeeForward > latte.CoffeeForward);
        Assert.True(espresso.MilkIntensity < latte.MilkIntensity);
        Assert.True(BeverageIntelligence.SpecialtyScore(espresso, intent) > BeverageIntelligence.SpecialtyScore(latte, intent));
    }

    [Fact]
    public void Coffee_sweet_low_caffeine_prefers_soft_milk_coffee_over_black_coffee()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_light", "Nhẹ nhàng", "nhẹ nhàng", new DrinkSensoryProfile { Energy = "still", Texture = "satin" }, "calm"),
            Sweetness("q3_sweet", "Ngọt nhiều", "ngọt nhiều", new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet", Body = "syrupy" }),
            Caffeine("q4_low", "Ít thôi", "caffeine nhẹ", new DrinkSensoryProfile { CaffeineIntensity = 2, Energy = "still" }));

        var topNames = ranked.Take(4).Select(r => r.Name).ToArray();
        Assert.Contains(topNames[0], new[] { "Bạc Xỉu", "Salted Caramel Latte", "Mocha", "Latte" });
        Assert.DoesNotContain("Espresso", topNames);
        Assert.DoesNotContain("Americano", topNames);
        Assert.DoesNotContain("Cà Phê Đen", topNames);
    }

    [Fact]
    public void Coffee_sweet_medium_caffeine_prefers_cappuccino_latte_and_caramel()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_alert", "Tỉnh táo", "tỉnh táo", new DrinkSensoryProfile { Energy = "focused", CaffeineIntensity = 3 }, "focus"),
            Sweetness("q3_semisweet", "Hơi ngọt", "hơi ngọt", new DrinkSensoryProfile { Sweetness = "gentle", Body = "round" }),
            Caffeine("q4_medium", "Vừa đủ tỉnh", "caffeine vừa", new DrinkSensoryProfile { CaffeineIntensity = 3, Energy = "focused" }));

        var topNames = ranked.Take(4).Select(r => r.Name).ToArray();
        Assert.Contains(topNames[0], new[] { "Cappuccino", "Latte", "Salted Caramel Latte", "Mocha" });
        Assert.True(IndexOf(ranked, "Latte") < IndexOf(ranked, "Espresso"));
        Assert.True(IndexOf(ranked, "Salted Caramel Latte") < IndexOf(ranked, "Cà Phê Đen"));
    }

    [Fact]
    public void Coffee_strong_bitter_keeps_black_and_cold_brew_first()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_alert", "Tỉnh táo", "tỉnh táo", new DrinkSensoryProfile { Energy = "focused", CaffeineIntensity = 3 }, "focus"),
            Sweetness("q3_low", "Ít ngọt", "ít ngọt", new DrinkSensoryProfile { Sweetness = "restrained", Acidity = "balanced" }),
            Caffeine("q4_strong", "Càng mạnh càng tốt", "caffeine mạnh", new DrinkSensoryProfile { CaffeineIntensity = 5, Energy = "intense" }));

        var topNames = ranked.Take(4).Select(r => r.Name).ToArray();
        Assert.All(topNames, name => Assert.Contains(name, new[] { "Espresso", "Americano", "Cà Phê Đen", "Cold Brew" }));
        Assert.True(IndexOf(ranked, "Espresso") < IndexOf(ranked, "Latte"));
    }

    [Fact]
    public void Coffee_creamy_relaxed_prefers_bac_xiu_latte_and_mocha()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_light", "Nhẹ nhàng", "nhẹ nhàng", new DrinkSensoryProfile { Energy = "still", Texture = "velvet" }, "calm"),
            Sweetness("q3_semisweet", "Hơi ngọt", "hơi ngọt", new DrinkSensoryProfile { Sweetness = "gentle", Body = "round", Texture = "velvet" }),
            Caffeine("q4_low", "Ít thôi", "caffeine nhẹ", new DrinkSensoryProfile { CaffeineIntensity = 2, Energy = "still" }));

        var topNames = ranked.Take(3).Select(r => r.Name).ToArray();
        Assert.All(topNames, name => Assert.Contains(name, new[] { "Bạc Xỉu", "Latte", "Mocha", "Salted Caramel Latte" }));
    }

    [Fact]
    public void Coffee_fruity_adventurous_prefers_cold_brew_variants()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_curious", "Muốn gì đó lạ", "thích khám phá", new DrinkSensoryProfile { Energy = "playful", Finish = "linger" }, "adventurous"),
            Sweetness("q3_medium", "Vừa phải", "ngọt vừa", new DrinkSensoryProfile { Sweetness = "rounded", Finish = "clean" }),
            Caffeine("q4_medium", "Vừa đủ tỉnh", "caffeine vừa", new DrinkSensoryProfile { CaffeineIntensity = 3, Energy = "focused" }),
            extraGuestHints: new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Finish = "clean", Energy = "playful", CaffeineIntensity = 3 });

        var topNames = ranked.Take(2).Select(r => r.Name).ToArray();
        Assert.All(topNames, name => Assert.Contains(name, new[] { "Cold Brew Cam", "Cold Brew Táo" }));
    }

    [Fact]
    public void Coffee_dessert_like_prefers_mocha_caramel_and_bac_xiu()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_light", "Nhẹ nhàng", "nhẹ nhàng", new DrinkSensoryProfile { Energy = "still", Texture = "satin" }, "calm"),
            Sweetness("q3_sweet", "Ngọt nhiều", "ngọt nhiều", new DrinkSensoryProfile { Sweetness = "luscious", Body = "syrupy", Texture = "velvet" }),
            Caffeine("q4_medium", "Vừa đủ tỉnh", "caffeine vừa", new DrinkSensoryProfile { CaffeineIntensity = 3, Energy = "focused" }));

        var topNames = ranked.Take(3).Select(r => r.Name).ToArray();
        Assert.All(topNames, name => Assert.Contains(name, new[] { "Mocha", "Salted Caramel Latte", "Bạc Xỉu", "Latte" }));
    }

    [Fact]
    public void Coffee_refreshing_prefers_clean_cold_brew_directions()
    {
        var rows = CoffeeMatrixRows();
        var ranked = RankCoffee(rows,
            Mood("q1_refresh", "Refresh", "tươi mát", new DrinkSensoryProfile { Acidity = "lifted", AromaFamily = "citrus", Energy = "lifted", Finish = "clean" }, "bright"),
            Sweetness("q3_medium", "Vừa phải", "ngọt vừa", new DrinkSensoryProfile { Sweetness = "rounded", Finish = "clean" }),
            Caffeine("q4_medium", "Vừa đủ tỉnh", "caffeine vừa", new DrinkSensoryProfile { CaffeineIntensity = 3, Energy = "focused" }));

        var topNames = ranked.Take(3).Select(r => r.Name).ToArray();
        Assert.Contains("Cold Brew Cam", topNames);
        Assert.Contains("Cold Brew Táo", topNames);
    }

    private static MenuItemScoringRow Row(Guid id, string name, string category, DrinkSensoryProfile sensory) =>
        new(
            id,
            name,
            45000m,
            "test notes",
            "test story",
            "/images/menu-fallback.svg",
            "test mood",
            sensory,
            category);

    private static IReadOnlyList<MenuItemScoringRow> CoffeeMatrixRows() =>
    [
        Row(Guid.NewGuid(), "Espresso", "Espresso", new DrinkSensoryProfile { Body = "tea_like", Texture = "crisp", Sweetness = "dry", Acidity = "balanced", Finish = "clean", Energy = "intense", CaffeineIntensity = 5 }),
        Row(Guid.NewGuid(), "Americano", "Espresso", new DrinkSensoryProfile { Body = "tea_like", Texture = "crisp", Sweetness = "dry", Acidity = "balanced", Finish = "clean", Energy = "focused", CaffeineIntensity = 5 }),
        Row(Guid.NewGuid(), "Cà Phê Đen", "Vietnamese Coffee", new DrinkSensoryProfile { Body = "round", Texture = "satin", Sweetness = "restrained", Acidity = "quiet", Finish = "linger", Energy = "intense", CaffeineIntensity = 5 }),
        Row(Guid.NewGuid(), "Cold Brew", "Cold Brew", new DrinkSensoryProfile { Body = "round", Texture = "satin", Sweetness = "restrained", Acidity = "balanced", Finish = "clean", Energy = "focused", CaffeineIntensity = 4 }),
        Row(Guid.NewGuid(), "Cold Brew Cam", "Cold Brew", new DrinkSensoryProfile { Body = "round", Texture = "crisp", Sweetness = "rounded", Acidity = "lifted", AromaFamily = "citrus", Finish = "clean", Energy = "lifted", CaffeineIntensity = 4 }),
        Row(Guid.NewGuid(), "Cold Brew Táo", "Cold Brew", new DrinkSensoryProfile { Body = "round", Texture = "crisp", Sweetness = "rounded", Acidity = "crystalline", AromaFamily = "citrus", Finish = "clean", Energy = "playful", CaffeineIntensity = 4 }),
        Row(Guid.NewGuid(), "Cappuccino", "Espresso", new DrinkSensoryProfile { Body = "round", Texture = "satin", Sweetness = "rounded", Acidity = "balanced", Finish = "clean", Energy = "focused", CaffeineIntensity = 3 }),
        Row(Guid.NewGuid(), "Latte", "Espresso", new DrinkSensoryProfile { Body = "round", Texture = "velvet", Sweetness = "gentle", Acidity = "quiet", Finish = "linger", Energy = "still", CaffeineIntensity = 2 }),
        Row(Guid.NewGuid(), "Mocha", "Espresso", new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Acidity = "quiet", AromaFamily = "cocoa", Finish = "linger", Energy = "still", CaffeineIntensity = 3 }),
        Row(Guid.NewGuid(), "Salted Caramel Latte", "Espresso", new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Acidity = "quiet", AromaFamily = "cocoa", Finish = "linger", Energy = "still", CaffeineIntensity = 2 }),
        Row(Guid.NewGuid(), "Bạc Xỉu", "Vietnamese Coffee", new DrinkSensoryProfile { Body = "syrupy", Texture = "velvet", Sweetness = "luscious", Acidity = "quiet", Finish = "linger", Energy = "still", CaffeineIntensity = 1 })
    ];

    private static IReadOnlyList<GuidedRecommendationRow> RankCoffee(
        IReadOnlyList<MenuItemScoringRow> rows,
        GuidedOptionSeed mood,
        GuidedOptionSeed sweetness,
        GuidedOptionSeed caffeine,
        DrinkSensoryProfile? extraGuestHints = null)
    {
        var selected = new[]
        {
            mood,
            new GuidedOptionSeed(
                "q2_coffee",
                "Cà phê",
                "cà phê",
                new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "round", Energy = "focused", CaffeineIntensity = 3 },
                MoodKey: "focus",
                CategoryIntentKey: BeverageFamilyGrounding.Coffee),
            sweetness,
            caffeine
        };

        var hints = Merge(
            new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "round" },
            mood.SensoryHints,
            sweetness.SensoryHints,
            caffeine.SensoryHints,
            extraGuestHints ?? new DrinkSensoryProfile());

        return GuidedSommelierRecommendationEngine.Rank(hints, selected, rows, take: rows.Count);
    }

    private static GuidedOptionSeed Mood(string id, string label, string fragment, DrinkSensoryProfile hints, string moodKey) =>
        new(id, label, fragment, hints, MoodKey: moodKey);

    private static GuidedOptionSeed Sweetness(string id, string label, string fragment, DrinkSensoryProfile hints) =>
        new(id, label, fragment, hints);

    private static GuidedOptionSeed Caffeine(string id, string label, string fragment, DrinkSensoryProfile hints) =>
        new(id, label, fragment, hints);

    private static int IndexOf(IReadOnlyList<GuidedRecommendationRow> ranked, string name) =>
        ranked.Select((r, i) => (r.Name, i)).First(x => x.Name == name).i;

    private static DrinkSensoryProfile Merge(params DrinkSensoryProfile[] profiles)
    {
        var result = new DrinkSensoryProfile();
        foreach (var p in profiles)
        {
            result.Body = Coalesce(p.Body, result.Body);
            result.Acidity = Coalesce(p.Acidity, result.Acidity);
            result.Sweetness = Coalesce(p.Sweetness, result.Sweetness);
            result.Finish = Coalesce(p.Finish, result.Finish);
            result.AromaFamily = Coalesce(p.AromaFamily, result.AromaFamily);
            result.TemperatureEmotion = Coalesce(p.TemperatureEmotion, result.TemperatureEmotion);
            result.Energy = Coalesce(p.Energy, result.Energy);
            result.SocialMood = Coalesce(p.SocialMood, result.SocialMood);
            result.Texture = Coalesce(p.Texture, result.Texture);
            if (p.CaffeineIntensity is >= 1 and <= 5)
                result.CaffeineIntensity = p.CaffeineIntensity;
        }

        return result;
    }

    private static string? Coalesce(string? next, string? current) =>
        string.IsNullOrWhiteSpace(next) ? current : next;
}