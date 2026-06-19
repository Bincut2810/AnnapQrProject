using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Deterministic flavor / mood / texture affinity with penalties for crossed signals.
/// </summary>
public static class GuestDiscoveryAffinityScorer
{
    public sealed record AffinityBreakdown(
        double Positive,
        double Penalty,
        double Effective,
        IReadOnlyList<string> MatchReasons);

    private static readonly Dictionary<string, string[]> SignalKeywordHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["soft_sweet"] = ["sweet", "sugar", "honey", "caramel", "chocolate", "milk", "vanilla", "rounded", "luscious", "soft", "gentle", "cocoa"],
        ["creamy_calm"] = ["cream", "velvet", "silky", "round", "milk", "cocoa", "comfort", "calm", "still", "quiet", "linger", "butter"],
        ["bright_lift"] = ["bright", "citrus", "lift", "clean", "acid", "crystalline", "fresh", "yuzu", "spark", "effervescent"],
        ["floral_quiet"] = ["floral", "jasmine", "blossom", "delicate", "tea", "perfume", "honeysuckle", "quiet", "herbal"],
        ["bold_depth"] = ["bold", "dark", "roast", "cocoa", "smoke", "intense", "deep", "structure", "assertive", "bitter"],
        ["slow_evening"] = ["slow", "evening", "warm", "ember", "linger", "solitary", "hush", "unhurried", "velvet", "comfort"],
        ["curious_edge"] = ["playful", "adventurous", "lifted", "unexpected", "nuance", "layered", "curious", "edge", "complex"]
    };

    /// <summary>Anti-signals: when guest leans comforting / soft, sharp or tea-forward cups lose ground.</summary>
    private static readonly string[] ComfortConflictTokens =
    [
        "citrus", "lemon", "yuzu", "grapefruit", "acidic", "phosphoric", "vinegar", "sharp acidity", "bracing", "astringent",
        "tea-like", "tannic", "effervescent", "sparkling", "crisp acid"
    ];

    private static readonly string[] BrightConflictTokens =
    [
        "muddy", "ashy", "heavy roast", "bitter ash", "syrupy", "cloying", "flat", "stale"
    ];

    private static readonly string[] FloralConflictTokens =
    [
        "smoky", "carbon", "burnt", "rubber", "fermented funk"
    ];

    public static AffinityBreakdown Compute(GuestDiscoveryCurator.PoolRow row, IReadOnlyList<string> signals, int salt)
    {
        var blob = BuildSearchBlob(row);
        var sensory = row.SensoryMerged;
        var reasons = new List<string>();

        var listWeight = (double)Math.Clamp(row.DiscoveryWeight, 0.05m, 6m);
        var baseW = 18d + listWeight * 6d;
        if (row.IsSignature)
        {
            baseW += 7d;
            reasons.Add("House signature — weighted gently toward the tray");
        }

        if (row.IsSeasonalHighlight)
        {
            baseW += 5d;
            reasons.Add("Seasonal highlight on the list");
        }

        if (row.IsFeatured)
            baseW += 3d;

        // Tiny deterministic jitter so ties separate without feeling random.
        var jitter = (HashCode.Combine(row.Id, salt) & 0xFF) / 1024d;
        baseW += jitter;

        if (signals.Count == 0)
        {
            baseW += SensoryRichness(sensory);
            if (reasons.Count == 0)
                reasons.Add("Chosen from the open list — sensory detail and house weight carried the read.");
            return new AffinityBreakdown(baseW, 0d, baseW, reasons);
        }

        var signalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in signals)
        {
            var k = NormalizeSignal(raw);
            if (k.Length > 0)
                signalSet.Add(k);
        }

        var positive = baseW;
        var penalty = 0d;

        foreach (var raw in signals)
        {
            var key = NormalizeSignal(raw);
            if (string.IsNullOrEmpty(key) || !SignalKeywordHints.TryGetValue(key, out var hints))
                continue;

            var hits = 0;
            foreach (var h in hints)
            {
                if (!blob.Contains(h, StringComparison.OrdinalIgnoreCase))
                    continue;
                positive += 5.5d;
                hits++;
                if (hits >= 4)
                    break;
            }

            var bonus = SensorySignalBonus(sensory, key);
            if (bonus > 0)
            {
                positive += bonus;
                reasons.Add(SensoryBonusReason(key, sensory));
            }

            if (hits > 0)
                reasons.Add(HintClusterReason(key));
        }

        penalty += ComfortPenalty(blob, signalSet);
        penalty += BrightPenalty(blob, signalSet);
        penalty += FloralPenalty(blob, signalSet);

        if (penalty > 0 && reasons.Count < 4)
            reasons.Add("Softened slightly where the cup pushes past the comfort you signaled");

        positive += TagOverlapBonus(row, signalSet, reasons);

        var effective = Math.Max(1d, positive - penalty);
        DedupeReasons(reasons, 5);

        if (reasons.Count == 0)
            reasons.Add("Held for how it sits beside your selections on the list");

        return new AffinityBreakdown(positive, penalty, effective, reasons);
    }

    /// <summary>Backward-compatible scalar for callers that only need a sort key.</summary>
    public static double ScoreRow(GuestDiscoveryCurator.PoolRow row, IReadOnlyList<string> signals) =>
        Compute(row, signals, 0).Effective;

    private static double TagOverlapBonus(GuestDiscoveryCurator.PoolRow row, HashSet<string> signalSet, List<string> reasons)
    {
        var ft = (row.FlavorTags ?? "").ToLowerInvariant();
        var mt = (row.MoodTags ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ft) && string.IsNullOrWhiteSpace(mt))
            return 0d;

        var bonus = 0d;
        if (signalSet.Contains("soft_sweet") && ContainsAny(ft + " " + mt, "chocolate", "caramel", "vanilla", "sweet", "honey", "milk"))
        {
            bonus += 6d;
            reasons.Add("Flavor tags echo soft sweetness");
        }

        if (signalSet.Contains("creamy_calm") && ContainsAny(ft + " " + mt, "cream", "milk", "cocoa", "round", "velvet"))
        {
            bonus += 6d;
            reasons.Add("Tags read creamy and composed");
        }

        if (signalSet.Contains("bright_lift") && ContainsAny(ft + " " + mt, "citrus", "bright", "acid", "fruit", "clean"))
        {
            bonus += 6d;
            reasons.Add("Tags carry the lift you asked for");
        }

        if (signalSet.Contains("floral_quiet") && ContainsAny(ft + " " + mt, "floral", "tea", "jasmine", "delicate", "herbal"))
        {
            bonus += 5d;
            reasons.Add("Quiet floral or tea-like tags align");
        }

        if (signalSet.Contains("bold_depth") && ContainsAny(ft + " " + mt, "dark", "cocoa", "roast", "bold", "smoke"))
        {
            bonus += 5d;
            reasons.Add("Depth-forward tags match your appetite");
        }

        if (signalSet.Contains("slow_evening") && ContainsAny(ft + " " + mt, "comfort", "warm", "chocolate", "caramel", "night"))
        {
            bonus += 5d;
            reasons.Add("Evening comfort shows in tagged flavors");
        }

        if (signalSet.Contains("curious_edge") && ContainsAny(ft + " " + mt, "complex", "layered", "unique", "experimental", "fruit"))
        {
            bonus += 4d;
            reasons.Add("Tagged nuance fits a curious palate");
        }

        return bonus;
    }

    private static bool ContainsAny(string hay, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (hay.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static double ComfortPenalty(string blob, HashSet<string> signalSet)
    {
        var softLean = signalSet.Contains("creamy_calm") || signalSet.Contains("soft_sweet") || signalSet.Contains("slow_evening");
        if (!softLean)
            return 0d;

        var p = 0d;
        foreach (var t in ComfortConflictTokens)
        {
            if (blob.Contains(t, StringComparison.OrdinalIgnoreCase))
                p += 7d;
        }

        if (BlobHasAny(blob, "crystalline", "lifted acid", "bracing"))
            p += 5d;

        return Math.Min(p, 48d);
    }

    private static double BrightPenalty(string blob, HashSet<string> signalSet)
    {
        if (!signalSet.Contains("bright_lift"))
            return 0d;

        var p = 0d;
        foreach (var t in BrightConflictTokens)
        {
            if (blob.Contains(t, StringComparison.OrdinalIgnoreCase))
                p += 6d;
        }

        return Math.Min(p, 36d);
    }

    private static double FloralPenalty(string blob, HashSet<string> signalSet)
    {
        if (!signalSet.Contains("floral_quiet"))
            return 0d;

        var p = 0d;
        foreach (var t in FloralConflictTokens)
        {
            if (blob.Contains(t, StringComparison.OrdinalIgnoreCase))
                p += 8d;
        }

        return Math.Min(p, 32d);
    }

    private static bool BlobHasAny(string blob, params string[] tokens)
    {
        foreach (var t in tokens)
        {
            if (blob.Contains(t, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void DedupeReasons(List<string> reasons, int max)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = reasons.Count - 1; i >= 0; i--)
        {
            var r = reasons[i].Trim();
            if (r.Length == 0 || !seen.Add(r))
                reasons.RemoveAt(i);
        }

        while (reasons.Count > max)
            reasons.RemoveAt(reasons.Count - 1);
    }

    private static string HintClusterReason(string key) => key switch
    {
        "soft_sweet" => "Language on the cup carries the sweetness you leaned toward",
        "creamy_calm" => "Creamy, calm cues appear in the cup’s story",
        "bright_lift" => "Lift and clarity show in the description",
        "floral_quiet" => "Floral or tea-quiet notes surface in the text",
        "bold_depth" => "Depth and structure read in the profile",
        "slow_evening" => "Slow, warm evening language sits near this pour",
        "curious_edge" => "Layered or playful language rewards curiosity",
        _ => "Echoes one of the notes you selected"
    };

    private static string SensoryBonusReason(string key, DrinkSensoryProfile s) => key switch
    {
        "soft_sweet" => $"Sweetness axis reads {TrimAxis(s.Sweetness)} — close to your soft-sweet line",
        "creamy_calm" => $"Texture reads {TrimAxis(s.Texture)} with a rounded sip",
        "bright_lift" => $"Acidity sits {TrimAxis(s.Acidity)} — brightness without roughness",
        "floral_quiet" => $"Aromatics lean {TrimAxis(s.AromaFamily)} — delicate, as requested",
        "bold_depth" => $"Energy and body carry {TrimAxis(s.Energy)} presence",
        "slow_evening" => $"Finish and warmth resolve slowly — {TrimAxis(s.Finish)}",
        "curious_edge" => $"Structure leaves room for nuance — {TrimAxis(s.Body)} body",
        _ => "Sensory profile supports your selections"
    };

    private static string TrimAxis(string? v)
    {
        var t = (v ?? "").Trim();
        return t.Length is > 0 and <= 48 ? t : "composed";
    }

    private static double SensoryRichness(DrinkSensoryProfile s)
    {
        var n = 0;
        if (!string.IsNullOrWhiteSpace(s.Body)) n++;
        if (!string.IsNullOrWhiteSpace(s.Texture)) n++;
        if (!string.IsNullOrWhiteSpace(s.AromaFamily)) n++;
        if (!string.IsNullOrWhiteSpace(s.Finish)) n++;
        if (!string.IsNullOrWhiteSpace(s.Energy)) n++;
        return n * 1.8d;
    }

    private static double SensorySignalBonus(DrinkSensoryProfile s, string key) => key switch
    {
        "soft_sweet" when Matches(s.Sweetness, "luscious", "rounded", "restrained") => 12d,
        "creamy_calm" when Matches(s.Texture, "velvet", "satin", "syrupy") || Matches(s.Body, "round", "silky") => 14d,
        "bright_lift" when Matches(s.Acidity, "lifted", "crystalline", "balanced") || Matches(s.Energy, "lifted", "playful") => 12d,
        "floral_quiet" when Matches(s.AromaFamily, "floral", "herbal", "jasmine") || Matches(s.SocialMood, "quiet", "solitary") => 11d,
        "bold_depth" when Matches(s.Energy, "intense", "focused", "playful") || s.CaffeineIntensity >= 4 => 10d,
        "slow_evening" when Matches(s.Finish, "linger", "smoky", "cooling") || Matches(s.TemperatureEmotion, "warming", "ember") => 11d,
        "curious_edge" when Matches(s.Energy, "playful", "lifted", "intense") || Matches(s.Body, "syrupy") => 9d,
        _ => 0d
    };

    private static bool Matches(string? field, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(field))
            return false;
        var f = field.Trim().ToLowerInvariant();
        foreach (var t in tokens)
        {
            if (f.Contains(t, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string BuildSearchBlob(GuestDiscoveryCurator.PoolRow row)
    {
        var tags = $"{row.FlavorTags} {row.MoodTags}";
        return $"{row.Name} {row.TastingNotes} {row.MoodProfile} {row.Description} {row.ShortStory} {tags} {row.SensoryMerged.ToSommelierLine()}"
            .ToLowerInvariant();
    }

    public static string NormalizeSignal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var t = raw.Trim().ToLowerInvariant().Replace(' ', '_');
        return SignalKeywordHints.ContainsKey(t) ? t : "";
    }

    public static string ExplainMatch(
        GuestDiscoveryCurator.PoolRow row,
        IReadOnlyList<string> signals,
        IReadOnlyList<string> matchedHints,
        IReadOnlyList<string> matchReasons)
    {
        if (matchReasons.Count > 0)
        {
            var lead = matchReasons[0].Trim().TrimEnd('.');
            if (matchReasons.Count >= 2)
            {
                var second = matchReasons[1].Trim().TrimEnd('.');
                return $"{lead}. {second}.";
            }

            return lead + ".";
        }

        if (signals.Count == 0)
        {
            var mood = (row.MoodProfile ?? "").Trim();
            if (mood.Length is > 0 and < 80)
                return $"Chosen for how this cup carries \"{mood}\" alongside the house list.";
            var finish = (row.SensoryMerged.Finish ?? "").Trim().ToLowerInvariant();
            if (finish.Contains("linger", StringComparison.Ordinal))
                return "Selected for a soft finish that stays with you without insisting.";
            return "Held for its balance on the tray — legible, composed, and easy to return to.";
        }

        var parts = new List<string>();
        if (matchedHints.Count > 0)
            parts.Add(string.Join(" and ", matchedHints.Take(2)));
        var tex = row.SensoryMerged.Texture?.Trim();
        if (!string.IsNullOrEmpty(tex) && parts.Count < 2)
            parts.Add($"{tex} texture in the sip");
        var sweet = row.SensoryMerged.Sweetness?.Trim();
        if (signals.Any(s => NormalizeSignal(s) == "soft_sweet") && !string.IsNullOrEmpty(sweet))
            parts.Add($"{sweet} sweetness");

        if (parts.Count == 0)
        {
            var ac = row.SensoryMerged.Acidity?.Trim();
            if (!string.IsNullOrEmpty(ac))
                return $"Matched for {ac} acidity and the way this cup keeps its voice measured.";
            return "Fits the line you drew — quiet confidence in the glass.";
        }

        return "Matched for " + string.Join(", ", parts.Take(2)).TrimEnd(' ', ',') + ".";
    }

    public static List<string> CollectMatchedHints(GuestDiscoveryCurator.PoolRow row, IReadOnlyList<string> signals)
    {
        var blob = BuildSearchBlob(row);
        var found = new List<string>();
        foreach (var raw in signals)
        {
            var key = NormalizeSignal(raw);
            if (!SignalKeywordHints.TryGetValue(key, out var hints))
                continue;
            foreach (var h in hints)
            {
                if (!blob.Contains(h, StringComparison.OrdinalIgnoreCase))
                    continue;
                var label = h switch
                {
                    "citrus" or "lemon" or "yuzu" => "bright citrus",
                    "caramel" or "chocolate" or "cocoa" => "gentle cocoa depth",
                    "velvet" or "silky" => "creamy texture",
                    "floral" or "jasmine" => "floral lift",
                    "linger" => "a lingering finish",
                    "quiet" or "calm" => "calm structure",
                    _ => h + " notes"
                };
                if (!found.Contains(label, StringComparer.OrdinalIgnoreCase))
                    found.Add(label);
                if (found.Count >= 4)
                    return found;
            }
        }

        return found;
    }
}
