namespace Annap.CoffeeQrOrdering.Application;

/// <summary>Guest-facing pairing note (OpenAI RAG when configured; otherwise simulated sommelier).</summary>
public sealed class SommelierSuggestion
{
    public Guid? MenuItemId { get; init; }
    public required string Recommendation { get; init; }

    /// <summary>Editorial opening line—sommelier voice, not Q&amp;A.</summary>
    public string? OpeningLetter { get; init; }

    public required string TastingNotes { get; init; }
    public required string EmotionalTone { get; init; }
    public required string Reason { get; init; }

    /// <summary>Quiet follow-up refinement (e.g. where to wander next on the tray).</summary>
    public string? FollowUpRefinement { get; init; }

    /// <summary>Best-matching flavor tag for subtle UI (e.g. floral, refreshing).</summary>
    public string? SenseTag { get; init; }

    /// <summary>Other cups from the same grounded set—never fabricated SKUs.</summary>
    public IReadOnlyList<SommelierAlternativeCup> Alternatives { get; init; } = [];
}
