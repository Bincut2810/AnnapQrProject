using Annap.CoffeeQrOrdering.Domain.Common;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class MenuItem : AuditableEntity
{
    public Guid CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>Optional stable key from the owner catalog export (slug); used for idempotent imports.</summary>
    public string? CatalogKey { get; set; }

    /// <summary>Optional single glyph or emoji for cards (owner catalog / QR menu).</summary>
    public string? IconGlyph { get; set; }

    /// <summary>Loose drink “type” from the house catalog (e.g. hot, iced, ritual) — display + filters.</summary>
    public string? ItemType { get; set; }

    /// <summary>Short line under the name on cards and detail (e.g. origin, format).</summary>
    public string? Subtitle { get; set; }

    public string? Description { get; set; }

    /// <summary>Specialty tasting language (acidity, sweetness, texture, finish).</summary>
    public string? TastingNotes { get; set; }

    /// <summary>Short mood line, e.g. “Bright · Restorative”.</summary>
    public string? MoodProfile { get; set; }

    /// <summary>One or two editorial lines—origin story, ritual, or sensory scene.</summary>
    public string? ShortStory { get; set; }

    /// <summary>Line- or pipe-separated ingredient / process notes for the guest-facing breakdown.</summary>
    public string? IngredientBreakdown { get; set; }

    /// <summary>Relative caffeine impression, 1 (gentle)–5 (assertive).</summary>
    public int? CaffeineLevel { get; set; }

    /// <summary>Perceived sweetness, 1 (dry)–5 (luscious).</summary>
    public int? SweetnessLevel { get; set; }

    /// <summary>Perceived acidity / brightness, 1 (round)–5 (luminous).</summary>
    public int? AcidityLevel { get; set; }

    public decimal Price { get; set; }

    /// <summary>Lower sorts first within a category (guest menu, QR rails).</summary>
    public int DisplaySortOrder { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>When true, hidden from guest menu and ordering; retained for the line sheet.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Surface on home or curated rails.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>House signature spotlight (home, storytelling).</summary>
    public bool IsSignature { get; set; }

    /// <summary>Seasonal or microlot emphasis.</summary>
    public bool IsSeasonalHighlight { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Web-relative path to the fullscreen detail poster image (admin upload). Falls back to ImageUrl when absent.</summary>
    public string? DetailPosterImagePath { get; set; }

    /// <summary>Relative weight in the house discovery ritual (0 = excluded from weighted pool).</summary>
    public decimal DiscoveryWeight { get; set; } = 1m;

    /// <summary>When true, excluded from discovery selection (still on line sheet).</summary>
    public bool IsHiddenDiscovery { get; set; }

    /// <summary>When false, excluded from the random discovery pool regardless of weight.</summary>
    public bool IsDiscoveryEligible { get; set; } = true;

    /// <summary>Short discovery-specific story line on the reveal card.</summary>
    public string? DiscoveryStory { get; set; }

    /// <summary>Guest-facing editorial for experience cards (future i18n key or prose).</summary>
    public string? StoryCopy { get; set; }

    /// <summary>Coffee origin — country, region, or farm. E.g., "Đà Lạt", "Ethiopia Yirgacheffe".</summary>
    public string? Origin { get; set; }

    /// <summary>Certification claims, e.g., "Organic · Direct Trade".</summary>
    public string? Certification { get; set; }

    /// <summary>Short producer or processing story for editorial composition and guest storytelling.</summary>
    public string? ProducerStory { get; set; }

    /// <summary>Comma-separated mood tags for search / future AI tuning.</summary>
    public string? MoodTags { get; set; }

    /// <summary>Comma-separated flavor tags for search / future AI tuning.</summary>
    public string? FlavorTags { get; set; }

    /// <summary>Structured sensory axes (body, acidity, social mood, etc.) for retrieval fusion and continuity.</summary>
    public DrinkSensoryProfile SensoryProfile { get; set; } = new();

    public List<MenuItemIngredient> RecipeLines { get; set; } = [];

    // Foundation for future AI recommendations (stored via pgvector in Infrastructure).
    public EmbeddingVector? Embedding { get; set; }
    public string? EmbeddingModel { get; set; }
}

