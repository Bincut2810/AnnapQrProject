using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Fuses pgvector order with typed flavor-graph affinity (not keyword overlap).</summary>
public static class SommelierCandidateFusion
{
    public static IReadOnlyList<SommelierMenuCandidate> Fuse(
        IReadOnlyList<SommelierMenuCandidate> vectorOrdered,
        DrinkSensoryProfile queryHints,
        DrinkSensoryProfile? previousLead,
        string? refinementKey,
        int take,
        Guid? previousLeadMenuItemId = null,
        SommelierRefinementTier refinementTier = SommelierRefinementTier.None,
        BeverageIntent? beverageIntent = null)
    {
        if (vectorOrdered.Count == 0)
            return [];

        take = Math.Clamp(take, 1, 24);
        var scored = new List<(SommelierMenuCandidate c, double score)>(vectorOrdered.Count);
        for (var i = 0; i < vectorOrdered.Count; i++)
        {
            var c = vectorOrdered[i];
            var cup = c.EffectiveSensory;
            var vec = 1.0 / (i + 4);
            var aff = FlavorAffinityEngine.ScoreHintsVsCup(queryHints, cup);
            if (beverageIntent is not null)
            {
                var profile = BeverageIntelligence.Classify(c.CategoryName, c.Name, cup);
                aff += BeverageIntelligence.SpecialtyScore(profile, beverageIntent) * 0.45;
            }
            aff += FlavorAffinityEngine.TrajectoryFromPrevious(previousLead, cup, refinementKey);

            if (previousLeadMenuItemId is Guid pid && c.Id == pid)
            {
                aff += refinementTier switch
                {
                    SommelierRefinementTier.Subtle => 5.4,
                    SommelierRefinementTier.Moderate => 2.35,
                    SommelierRefinementTier.Bold => 0.5,
                    _ => 0.95
                };
            }
            else if (previousLead is not null)
            {
                var neighbor = FlavorAffinityEngine.SensoryNeighborStepAffinity(previousLead, cup);
                aff += refinementTier switch
                {
                    SommelierRefinementTier.Bold => neighbor * 2.15,
                    SommelierRefinementTier.Moderate => neighbor * 1.38,
                    SommelierRefinementTier.Subtle => neighbor * 0.26,
                    _ => neighbor * 0.62
                };
            }

            var fusion = vec * 0.56 + Normalize(aff) * 0.44;
            scored.Add((c, fusion));
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));
        return scored.Take(take).Select(x => x.c).ToList();
    }

    private static double Normalize(double affinity) => Math.Clamp((affinity + 6) / 26.0, 0, 1);
}
