namespace Annap.CoffeeQrOrdering.Web.ViewModels;

/// <summary>JSON payload for client-side <see cref="DrinkDetailRenderer"/>.</summary>
public sealed class DrinkDetailRendererDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required decimal Price { get; init; }
    public required string PriceDisplay { get; init; }
    // ── Real CSV fields ───────────────────────────────────────────────────────
    /// <summary>Nguyên liệu — rendered as "Inside the glass".</summary>
    public string? IngredientBreakdown { get; init; }
    /// <summary>Nguồn gốc / Xuất xứ — rendered as "Sourced from" (null when unknown).</summary>
    public string? Origin { get; init; }
    /// <summary>Vị — rendered as "On the palate".</summary>
    public string? TastingNotes { get; init; }
    /// <summary>Bar explanation — Origin Letter body.</summary>
    public string? ShortStory { get; init; }
    /// <summary>Producer witness — Origin Letter.</summary>
    public string? ProducerStory { get; init; }
    public string? Subtitle { get; init; }
    public bool IsSignature { get; init; }

    // ── Runtime fields ────────────────────────────────────────────────────────
    public string? ServingNotes { get; init; }
    public required string AccentColor { get; init; }
    public string? CategoryName { get; init; }
    public bool CanAdd { get; init; } = true;
    public string? ServiceNote { get; init; }
    public bool IsBakery { get; init; }
    public IReadOnlyList<PairingSuggestionDto> Pairings { get; init; } = Array.Empty<PairingSuggestionDto>();
}
