using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>
/// Typed affinity between sensory tokens—edges are authored, not scraped keywords.
/// Scores combine axis alignment and gentle progression along refinement arcs.
/// </summary>
public static class FlavorAffinityEngine
{
    private static readonly string[] SweetnessDryToSweet =
        ["dry", "austere", "restrained", "rounded", "gentle", "luscious", "decadent"];

    private static readonly string[] AcidityQuietToBright =
        ["quiet", "balanced", "lifted", "crystalline", "luminous"];

    private static readonly string[] EnergyStillToIntense =
        ["still", "focused", "lifted", "playful", "intense"];

    public static double ScoreHintsVsCup(DrinkSensoryProfile hints, DrinkSensoryProfile cup)
    {
        double s = 0;
        s += MatchAxis(hints.Body, cup.Body, 2.1);
        s += MatchAxis(hints.Acidity, cup.Acidity, 2.0);
        s += MatchAxis(hints.Sweetness, cup.Sweetness, 2.2);
        s += MatchAxis(hints.Finish, cup.Finish, 1.4);
        s += MatchAxis(hints.AromaFamily, cup.AromaFamily, 1.9);
        s += MatchAxis(hints.TemperatureEmotion, cup.TemperatureEmotion, 1.5);
        s += MatchAxis(hints.Energy, cup.Energy, 1.8);
        s += MatchAxis(hints.SocialMood, cup.SocialMood, 1.7);
        s += MatchAxis(hints.Texture, cup.Texture, 1.6);
        s += CrossAffinity(hints, cup);
        if (hints.CaffeineIntensity is >= 1 and <= 5 && cup.CaffeineIntensity is >= 1 and <= 5)
        {
            var d = Math.Abs(hints.CaffeineIntensity - cup.CaffeineIntensity);
            s += d == 0 ? 1.4 : d == 1 ? 0.75 : 0.2;
        }

        return s;
    }

    /// <summary>
    /// How much <paramref name="cup"/> sits one sensory step from <paramref name="previousLead"/> on major axes
    /// (adjacency, not identity)—used to prefer believable tray moves over random jumps.
    /// </summary>
    public static double SensoryNeighborStepAffinity(DrinkSensoryProfile? previousLead, DrinkSensoryProfile cup)
    {
        if (previousLead is null)
            return 0;
        double s = 0;
        s += AxisStepOnly(previousLead.Sweetness, cup.Sweetness, SweetnessDryToSweet, 1.05);
        s += AxisStepOnly(previousLead.Acidity, cup.Acidity, AcidityQuietToBright, 1.0);
        s += AxisStepOnly(previousLead.Energy, cup.Energy, EnergyStillToIntense, 0.95);
        s += AxisTokenNeighborOnly(previousLead.Texture, cup.Texture, 0.78);
        s += AxisTokenNeighborOnly(previousLead.Body, cup.Body, 0.72);
        s += AxisTokenNeighborOnly(previousLead.Finish, cup.Finish, 0.65);
        s += AxisTokenNeighborOnly(previousLead.AromaFamily, cup.AromaFamily, 0.7);
        s += AxisTokenNeighborOnly(previousLead.TemperatureEmotion, cup.TemperatureEmotion, 0.62);
        if (previousLead.CaffeineIntensity is >= 1 and <= 5 && cup.CaffeineIntensity is >= 1 and <= 5)
        {
            var d = Math.Abs(previousLead.CaffeineIntensity - cup.CaffeineIntensity);
            if (d == 1)
                s += 1.05;
        }

        return Math.Min(s, 3.85);
    }

    private static double AxisStepOnly(string? prev, string? cup, IReadOnlyList<string> axis, double weight)
    {
        var ia = IndexOnAxis(prev, axis);
        var ib = IndexOnAxis(cup, axis);
        if (ia < 0 || ib < 0)
            return 0;
        var d = Math.Abs(ia - ib);
        if (d == 1)
            return weight;
        if (d == 0)
            return 0.1;
        return 0;
    }

    private static double AxisTokenNeighborOnly(string? prev, string? cup, double weight)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(cup))
            return 0;
        if (string.Equals(prev.Trim(), cup.Trim(), StringComparison.OrdinalIgnoreCase))
            return 0.08;
        return NeighborBonus(prev, cup) ? weight * 0.52 : 0;
    }

    public static double TrajectoryFromPrevious(
        DrinkSensoryProfile? previousLead,
        DrinkSensoryProfile cup,
        string? refinementKey)
    {
        if (previousLead is null || string.IsNullOrWhiteSpace(refinementKey))
            return 0;
        var k = refinementKey.Trim().ToLowerInvariant();
        return k switch
        {
            "less_sweet" => SweetnessPull(previousLead, cup, -1),
            "softer" => SoftPull(previousLead, cup),
            "brighter" => AcidityPull(previousLead, cup, 1),
            "warmer" => WarmPull(previousLead, cup),
            "low_caffeine" => CaffeinePull(previousLead, cup, -1),
            "more_adventurous" => EnergyPull(previousLead, cup, 1),
            _ => ContinuitySoft(previousLead, cup)
        };
    }

    private static double ContinuitySoft(DrinkSensoryProfile prev, DrinkSensoryProfile cup) =>
        (MatchAxis(prev.AromaFamily, cup.AromaFamily, 1.0) + MatchAxis(prev.Body, cup.Body, 0.8)) * 0.35;

    private static double SweetnessPull(DrinkSensoryProfile prev, DrinkSensoryProfile cup, int steps)
    {
        var ti = IndexOnAxis(prev.Sweetness, SweetnessDryToSweet);
        if (ti < 0)
            return 0;
        var target = Math.Clamp(ti + steps, 0, SweetnessDryToSweet.Length - 1);
        var ci = IndexOnAxis(cup.Sweetness, SweetnessDryToSweet);
        if (ci < 0)
            return MatchAxis(prev.Sweetness, cup.Sweetness, 0.6);
        return 2.8 - Math.Abs(ci - target) * 0.95;
    }

    private static double AcidityPull(DrinkSensoryProfile prev, DrinkSensoryProfile cup, int steps)
    {
        var ti = IndexOnAxis(prev.Acidity, AcidityQuietToBright);
        if (ti < 0)
            return MatchAxis(prev.Acidity, cup.Acidity, 0.7);
        var target = Math.Clamp(ti + steps, 0, AcidityQuietToBright.Length - 1);
        var ci = IndexOnAxis(cup.Acidity, AcidityQuietToBright);
        if (ci < 0)
            return 0;
        return 2.6 - Math.Abs(ci - target) * 0.9;
    }

    private static double EnergyPull(DrinkSensoryProfile prev, DrinkSensoryProfile cup, int steps)
    {
        var ti = IndexOnAxis(prev.Energy, EnergyStillToIntense);
        if (ti < 0)
            return 0;
        var target = Math.Clamp(ti + steps, 0, EnergyStillToIntense.Length - 1);
        var ci = IndexOnAxis(cup.Energy, EnergyStillToIntense);
        if (ci < 0)
            return 0;
        return 2.4 - Math.Abs(ci - target) * 0.85;
    }

    private static double SoftPull(DrinkSensoryProfile prev, DrinkSensoryProfile cup)
    {
        var t = MatchAxis(prev.Texture, cup.Texture, 0.9);
        var b = MatchAxis(prev.Body, cup.Body, 0.8);
        var e = EnergyPull(prev, cup, -1) * 0.45;
        return t + b + e;
    }

    private static double WarmPull(DrinkSensoryProfile prev, DrinkSensoryProfile cup)
    {
        var th = MatchAxis(prev.TemperatureEmotion, cup.TemperatureEmotion, 1.0);
        var aroma = MatchAxis(prev.AromaFamily, cup.AromaFamily, 0.5);
        // Encourage temperate→warming or warming→ember without hard string equality
        var prevT = prev.TemperatureEmotion?.ToLowerInvariant() ?? "";
        var cupT = cup.TemperatureEmotion?.ToLowerInvariant() ?? "";
        var step = (prevT, cupT) switch
        {
            ("cool", "temperate") or ("temperate", "warming") or ("warming", "ember") => 1.6,
            ("cool", "warming") or ("temperate", "ember") => 1.2,
            _ when prevT == cupT => 0.9,
            _ => 0.2
        };
        return th + aroma + step;
    }

    private static double CaffeinePull(DrinkSensoryProfile prev, DrinkSensoryProfile cup, int delta)
    {
        if (prev.CaffeineIntensity is < 1 or > 5 || cup.CaffeineIntensity is < 1 or > 5)
            return 0;
        var target = Math.Clamp(prev.CaffeineIntensity + delta, 1, 5);
        return 2.5 - Math.Abs(cup.CaffeineIntensity - target) * 0.9;
    }

    private static double MatchAxis(string? hint, string? cup, double weight)
    {
        if (string.IsNullOrWhiteSpace(hint) || string.IsNullOrWhiteSpace(cup))
            return 0;
        if (string.Equals(hint.Trim(), cup.Trim(), StringComparison.OrdinalIgnoreCase))
            return weight;
        if (NeighborBonus(hint, cup))
            return weight * 0.48;
        return 0;
    }

    private static bool NeighborBonus(string a, string b)
    {
        if (AxisNeighbors(a, b, SweetnessDryToSweet))
            return true;
        if (AxisNeighbors(a, b, AcidityQuietToBright))
            return true;
        if (AxisNeighbors(a, b, EnergyStillToIntense))
            return true;
        return GraphEdge(a, b);
    }

    private static bool AxisNeighbors(string a, string b, IReadOnlyList<string> axis)
    {
        var ia = IndexOnAxis(a, axis);
        var ib = IndexOnAxis(b, axis);
        if (ia < 0 || ib < 0)
            return false;
        return Math.Abs(ia - ib) == 1;
    }

    private static int IndexOnAxis(string? token, IReadOnlyList<string> axis)
    {
        if (string.IsNullOrWhiteSpace(token))
            return -1;
        var t = token.Trim().ToLowerInvariant();
        for (var i = 0; i < axis.Count; i++)
        {
            if (string.Equals(axis[i], t, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>Lightweight cross-axis graph: aroma ↔ finish, social ↔ energy.</summary>
    private static double CrossAffinity(DrinkSensoryProfile hints, DrinkSensoryProfile cup)
    {
        double s = 0;
        if (TokenEquals(hints.AromaFamily, "floral") && TokenEquals(cup.Finish, "linger"))
            s += 0.55;
        if (TokenEquals(hints.AromaFamily, "citrus") && TokenEquals(cup.Acidity, "crystalline"))
            s += 0.55;
        if (TokenEquals(hints.SocialMood, "quiet") && TokenEquals(cup.Energy, "focused"))
            s += 0.65;
        if (TokenEquals(hints.SocialMood, "quiet") && TokenEquals(cup.SocialMood, "solitary"))
            s += 0.5;
        if (TokenEquals(hints.Energy, "playful") && TokenEquals(cup.SocialMood, "gathered"))
            s += 0.45;
        if (TokenEquals(hints.AromaFamily, "cocoa") && TokenEquals(cup.TemperatureEmotion, "warming"))
            s += 0.45;
        return s;
    }

    private static bool GraphEdge(string a, string b) =>
        (TokenEquals(a, "floral") && TokenEquals(b, "soft_floral")) ||
        (TokenEquals(a, "citrus") && TokenEquals(b, "lifted"));

    private static bool TokenEquals(string? x, string y) =>
        !string.IsNullOrWhiteSpace(x) && string.Equals(x.Trim(), y, StringComparison.OrdinalIgnoreCase);
}
