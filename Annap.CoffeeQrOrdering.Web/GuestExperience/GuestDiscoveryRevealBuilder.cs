using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Builds the discovery ritual API payload (reflection + ranked cups). Keeps orchestration out of Program.cs; CMS can replace templates later.
/// </summary>
public static class GuestDiscoveryRevealBuilder
{
    private const int MaxRecommendations = 3;

    public static object BuildResponse(
        IReadOnlyList<GuestDiscoveryCurator.PoolRow> pool,
        GuestDiscoveryRevealRequest body,
        int hourUtc,
        Func<GuestDiscoveryCurator.PoolRow, string> resolveImageUrl,
        ExperienceDiscoverySettings? discoverySettings = null,
        int chosenEnvelopeIndex = 0)
    {
        if (pool.Count == 0)
            throw new InvalidOperationException("Empty pool.");

        var tasteSignals = NormalizeSignals(body.TasteSignals);
        var envIx = chosenEnvelopeIndex is >= 0 and <= 2 ? chosenEnvelopeIndex : 0;
        var salt = HashCode.Combine(body.VenueTableId ?? Guid.Empty, body.RollNonce, hourUtc, envIx);

        var scored = pool
            .Select(row => (row, aff: GuestDiscoveryAffinityScorer.Compute(row, tasteSignals, salt)))
            .OrderByDescending(t => t.aff.Effective)
            .ThenByDescending(t => t.row.DiscoveryWeight)
            .ThenByDescending(t => t.row.IsSignature)
            .ToList();

        var top = scored.Take(MaxRecommendations).ToList();
        if (top.Count == 0)
            throw new InvalidOperationException("Empty pool.");

        var eff = top.Select(t => t.aff.Effective).ToList();
        while (eff.Count < MaxRecommendations)
            eff.Add(eff.Count > 0 ? eff[^1] * 0.92 : 1d);

        var pcts = MapCompatibilityPercents(eff[0], eff[1], eff[2], salt);

        var recommendations = new List<object>(top.Count);
        for (var i = 0; i < top.Count; i++)
        {
            var (row, aff) = top[i];
            var hints = GuestDiscoveryAffinityScorer.CollectMatchedHints(row, tasteSignals);
            var compat = pcts[i];
            var tasting = row.TastingNotes ?? row.Description ?? "";
            var story = row.ShortStory ?? "";
            var img = resolveImageUrl(row);
            var explanation = GuestDiscoveryAffinityScorer.ExplainMatch(row, tasteSignals, hints, aff.MatchReasons);
            var chips = GuestDiscoveryRitualComposer.FlavorChips(row, hints);

            recommendations.Add(new
            {
                id = row.Id,
                name = row.Name,
                price = row.Price,
                tastingNotes = tasting,
                shortStory = story,
                imageUrl = img,
                hostNote = GuestDiscoveryCurator.ComposeHostNote(row, hourUtc),
                compatibilityPercent = compat,
                palateAlignmentLabel = GuestDiscoveryRitualComposer.PalateAlignmentLabel(compat, i),
                moodAlignmentLine = GuestDiscoveryRitualComposer.MoodAlignmentLine(row),
                flavorChips = chips,
                matchExplanation = explanation,
                matchReasons = aff.MatchReasons
            });
        }

        var allowRerolls = discoverySettings?.AllowRerolls ?? true;
        var maxRerolls = allowRerolls ? 2 : 0;

        var adminCourier = discoverySettings?.CourierMoodCopy?.Trim();
        var loadingPhrase = string.IsNullOrWhiteSpace(adminCourier)
            ? GuestDiscoveryRitualComposer.PickLoadingPhrase(salt)
            : adminCourier;

        var adventureTone = discoverySettings?.AdventureTone is >= 1 and <= 5 ? discoverySettings.AdventureTone : 3;
        var letterRoom = GuestLetterRoomDesk.Resolve(discoverySettings?.LetterRoomContentJson, adventureTone);
        var letterLead = tasteSignals.Count == 0
            ? GuestLetterRoomDesk.PickInsideLine(letterRoom.InsideLetterLines, salt)
            : GuestDiscoveryRitualComposer.LeadEmotionalLine(tasteSignals);
        var letterTasting = tasteSignals.Count == 0
            ? GuestLetterRoomDesk.PickTastingBreath(salt)
            : GuestDiscoveryRitualComposer.TastingInterpretation(tasteSignals);

        return new
        {
            loadingPhrase,
            letterRoomFast = true,
            clientPacing = new { loadMs = 140, leadMs = 520, bridgeMs = 400 },
            letterRoom = GuestLetterRoomDesk.ToClientObject(letterRoom),
            reflection = new
            {
                lead = letterLead,
                paragraph = GuestDiscoveryRitualComposer.PersonalityReflection(tasteSignals),
                tastingInterpretation = letterTasting
            },
            houseRitual = new
            {
                maxRerolls,
                atmosphereLine = string.IsNullOrWhiteSpace(discoverySettings?.CourierMoodCopy)
                    ? null
                    : discoverySettings.CourierMoodCopy.Trim(),
                trustSealedLine = string.IsNullOrWhiteSpace(discoverySettings?.RevealCopyNotes)
                    ? null
                    : discoverySettings.RevealCopyNotes.Trim(),
                deferEvenLine = string.IsNullOrWhiteSpace(discoverySettings?.FatigueCopyEvenLeg)
                    ? null
                    : discoverySettings.FatigueCopyEvenLeg.Trim(),
                deferOddLine = string.IsNullOrWhiteSpace(discoverySettings?.FatigueCopyOddLeg)
                    ? null
                    : discoverySettings.FatigueCopyOddLeg.Trim(),
                refusalLines = letterRoom.RefusalLines
            },
            recommendations
        };
    }

    /// <summary>
    /// Maps effective scores to guest-facing percents with clear separation: top 92–98, second 72–91, third 62–80.
    /// </summary>
    internal static int[] MapCompatibilityPercents(double e0, double e1, double e2, int salt)
    {
        var d01 = Math.Max(0, e0 - e1);
        var d12 = Math.Max(0, e1 - e2);
        var span = Math.Max(1e-6, e0 - e2);
        var r01 = Math.Clamp(d01 / span, 0, 1);
        var r12 = Math.Clamp(d12 / span, 0, 1);

        var micro = (Math.Abs(salt) % 5) - 2;

        var p0 = (int)Math.Round(92 + Math.Min(6, 4 * r01 + micro * 0.25));
        var p1 = (int)Math.Round(74 + Math.Min(16, 10 * r12 + 6 * (1 - r01) * 0.35 + micro * 0.2));
        var p2 = (int)Math.Round(62 + Math.Min(16, 8 * r12 + 4 * (1 - r12) + micro * 0.15));

        p0 = Math.Clamp(p0, 92, 98);
        p1 = Math.Clamp(p1, 72, 91);
        p2 = Math.Clamp(p2, 62, 80);

        if (p0 - p1 < 5)
            p1 = p0 - 5;
        if (p1 - p2 < 5)
            p2 = p1 - 5;

        p1 = Math.Clamp(p1, 72, 91);
        p2 = Math.Clamp(p2, 62, 80);
        if (p0 - p1 < 5)
            p0 = Math.Min(98, p1 + 5);

        return [p0, p1, p2];
    }

    private static List<string> NormalizeSignals(IReadOnlyList<string>? raw)
    {
        if (raw is null || raw.Count == 0)
            return [];
        var list = new List<string>();
        foreach (var r in raw)
        {
            var n = GuestDiscoveryAffinityScorer.NormalizeSignal(r);
            if (n.Length == 0)
                continue;
            if (!list.Contains(n, StringComparer.OrdinalIgnoreCase))
                list.Add(n);
        }

        return list;
    }
}
