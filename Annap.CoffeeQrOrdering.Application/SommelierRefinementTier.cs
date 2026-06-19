namespace Annap.CoffeeQrOrdering.Application;

/// <summary>How strongly a refinement chip should move the tray vs reinterpret the current lead.</summary>
public enum SommelierRefinementTier : byte
{
    None = 0,
    /// <summary>Prefer same lead; evolve language and neighbors in alternatives.</summary>
    Subtle = 1,
    /// <summary>Gentle sensory steps from the prior cup are welcome.</summary>
    Moderate = 2,
    /// <summary>Encourage justified moves toward adjacent profiles.</summary>
    Bold = 3
}

public static class SommelierRefinementTierMapper
{
    /// <summary>Maps refinement chip ids to intensity (see ritual chips on the guest UI).</summary>
    public static SommelierRefinementTier FromRefinementKey(string? refinementKey)
    {
        if (string.IsNullOrWhiteSpace(refinementKey))
            return SommelierRefinementTier.None;
        return refinementKey.Trim().ToLowerInvariant() switch
        {
            "softer" or "brighter" => SommelierRefinementTier.Subtle,
            "less_sweet" or "warmer" => SommelierRefinementTier.Moderate,
            "low_caffeine" or "more_adventurous" => SommelierRefinementTier.Bold,
            _ => SommelierRefinementTier.Moderate
        };
    }
}
