namespace Annap.CoffeeQrOrdering.Application;

/// <summary>
/// Sommelier input split so pgvector retrieval stays on a short semantic line while
/// completion can carry session continuity without bloating embeddings.
/// </summary>
public sealed record SommelierGuideRequest(
    /// <summary>Compact line for embedding / keyword retrieval (mood + current refinement vector).</summary>
    string SemanticQuery,
    /// <summary>What the guest is asking for this beat (mood + refinement text).</summary>
    string GuestLine,
    /// <summary>Optional memory from the current table sitting; informs tone without becoming a chat log.</summary>
    string? SessionContinuity,
    /// <summary>Isolates RAG cache entries per sitting so the same words do not replay a frozen letter.</summary>
    Guid? SessionId = null,
    /// <summary>Stable refinement chip id when the guest tapped a chip (e.g. less_sweet).</summary>
    string? RefinementKey = null,
    /// <summary>Lead cup name from the prior beat—lets simulated and completion paths evolve gently.</summary>
    string? PreviousLeadName = null,
    /// <summary>Mood pill key (bright, slow, …) for sensory target inference.</summary>
    string? MoodKey = null,
    /// <summary>Prior recommendation menu id for sensory trajectory and fusion.</summary>
    Guid? PreviousLeadMenuItemId = null,
    /// <summary>Session flavor-drift summary (e.g. softer / less sweet) for sensory hints.</summary>
    string? FlavorDirectionHint = null,
    /// <summary>Chip-driven intensity: subtle reinterpretation vs gradual vs bolder neighbor moves.</summary>
    SommelierRefinementTier RefinementTier = SommelierRefinementTier.None,
    /// <summary>Number of refinement chips recorded this sitting (incl. current); nudges variety in alts/copy.</summary>
    int? SessionRefinementDepth = null,
    /// <summary>Guest copy language: <c>en</c> or <c>vi</c> (Vietnamese must be culturally written, not literal translation).</summary>
    string OutputLanguage = GuestOutputLanguage.English,
    /// <summary>Hard beverage-family lock: coffee, tea, juice, smoothie, matcha, fruit, or signature.</summary>
    string? BeverageFamilyKey = null);
