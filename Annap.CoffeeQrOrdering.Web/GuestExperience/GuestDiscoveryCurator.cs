using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Quiet, deterministic "house choice" for discovery — weighted like a sommelier tray, never exposed as randomness.
/// </summary>
public static class GuestDiscoveryCurator
{
    public sealed record PoolRow(
        Guid Id,
        string Name,
        decimal Price,
        string? TastingNotes,
        string? ShortStory,
        string? MoodProfile,
        string? Description,
        string CategoryName,
        string? ImageUrl,
        string? DetailPosterImagePath,
        string? FlavorTags,
        string? MoodTags,
        bool IsSignature,
        bool IsFeatured,
        bool IsSeasonalHighlight,
        DateTimeOffset? UpdatedAtUtc,
        DrinkSensoryProfile SensoryMerged,
        decimal DiscoveryWeight);

    public sealed record RevealResult(PoolRow Row, string HostNote);

    public static RevealResult Select(
        IReadOnlyList<PoolRow> pool,
        Guid? venueTableId,
        int rollNonce,
        DateTime utcNow)
    {
        if (pool.Count == 0)
            throw new InvalidOperationException("Empty pool.");

        var hour = utcNow.Hour;
        var weights = new int[pool.Count];
        for (var i = 0; i < pool.Count; i++)
            weights[i] = ComputeWeight(pool[i], hour, rollNonce, i);

        var total = 0L;
        for (var i = 0; i < weights.Length; i++)
            total += weights[i];
        if (total <= 0)
            total = pool.Count;

        var seed = HashCode.Combine(venueTableId ?? Guid.Empty, rollNonce, utcNow.DayOfYear, unchecked((int)(utcNow.Ticks >> 16)));
        var u = (uint)((seed & 0x7FFFFFFF) == 0 ? 1 : seed & 0x7FFFFFFF);
        var pick = 0;
        var rPick = (int)(u % (ulong)total);
        for (var i = 0; i < weights.Length; i++)
        {
            rPick -= weights[i];
            if (rPick < 0)
            {
                pick = i;
                break;
            }
        }

        var row = pool[pick];
        var note = ComposeHostNote(row, hour);
        return new RevealResult(row, note);
    }

    private static int ComputeWeight(PoolRow r, int hourUtc, int nonce, int indexInPool)
    {
        var w = 100;
        if (r.IsSignature)
            w += 240;
        if (r.IsSeasonalHighlight)
            w += 160;
        if (r.IsFeatured)
            w += 100;

        var blob = $"{r.Name} {r.TastingNotes} {r.MoodProfile} {r.Description} {r.SensoryMerged.ToSommelierLine()}".ToLowerInvariant();

        if (hourUtc is >= 17 and <= 22)
        {
            if (ContainsAny(blob, "warm", "cocoa", "comfort", "velvet", "round", "linger", "ember", "gentle", "soft", "caramel", "milk"))
                w += 85;
        }
        else if (hourUtc is >= 5 and < 12)
        {
            if (ContainsAny(blob, "bright", "citrus", "clean", "lift", "crystalline", "morning", "fresh", "acid"))
                w += 75;
        }
        else if (hourUtc is >= 12 and < 17)
        {
            if (ContainsAny(blob, "balanced", "focus", "still", "tea", "quiet"))
                w += 45;
        }
        else
        {
            if (ContainsAny(blob, "quiet", "still", "solitary", "hush", "soft", "delicate"))
                w += 70;
        }

        // Gentle "rotation" so repeats feel considered, not slot-like (tiny, bounded jitter).
        var jitter = ((nonce * 13) ^ indexInPool ^ r.Id.GetHashCode()) & 0x1F;
        w += jitter;

        // Calm preference for cups that have not been edited today (feels "settled" on the list).
        if (r.UpdatedAtUtc is { } u)
        {
            var days = (DateTimeOffset.UtcNow - u).TotalDays;
            if (days > 45)
                w += 18;
            else if (days > 10)
                w += 8;
        }

        var dw = (double)Math.Clamp(r.DiscoveryWeight, 0.05m, 6m);
        w = (int)Math.Round(w * dw);

        return Math.Clamp(w, 48, 920);
    }

    private static bool ContainsAny(string hay, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (hay.Contains(n, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Editorial language only — never mentions weighting, algorithms, or chance.</summary>
    public static string ComposeHostNote(PoolRow r, int hourUtc)
    {
        var mood = (r.MoodProfile ?? "").Trim();
        if (mood.Length > 0 && mood.Length < 72)
        {
            if (hourUtc is >= 17 and <= 22)
                return $"Chosen while the room turns inward — your table's mood reads \"{mood}\", and this cup follows that line.";
            if (hourUtc is >= 5 and < 12)
                return $"Selected as the morning stays clear — \"{mood}\" is the note we heard when the list closed.";
            return $"Held for this moment because \"{mood}\" sat closest to what the house imagined for you.";
        }

        var finish = (r.SensoryMerged.Finish ?? "").Trim().ToLowerInvariant();
        var energy = (r.SensoryMerged.Energy ?? "").Trim().ToLowerInvariant();

        if (hourUtc is >= 17 and <= 22)
        {
            if (finish.Contains("linger", StringComparison.Ordinal) || energy == "still" || energy == "focused")
                return "Chosen for a slower evening pace and a softer finish — the kind of pour that lets conversation stay unhurried.";
            return "Chosen while the evening asks for warmth without spectacle — a quiet anchor on the tray.";
        }

        if (hourUtc is >= 5 and < 12)
            return "Selected as the day opens — structure first, sweetness patient, nothing loud in the glass.";

        if (hourUtc is >= 12 and < 17)
            return "Placed here for the in-between hours — balanced, legible, and easy to return to after a walk outside.";

        return "Offered now, when the room is hushed — a cup that keeps its voice low and its finish long.";
    }
}
