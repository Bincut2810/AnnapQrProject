namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Editorial copy for the discovery ritual — templates only (no LLM). Replace with CMS JSON later via same surface.
/// </summary>
public static class GuestDiscoveryRitualComposer
{
    public static IReadOnlyList<string> LoadingPhrases { get; } =
    [
        "One moment — I'm at the bar.",
        "The pass is quiet — I'm choosing one cup.",
        "Steam rises — give me a breath.",
        "The room is slowing down…",
        "I'm reading the evening before I pour."
    ];

    public static string PickLoadingPhrase(int salt) =>
        LoadingPhrases[Math.Abs(salt) % LoadingPhrases.Count];

    /// <summary>Single editorial line shown before the cup — calm, confident, no mascot tone.</summary>
    public static string LeadEmotionalLine(IReadOnlyList<string> signals)
    {
        var body = PersonalityReflection(signals).Trim();
        if (body.Length == 0)
            return "The room feels unhurried — I'll pour with that in mind.";
        var cut = body.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cut.Length == 0)
            return body;
        var first = cut[0];
        if (first.Length > 140)
            return first[..137].TrimEnd() + "…";
        return first.EndsWith('.') ? first : first + ".";
    }

    public static string TastingInterpretation(IReadOnlyList<string> signals)
    {
        if (signals.Count == 0)
            return "I'll read the room in this light — unhurried.";

        var set = new HashSet<string>(signals.Select(GuestDiscoveryAffinityScorer.NormalizeSignal).Where(s => s.Length > 0), StringComparer.Ordinal);

        if (set.Contains("slow_evening") && set.Contains("soft_sweet"))
            return "A slower pour, softer at the edges — conversation can stay unhurried.";
        if (set.Contains("creamy_calm") && set.Contains("floral_quiet"))
            return "Creamy calm with a floral lift — gentle contrast, quietly composed.";
        if (set.Contains("bright_lift") && set.Contains("curious_edge"))
            return "Bright, with a curious edge — structure first, a small surprise in the sip.";
        if (set.Contains("bold_depth"))
            return "Depth tonight — presence at the finish, still disciplined.";
        if (set.Contains("bright_lift"))
            return "Lift and clarity — brightness without sharpness.";
        if (set.Contains("soft_sweet") || set.Contains("creamy_calm"))
            return "Softness and roundness — comforting, held in a calm frame.";
        if (set.Contains("floral_quiet"))
            return "A quiet floral thread — delicate, not decorative.";
        if (set.Contains("curious_edge"))
            return "Curious, but grounded — nuance without spectacle.";

        return "Balance feels right for this table — I'll pour with that in mind.";
    }

    /// <summary>1–3 short sentences, sommelier tone.</summary>
    public static string PersonalityReflection(IReadOnlyList<string> signals)
    {
        if (signals.Count == 0)
            return "I'll choose with restraint — let the cup speak first.";

        var set = new HashSet<string>(signals.Select(GuestDiscoveryAffinityScorer.NormalizeSignal).Where(s => s.Length > 0), StringComparer.Ordinal);

        if (set.Contains("slow_evening") && (set.Contains("creamy_calm") || set.Contains("soft_sweet")))
            return "Comforting textures, slower moments — I'll keep the pour low and the finish long.";
        if (set.Contains("bright_lift") && set.Contains("floral_quiet"))
            return "Lifted brightness with a floral quiet — nothing loud, a clean arc in the cup.";
        if (set.Contains("bold_depth") && set.Contains("curious_edge"))
            return "Depth with a curious edge — expressive, still seated at a calm table.";
        if (set.Contains("creamy_calm"))
            return "Creamy calm, rounded edges, space to settle.";
        if (set.Contains("bright_lift"))
            return "Expressive clarity — brightness held in good manners.";
        if (set.Contains("floral_quiet"))
            return "Delicacy — floral nuance without perfume.";
        if (set.Contains("curious_edge"))
            return "Curious, but grounded — a thoughtful turn, not a jolt.";

        return "Balance feels right — I'll follow that line with care.";
    }

    public static string PalateAlignmentLabel(int compatibilityPercent, int rankIndex)
    {
        if (rankIndex == 0 && compatibilityPercent >= 95)
            return "Excellent fit";
        if (rankIndex == 0)
            return "Strong alignment";
        if (rankIndex == 1 && compatibilityPercent >= 84)
            return "A natural match";
        if (rankIndex == 1)
            return "Close companion pour";
        if (rankIndex == 2 && compatibilityPercent <= 72)
            return "Adventurous pairing";
        if (rankIndex == 2)
            return "A softer echo on your line";
        return compatibilityPercent switch
        {
            >= 93 => "Strong alignment",
            >= 87 => "A natural match",
            >= 81 => "Thoughtful beside what you described",
            _ => "Held quietly beside your answers"
        };
    }

    public static string MoodAlignmentLine(GuestDiscoveryCurator.PoolRow row)
    {
        var mood = (row.MoodProfile ?? "").Trim();
        if (mood.Length is > 0 and < 64)
            return $"This cup carries \"{mood}\" — quietly, as the evening asks.";
        var e = (row.SensoryMerged.Energy ?? "").Trim();
        if (!string.IsNullOrEmpty(e))
            return $"The sip reads {e} — held low, not announced.";
        return "A quiet cup for a quiet table.";
    }

    public static IReadOnlyList<string> FlavorChips(GuestDiscoveryCurator.PoolRow row, IReadOnlyList<string> matchedHints)
    {
        var chips = new List<string>();
        foreach (var h in matchedHints.Take(3))
            chips.Add(h.Trim());
        var tex = row.SensoryMerged.Texture?.Trim();
        if (!string.IsNullOrEmpty(tex) && chips.Count < 3 && !chips.Contains(tex, StringComparer.OrdinalIgnoreCase))
            chips.Add(tex + " mouthfeel");
        var sweet = row.SensoryMerged.Sweetness?.Trim();
        if (!string.IsNullOrEmpty(sweet) && chips.Count < 3)
            chips.Add(sweet + " sweetness");
        if (chips.Count == 0 && !string.IsNullOrWhiteSpace(row.TastingNotes))
        {
            var shortTn = row.TastingNotes.Trim();
            if (shortTn.Length > 42)
                shortTn = shortTn[..42].TrimEnd() + "…";
            chips.Add(shortTn);
        }

        return chips;
    }
}
