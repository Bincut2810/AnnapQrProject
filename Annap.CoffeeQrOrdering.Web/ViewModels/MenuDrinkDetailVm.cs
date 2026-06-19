namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed record RelatedDrinkVm(Guid Id, string Name, decimal Price, string? MoodProfile);

public sealed class MenuDrinkDetailVm
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required string CategoryName { get; init; }
    public string CardImageUrl { get; init; } = "";
    public string DetailPosterImageUrl { get; init; } = "";
    public IReadOnlyList<RelatedDrinkVm> Related { get; init; } = [];

    /// <summary>Quiet note when the cup cannot be added to the tray (pantry pause or availability hold).</summary>
    public string? ServiceNote { get; init; }

    // ── Editorial composition fields ──────────────────────────────────────────
    public string? Subtitle { get; init; }
    public string? ShortStory { get; init; }
    public string? TastingNotes { get; init; }
    public string? MoodProfile { get; init; }
    public string? FlavorTags { get; init; }
    public string? IngredientBreakdown { get; init; }
    public int? CaffeineLevel { get; init; }
    public int? SweetnessLevel { get; init; }
    public int? AcidityLevel { get; init; }
    public string? ItemType { get; init; }
    public bool IsSignature { get; init; }
    public bool IsSeasonalHighlight { get; init; }

    // ── Provenance ────────────────────────────────────────────────────────────
    public string? Origin { get; init; }
    public string? Certification { get; init; }
    public string? ProducerStory { get; init; }

    // ── Parsed helpers ────────────────────────────────────────────────────────
    public IReadOnlyList<string> FlavorTagList =>
        string.IsNullOrWhiteSpace(FlavorTags)
            ? Array.Empty<string>()
            : FlavorTags
                .Split(new[] { ',', '·', '•' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToArray();

    public IReadOnlyList<string> IngredientLines =>
        string.IsNullOrWhiteSpace(IngredientBreakdown)
            ? Array.Empty<string>()
            : IngredientBreakdown
                .Split(new[] { '|', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToArray();
}
