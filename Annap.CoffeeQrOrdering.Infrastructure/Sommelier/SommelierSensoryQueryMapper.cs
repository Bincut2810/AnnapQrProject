using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Maps moods, refinements, and session drift into partial sensory targets for graph scoring.</summary>
public static class SommelierSensoryQueryMapper
{
    public static DrinkSensoryProfile FromRequest(SommelierGuideRequest r)
    {
        var p = new DrinkSensoryProfile();
        ApplyMood(r.MoodKey, p);
        ApplyRefinement(r.RefinementKey, p);
        ApplyFlavorDirection(r.FlavorDirectionHint, p);
        return p;
    }

    /// <summary>Lightweight text probe for sommelier scoring—not keyword spam, a few anchors only.</summary>
    public static DrinkSensoryProfile FromFreeText(string? q)
    {
        var p = new DrinkSensoryProfile();
        if (string.IsNullOrWhiteSpace(q))
            return p;
        var t = q.ToLowerInvariant();
        if (t.Contains("bright") || t.Contains("citrus") || t.Contains("acid"))
            p.Acidity = "lifted";
        if (t.Contains("quiet") || t.Contains("focus") || t.Contains("calm"))
        {
            p.Energy = "focused";
            p.SocialMood = "quiet";
        }

        if (t.Contains("sweet") && (t.Contains("less") || t.Contains("dry")))
            p.Sweetness = "restrained";
        if (t.Contains("warm") || t.Contains("cozy"))
            p.TemperatureEmotion = "warming";
        if (t.Contains("decaf") || t.Contains("low caf"))
            p.CaffeineIntensity = 2;
        if (t.Contains("floral") || t.Contains("delicate"))
            p.AromaFamily = "floral";
        if (t.Contains("chocolate") || t.Contains("cocoa"))
            p.AromaFamily = "cocoa";
        if (t.Contains("playful") || t.Contains("adventur"))
            p.Energy = "playful";
        return p;
    }

    private static void ApplyMood(string? moodKey, DrinkSensoryProfile p)
    {
        if (string.IsNullOrWhiteSpace(moodKey))
            return;
        switch (moodKey.Trim().ToLowerInvariant())
        {
            case "bright":
                p.Acidity = "crystalline";
                p.Energy = "lifted";
                p.TemperatureEmotion = "temperate";
                p.AromaFamily = "citrus";
                p.SocialMood = "quiet";
                break;
            case "slow":
                p.Body = "round";
                p.Texture = "velvet";
                p.TemperatureEmotion = "warming";
                p.Energy = "still";
                p.Sweetness = "rounded";
                break;
            case "floral":
                p.AromaFamily = "floral";
                p.Finish = "linger";
                p.FinishDetail = "soft floral finish";
                p.Acidity = "balanced";
                p.SocialMood = "solitary";
                break;
            case "adventurous":
                p.Energy = "intense";
                p.AromaFamily = "spice";
                p.SocialMood = "gathered";
                break;
            case "focus":
                p.Energy = "focused";
                p.Acidity = "lifted";
                p.SocialMood = "quiet";
                p.Body = "tea_like";
                break;
        }
    }

    private static void ApplyRefinement(string? refinementKey, DrinkSensoryProfile p)
    {
        if (string.IsNullOrWhiteSpace(refinementKey))
            return;
        switch (refinementKey.Trim().ToLowerInvariant())
        {
            case "softer":
                p.Texture = "velvet";
                p.Body = "round";
                p.Energy = "still";
                break;
            case "brighter":
                p.Acidity = "crystalline";
                p.Finish = "clean";
                break;
            case "less_sweet":
                p.Sweetness = "restrained";
                p.Finish = "clean";
                break;
            case "more_adventurous":
                p.Energy = "playful";
                p.AromaFamily = "spice";
                break;
            case "low_caffeine":
                p.CaffeineIntensity = 2;
                p.Energy = "still";
                break;
            case "warmer":
                p.TemperatureEmotion = "warming";
                p.Body = "round";
                break;
        }
    }

    private static void ApplyFlavorDirection(string? hint, DrinkSensoryProfile p)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return;
        var h = hint.ToLowerInvariant();
        if (h.Contains("softer") || h.Contains("less sweet") || h.Contains("mềm") || h.Contains("ít ngọt") ||
            h.Contains("êm hơn"))
            ApplyRefinement("softer", p);
        if (h.Contains("brighter") || h.Contains("sáng hơn") || h.Contains("nâng độ") || h.Contains("thanh hơn") ||
            h.Contains("mở hơn") || h.Contains("thanh và mở"))
            ApplyRefinement("brighter", p);
        if (h.Contains("gentler caffeine") || h.Contains("caffeine nhẹ") || h.Contains("caffeine dịu") ||
            h.Contains("gu caffeine") || h.Contains("caffeine êm"))
            ApplyRefinement("low_caffeine", p);
        if (h.Contains("adventurous") || h.Contains("tò mò") || h.Contains("đi xa") || h.Contains("dấn thêm"))
            ApplyRefinement("more_adventurous", p);
        if (h.Contains("warmer") || h.Contains("ôm ấm") || h.Contains("ấm hơn") || h.Contains("ôm ấm thêm"))
            ApplyRefinement("warmer", p);
    }
}
