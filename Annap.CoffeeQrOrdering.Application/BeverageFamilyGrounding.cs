using System.Globalization;
using System.Text;

namespace Annap.CoffeeQrOrdering.Application;

/// <summary>
/// Hard semantic boundary for Sommelier beverage-family choices.
/// Mood may bend; the selected family may not.
/// </summary>
public static class BeverageFamilyGrounding
{
    public const string Coffee = "coffee";
    public const string Tea = "tea";
    public const string Juice = "juice";
    public const string Smoothie = "smoothie";
    public const string Matcha = "matcha";
    public const string Fruit = "fruit";
    public const string Signature = "signature";

    public static readonly string[] KnownFamilies =
    [
        Coffee,
        Tea,
        Juice,
        Smoothie,
        Matcha,
        Fruit,
        Signature
    ];

    public static string? NormalizeFamilyKey(string? raw)
    {
        var value = Normalize(raw);
        if (value.Length == 0)
            return null;

        if (ContainsAny(value, "coffee", "cafe", "ca phe", "caphe", "espresso", "latte", "americano", "cold brew", "bac xiu"))
            return Coffee;
        if (ContainsAny(value, "matcha"))
            return Matcha;
        if (ContainsAny(value, "smoothie", "sinh to", "sinh tố"))
            return Smoothie;
        if (ContainsAny(value, "juice", "nuoc ep", "nước ép"))
            return Juice;
        if (ContainsAny(value, "fruit", "trai cay", "trái cây"))
            return Fruit;
        if (ContainsAny(value, "tea", "tra", "trà", "hibicus", "hibiscus"))
            return Tea;
        if (ContainsAny(value, "signature", "house", "dac trung", "đặc trưng"))
            return Signature;

        return null;
    }

    public static string? ResolveFamilyKey(params string?[] values)
    {
        foreach (var value in values)
        {
            var key = NormalizeFamilyKey(value);
            if (key is not null)
                return key;
        }

        return null;
    }

    public static string DisplayName(string? familyKey) =>
        NormalizeFamilyKey(familyKey) switch
        {
            Coffee => "Coffee",
            Tea => "Tea",
            Juice => "Juice",
            Smoothie => "Smoothie",
            Matcha => "Matcha",
            Fruit => "Juice / Smoothie",
            Signature => "Signature",
            _ => "Any"
        };

    public static IReadOnlyList<string> AllowedCategoryNames(string? familyKey) =>
        NormalizeFamilyKey(familyKey) switch
        {
            Coffee => ["Espresso", "Cold Brew", "Vietnamese Coffee", "Coffee", "Specialty Coffee"],
            Tea => ["Tea"],
            Juice => ["Juice"],
            Smoothie => ["Smoothie"],
            Matcha => ["Matcha", "Tea"],
            Fruit => ["Juice", "Smoothie"],
            Signature => ["Signature"],
            _ => []
        };

    public static bool Matches(
        string? familyKey,
        string? categoryName,
        string? itemName,
        string? itemType = null,
        string? ingredientBreakdown = null,
        string? flavorTags = null)
    {
        var key = NormalizeFamilyKey(familyKey);
        if (key is null)
            return true;

        var category = Normalize(categoryName);
        var name = Normalize(itemName);
        var type = Normalize(itemType);
        var ingredients = Normalize(ingredientBreakdown);
        var tags = Normalize(flavorTags);
        var haystack = string.Join(' ', name, type, ingredients, tags);

        return key switch
        {
            Coffee => CategoryIs(category, "espresso", "cold brew", "vietnamese coffee", "coffee", "specialty coffee"),
            Tea => CategoryIs(category, "tea") && !ContainsAny(haystack, "matcha"),
            Juice => CategoryIs(category, "juice"),
            Smoothie => CategoryIs(category, "smoothie"),
            Matcha => CategoryIs(category, "matcha") || ContainsAny(haystack, "matcha"),
            Fruit => CategoryIs(category, "juice", "smoothie"),
            Signature => CategoryIs(category, "signature"),
            _ => true
        };
    }

    public static bool MatchesCategoryOnly(string? familyKey, string? categoryName)
    {
        var key = NormalizeFamilyKey(familyKey);
        if (key is null)
            return true;

        var category = Normalize(categoryName);
        return key switch
        {
            Coffee => CategoryIs(category, "espresso", "cold brew", "vietnamese coffee", "coffee", "specialty coffee"),
            Tea => CategoryIs(category, "tea"),
            Juice => CategoryIs(category, "juice"),
            Smoothie => CategoryIs(category, "smoothie"),
            Matcha => CategoryIs(category, "matcha", "tea"),
            Fruit => CategoryIs(category, "juice", "smoothie"),
            Signature => CategoryIs(category, "signature"),
            _ => true
        };
    }

    public static string PromptConstraintLine(string? familyKey)
    {
        var key = NormalizeFamilyKey(familyKey);
        if (key is null)
            return "";

        var display = DisplayName(key);
        return $"The guest has chosen the {display} family. You may ONLY recommend drinks within the {display} family. Reject any cross-category drift even when mood, embeddings, or refinement language point elsewhere.";
    }

    private static bool CategoryIs(string category, params string[] allowed) =>
        allowed.Any(a => category.Equals(Normalize(a), StringComparison.Ordinal));

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
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
