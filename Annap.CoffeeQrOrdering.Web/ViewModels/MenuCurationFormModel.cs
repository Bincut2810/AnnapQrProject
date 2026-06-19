using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Annap.CoffeeQrOrdering.Web.ModelBinding;

namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed class MenuCurationFormModel
{
    // ── Core identity ─────────────────────────────────────────────────────────
    [Display(Name = "Drink name")]
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(240)]
    public string? Subtitle { get; set; }

    // ── Menu position ─────────────────────────────────────────────────────────
    [Required]
    public Guid CategoryId { get; set; }

    [Range(0.01, 999999.99)]
    [ModelBinder(BinderType = typeof(FlexibleDecimalModelBinder))]
    public decimal Price { get; set; }

    /// <summary>Lower numbers appear first within a category on the guest menu.</summary>
    [Range(0, 9999)]
    public int DisplaySortOrder { get; set; }

    // ── Guest-facing copy ─────────────────────────────────────────────────────
    [StringLength(800)]
    public string? TastingNotes { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(800)]
    public string? ShortStory { get; set; }

    // ── Sensory & flavor ──────────────────────────────────────────────────────
    [StringLength(200)]
    public string? MoodProfile { get; set; }

    [StringLength(400)]
    public string? FlavorTags { get; set; }

    [StringLength(800)]
    public string? IngredientBreakdown { get; set; }

    [StringLength(100)]
    public string? ItemType { get; set; }

    [Range(1, 5)]
    public int? CaffeineLevel { get; set; }

    [Range(1, 5)]
    public int? SweetnessLevel { get; set; }

    [Range(1, 5)]
    public int? AcidityLevel { get; set; }

    // ── Provenance ────────────────────────────────────────────────────────────
    [StringLength(200)]
    public string? Origin { get; set; }

    [StringLength(200)]
    public string? Certification { get; set; }

    [StringLength(800)]
    public string? ProducerStory { get; set; }

    // ── Curation flags ────────────────────────────────────────────────────────
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsSignature { get; set; }
    public bool IsSeasonalHighlight { get; set; }

    // ── Discovery ─────────────────────────────────────────────────────────────
    [Range(0, 10)]
    [ModelBinder(BinderType = typeof(FlexibleDecimalModelBinder))]
    public decimal DiscoveryWeight { get; set; } = 1m;

    public bool IsDiscoveryEligible { get; set; } = true;

    // ── Images ───────────────────────────────────────────────────────────────
    /// <summary>Clear stored hero URL and delete uploaded file when applicable.</summary>
    public bool RemoveHeroImage { get; set; }

    public IFormFile? HeroImage { get; set; }

    /// <summary>Clear stored detail poster and delete uploaded file when applicable.</summary>
    public bool RemoveDetailPosterImage { get; set; }

    public IFormFile? DetailPosterImage { get; set; }

    // ── Normalisation ─────────────────────────────────────────────────────────
    /// <summary>
    /// Trims all string fields and converts whitespace-only values to null.
    /// Call before model validation so validation errors reflect cleaned values.
    /// </summary>
    public void Normalize()
    {
        Name                = Name.Trim();
        Subtitle            = NullIfWhite(Subtitle);
        TastingNotes        = NullIfWhite(TastingNotes);
        Description         = NullIfWhite(Description);
        ShortStory          = NullIfWhite(ShortStory);
        MoodProfile         = NullIfWhite(MoodProfile);
        FlavorTags          = NullIfWhite(FlavorTags);
        IngredientBreakdown = NullIfWhite(IngredientBreakdown);
        ItemType            = NullIfWhite(ItemType);
        Origin              = NullIfWhite(Origin);
        Certification       = NullIfWhite(Certification);
        ProducerStory       = NullIfWhite(ProducerStory);
    }

    private static string? NullIfWhite(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
