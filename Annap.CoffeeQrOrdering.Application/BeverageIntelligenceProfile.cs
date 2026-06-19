using System.Globalization;
using System.Text;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Application;

public sealed record BeverageIntent(
    string? FamilyKey,
    bool WantsFocus,
    bool WantsStrongCaffeine,
    bool WantsLowCaffeine,
    bool WantsComfort,
    bool WantsSweetness,
    bool WantsRefreshing,
    bool WantsBitterness = false,
    bool WantsCreamy = false,
    bool WantsDessertLike = false,
    bool WantsAdventurous = false)
{
    public bool IsStrongCoffeeFocus =>
        BeverageFamilyGrounding.NormalizeFamilyKey(FamilyKey) == BeverageFamilyGrounding.Coffee
        && (WantsStrongCaffeine || WantsFocus)
        && !WantsLowCaffeine
        && !WantsComfort
        && !WantsCreamy
        && !WantsDessertLike;
}

public sealed record BeverageIntelligenceProfile(
    string Category,
    int CaffeineLevel,
    int MilkIntensity,
    int Sweetness,
    int CoffeeForward,
    int Refreshing,
    int DessertLike,
    int FruitForward,
    int TeaForward,
    int SpecialtyScore,
    int FocusScore,
    int ComfortScore,
    int HeatTolerance,
    int Acidity,
    int TextureWeight,
    int Bitterness,
    int Approachability,
    int Adventurousness)
{
    public string SpecialtyLine() =>
        $"caffeine {CaffeineLevel}/5; coffee-forward {CoffeeForward}/5; milk {MilkIntensity}/5; sweetness {Sweetness}/5; bitterness {Bitterness}/5; comfort {ComfortScore}/5; approachability {Approachability}/5; specialty {SpecialtyScore}/5; focus {FocusScore}/5; clarity {Acidity}/5; texture weight {TextureWeight}/5";
}

/// <summary>
/// Specialty beverage intelligence layered above mood affinity.
/// It keeps ANNAP's guidance coffee-literate without adding new DB schema.
/// </summary>
public static class BeverageIntelligence
{
    public static BeverageIntent BuildIntent(
        string? familyKey,
        DrinkSensoryProfile hints,
        IEnumerable<string?> selectedKeysOrLabels,
        string? moodKey = null,
        string? refinementKey = null,
        string? guestLine = null)
    {
        var text = Normalize(string.Join(' ', selectedKeysOrLabels.Where(x => !string.IsNullOrWhiteSpace(x)))
                             + " " + moodKey + " " + refinementKey + " " + guestLine);
        var wantsFocus =
            hints.Energy.Equals("focused", StringComparison.OrdinalIgnoreCase)
            || hints.Energy.Equals("intense", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(text, "focus", "focused", "productivity", "productive", "alert", "tinh tao", "tỉnh táo", "tap trung", "tập trung", "lam viec", "làm việc");
        var wantsStrong =
            hints.CaffeineIntensity >= 4
            || ContainsAny(text, "strong", "cang manh", "càng mạnh", "high caffeine", "intense", "dam", "đậm", "manh", "mạnh");
        var wantsLow =
            hints.CaffeineIntensity is >= 1 and <= 2
            || ContainsAny(text, "low_caffeine", "low caffeine", "decaf", "khong caffeine", "không caffeine", "caffeine nhe", "caffeine nhẹ");
        var wantsSweet =
            hints.Sweetness is "gentle" or "luscious"
            || ContainsAny(text, "sweet", "ngot nhieu", "ngọt nhiều", "hoi ngot", "hơi ngọt", "dessert", "caramel", "chocolate", "mocha");
        var wantsComfort =
            hints.Energy.Equals("still", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(text, "soft", "softer", "comfort", "calm", "cozy", "nhe nhang", "nhẹ nhàng", "em", "êm");
        var wantsRefreshing =
            hints.Acidity is "lifted" or "crystalline"
            || hints.Finish.Equals("clean", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(text, "refresh", "bright", "brighter", "fresh", "tuoi", "tươi", "thanh");
        var wantsBitterness =
            hints.Sweetness is "dry" or "restrained"
            || ContainsAny(text, "bitter", "bitterness", "dry", "less sweet", "it ngot", "ít ngọt", "dang", "đắng");
        var wantsCreamy =
            (!wantsStrong && !wantsFocus && hints.Texture is "velvet")
            || ContainsAny(text, "creamy", "cream", "milk", "milky", "velvet", "latte", "sua", "sữa", "beo", "béo");
        var wantsDessert =
            hints.Sweetness is "luscious"
            || ContainsAny(text, "dessert", "caramel", "chocolate", "mocha", "ngot nhieu", "ngọt nhiều");
        var wantsAdventurous =
            hints.Energy.Equals("playful", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(text, "adventurous", "exploratory", "curious", "new", "strange", "la", "lạ", "kham pha", "khám phá");

        return new BeverageIntent(
            BeverageFamilyGrounding.NormalizeFamilyKey(familyKey),
            wantsFocus,
            wantsStrong,
            wantsLow,
            wantsComfort,
            wantsSweet,
            wantsRefreshing,
            wantsBitterness,
            wantsCreamy,
            wantsDessert,
            wantsAdventurous);
    }

    public static BeverageIntelligenceProfile Classify(
        string? categoryName,
        string? itemName,
        DrinkSensoryProfile? sensory = null,
        string? itemType = null,
        string? ingredientBreakdown = null,
        string? flavorTags = null)
    {
        var category = categoryName?.Trim() ?? "";
        var n = Normalize(itemName);
        var c = Normalize(categoryName);
        var t = Normalize(itemType);
        var i = Normalize(ingredientBreakdown);
        var tags = Normalize(flavorTags);
        var all = string.Join(' ', n, c, t, i, tags);
        var sx = sensory ?? new DrinkSensoryProfile();

        var caffeine = sx.CaffeineIntensity is >= 1 and <= 5 ? sx.CaffeineIntensity : 2;
        var milk = ContainsAny(all, "sua tuoi", "sữa tươi", "sua dac", "sữa đặc", "milk", "latte", "bac xiu", "bạc xỉu") ? 3 : 0;
        var sweetness = sx.Sweetness switch
        {
            "dry" or "restrained" => 1,
            "rounded" => 3,
            "gentle" => 4,
            "luscious" => 5,
            _ => 2
        };
        var coffeeForward = BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Coffee, category, itemName) ? 3 : 0;
        var refreshing = sx.Finish == "clean" || sx.Acidity is "lifted" or "crystalline" ? 3 : 1;
        var dessert = sweetness >= 4 ? 3 : 0;
        var fruit = BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Fruit, category, itemName) ? 4 : 0;
        var tea = BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Tea, category, itemName) ? 4 : 0;
        var specialty = 2;
        var focus = sx.Energy is "focused" or "intense" ? 3 : Math.Clamp(caffeine - 1, 0, 5);
        var comfort = sx.Energy is "still" ? 3 : 1;
        var heat = 3;
        var acidity = sx.Acidity switch
        {
            "quiet" => 1,
            "balanced" => 3,
            "lifted" => 4,
            "crystalline" or "luminous" => 5,
            _ => 2
        };
        var texture = sx.Texture switch
        {
            "crisp" or "effervescent" => 1,
            "satin" => 2,
            "velvet" => 4,
            "syrupy" => 5,
            _ => 2
        };
        var bitterness = Math.Clamp(coffeeForward + Math.Max(0, caffeine - 2) - Math.Max(0, milk - 1) - Math.Max(0, sweetness - 3), 0, 5);
        var approachability = Math.Clamp(3 + milk + sweetness - bitterness - Math.Max(0, caffeine - 3), 0, 5);
        var adventurous = sx.Energy is "playful" or "lifted" ? 3 : 1;

        if (ContainsAny(n, "espresso"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 5, 0, 5, 5, 5);
            sweetness = Math.Min(sweetness, 2);
            bitterness = 5;
            approachability = 1;
            texture = Math.Min(texture, 2);
        }
        else if (ContainsAny(n, "americano", "ca phe den", "cà phê đen", "den da", "đen đá"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 5, 0, 5, 5, 5);
            sweetness = Math.Min(sweetness, 2);
            bitterness = 4;
            approachability = 2;
            texture = Math.Min(texture, 2);
        }
        else if (ContainsAny(n, "cold brew"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 4, 0, 5, 5, 4);
            refreshing = Math.Max(refreshing, 4);
            bitterness = Math.Max(bitterness, 3);
            approachability = Math.Max(approachability, 3);
            if (ContainsAny(n, "cam", "orange", "tao", "táo", "apple", "citrus"))
            {
                sweetness = Math.Max(sweetness, 3);
                fruit = Math.Max(fruit, 4);
                acidity = Math.Max(acidity, 4);
                refreshing = Math.Max(refreshing, 5);
                adventurous = Math.Max(adventurous, 5);
                bitterness = Math.Min(bitterness, 3);
            }
        }
        else if (ContainsAny(n, "capuchino", "cappuccino"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 3, 3, 3, 3, 3);
            sweetness = Math.Max(sweetness, 3);
            comfort = Math.Max(comfort, 3);
            bitterness = Math.Min(bitterness, 2);
            approachability = Math.Max(approachability, 4);
        }
        else if (ContainsAny(n, "mocha"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 3, 3, 3, 3, 3);
            milk = Math.Max(milk, 4);
            sweetness = Math.Max(sweetness, 4);
            dessert = Math.Max(dessert, 5);
            comfort = Math.Max(comfort, 4);
            bitterness = Math.Min(bitterness, 2);
            approachability = Math.Max(approachability, 4);
        }
        else if (ContainsAny(n, "ca phe sua", "cà phê sữa", "sua da sai gon", "sữa đá sài gòn"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 3, 3, 3, 3, 3);
            sweetness = Math.Max(sweetness, 4);
            dessert = Math.Max(dessert, 3);
            comfort = Math.Max(comfort, 3);
            bitterness = Math.Min(bitterness, 3);
            approachability = Math.Max(approachability, 3);
        }
        else if (ContainsAny(n, "salted caramel"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 2, 5, 2, 2, 2);
            sweetness = Math.Max(sweetness, 5);
            dessert = Math.Max(dessert, 5);
            comfort = Math.Max(comfort, 5);
            bitterness = Math.Min(bitterness, 1);
            approachability = Math.Max(approachability, 5);
        }
        else if (ContainsAny(n, "latte"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 2, 4, 2, 2, 2);
            sweetness = Math.Max(sweetness, 3);
            comfort = Math.Max(comfort, 5);
            bitterness = Math.Min(bitterness, 1);
            approachability = Math.Max(approachability, 5);
        }
        else if (ContainsAny(n, "bac xiu", "bạc xỉu"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 1, 5, 1, 2, 1);
            sweetness = Math.Max(sweetness, 5);
            dessert = Math.Max(dessert, 5);
            comfort = Math.Max(comfort, 5);
            bitterness = 0;
            approachability = 5;
        }
        else if (ContainsAny(n, "dufatanye", "abateranankunga", "kinini village", "rift valley coffee caucus", "nigussie nare", "murago outgrowers"))
        {
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, 3, 0, 4, 5, 4);
            if (ContainsAny(n, "dufatanye"))
            {
                approachability = 5;
                acidity = Math.Max(acidity, 2);
                fruit = Math.Max(fruit, 2);
                adventurous = 1;
            }
            else if (ContainsAny(n, "abateranankunga"))
            {
                approachability = 5;
                fruit = Math.Max(fruit, 4);
                acidity = Math.Max(acidity, 3);
                adventurous = 3;
            }
            else if (ContainsAny(n, "rift valley"))
            {
                approachability = 2;
                fruit = Math.Max(fruit, 4);
                adventurous = 5;
                bitterness = Math.Max(bitterness, 3);
            }
            else if (ContainsAny(n, "nigussie", "murago"))
            {
                approachability = 3;
                fruit = Math.Max(fruit, 5);
                specialty = 5;
                adventurous = 4;
            }
        }
        else if (BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Coffee, category, itemName))
            SetCoffee(ref caffeine, ref milk, ref coffeeForward, ref specialty, ref focus, Math.Max(caffeine, 3), milk, 3, 3, Math.Max(focus, 3));

        if (BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Matcha, category, itemName, itemType, ingredientBreakdown, flavorTags))
        {
            tea = Math.Max(tea, 4);
            specialty = Math.Max(specialty, 3);
            focus = Math.Max(focus, 3);
        }

        if (BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Smoothie, category, itemName))
        {
            milk = Math.Max(milk, 3);
            fruit = Math.Max(fruit, 4);
            comfort = Math.Max(comfort, 4);
            texture = Math.Max(texture, 4);
            approachability = Math.Max(approachability, 5);
        }

        if (BeverageFamilyGrounding.Matches(BeverageFamilyGrounding.Juice, category, itemName))
        {
            fruit = Math.Max(fruit, 5);
            refreshing = Math.Max(refreshing, 5);
            milk = 0;
            texture = Math.Min(texture, 2);
            adventurous = Math.Max(adventurous, 3);
        }

        return new BeverageIntelligenceProfile(
            category,
            Clamp5(caffeine),
            Clamp5(milk),
            Clamp5(sweetness),
            Clamp5(coffeeForward),
            Clamp5(refreshing),
            Clamp5(dessert),
            Clamp5(fruit),
            Clamp5(tea),
            Clamp5(specialty),
            Clamp5(focus),
            Clamp5(comfort),
            Clamp5(heat),
            Clamp5(acidity),
            Clamp5(texture),
            Clamp5(bitterness),
            Clamp5(approachability),
            Clamp5(adventurous));
    }

    public static double SpecialtyScore(BeverageIntelligenceProfile p, BeverageIntent intent)
    {
        var score = p.SpecialtyScore * 0.45 + p.Approachability * 0.2;
        if (BeverageFamilyGrounding.NormalizeFamilyKey(intent.FamilyKey) == BeverageFamilyGrounding.Coffee)
            score += p.CoffeeForward * 0.28;

        if (intent.WantsFocus)
            score += p.FocusScore * 1.15 + p.CaffeineLevel * 0.72 + p.CoffeeForward * 0.32;
        if (intent.WantsStrongCaffeine)
        {
            score += p.CaffeineLevel * 1.25 + p.CoffeeForward * 0.62 + p.FocusScore * 0.75;
            if (!intent.WantsSweetness && !intent.WantsComfort && !intent.WantsCreamy && !intent.WantsDessertLike)
                score += p.Bitterness * 0.85 - p.MilkIntensity * 0.52 - p.DessertLike * 0.44;
            else
                score += p.Sweetness * 0.28 + p.MilkIntensity * 0.2 - p.Bitterness * 0.22;
        }
        if (intent.WantsLowCaffeine)
        {
            score += (6 - p.CaffeineLevel) * 1.9 + p.Approachability * 0.8 + p.ComfortScore * 0.55;
            score -= Math.Max(0, p.CaffeineLevel - 3) * 1.15;
        }
        if (intent.WantsComfort)
            score += p.ComfortScore * 1.25 + p.MilkIntensity * 0.8 + p.TextureWeight * 0.58 + p.Approachability * 0.48 - p.Bitterness * 0.45;
        if (intent.WantsSweetness)
            score += p.Sweetness * 1.35 + p.DessertLike * 1.0 + p.Approachability * 0.55 - p.Bitterness * 0.72;
        if (intent.WantsRefreshing)
            score += p.Refreshing * 1.32 + p.Acidity * 0.72 + p.FruitForward * 0.35 - p.MilkIntensity * 0.25;
        if (intent.WantsBitterness)
            score += p.Bitterness * 1.2 + p.CoffeeForward * 0.55 - p.Sweetness * 0.42 - p.MilkIntensity * 0.3;
        if (intent.WantsCreamy)
            score += p.MilkIntensity * 1.35 + p.TextureWeight * 0.86 + p.ComfortScore * 0.6 + p.Approachability * 0.42 - p.Bitterness * 0.58;
        if (intent.WantsDessertLike)
            score += p.DessertLike * 1.25 + p.Sweetness * 0.78 + p.MilkIntensity * 0.6 + p.ComfortScore * 0.35 - p.Bitterness * 0.55;
        if (intent.WantsAdventurous)
            score += p.Adventurousness * 1.25 + p.FruitForward * 0.72 + p.Acidity * 0.45 + p.Refreshing * 0.3;

        if (intent.WantsLowCaffeine && intent.WantsSweetness)
            score += p.MilkIntensity * 0.9 + p.DessertLike * 0.75 + p.ComfortScore * 0.55 - p.CaffeineLevel * 0.85 - p.Bitterness * 0.8;

        return score;
    }

    public static string PromptPolicyLine(string? familyKey)
    {
        if (BeverageFamilyGrounding.NormalizeFamilyKey(familyKey) != BeverageFamilyGrounding.Coffee)
            return "";

        return "SPECIALTY_COFFEE_POLICY: Keep the guest inside Coffee, but let sweetness, texture, caffeine tolerance, and mood decide the style. For sweet or low-caffeine guests, avoid sharp black espresso expressions and prefer gentler milk or dessert coffee such as Latte, Mocha, Salted Caramel Latte, or Bạc Xỉu. For strong focus or bitter preferences, prefer Espresso, Americano, black Vietnamese coffee, and Cold Brew. For fruity or exploratory coffee, prefer bright Cold Brew variants. Explain this as sensory guidance, not filtering.";
    }

    private static void SetCoffee(
        ref int caffeine,
        ref int milk,
        ref int coffeeForward,
        ref int specialty,
        ref int focus,
        int c,
        int m,
        int cf,
        int sp,
        int fs)
    {
        caffeine = c;
        milk = m;
        coffeeForward = cf;
        specialty = sp;
        focus = fs;
    }

    private static int Clamp5(int value) => Math.Clamp(value, 0, 5);

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(n => value.Contains(Normalize(n), StringComparison.Ordinal));

    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var formD = raw.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
