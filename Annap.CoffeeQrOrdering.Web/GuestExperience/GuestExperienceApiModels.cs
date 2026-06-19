namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

public sealed class GuidedSommelierRecommendRequest
{
    public List<string>? OptionIds { get; set; }
}

public sealed class GuestDiscoveryRevealRequest
{
    public Guid? VenueTableId { get; set; }

    public int RollNonce { get; set; }

    /// <summary>Guest taste rhythm keys from intake (e.g. soft_sweet, creamy_calm). Drives reflection and ranking.</summary>
    public List<string>? TasteSignals { get; set; }

    /// <summary>Which sealed envelope was opened (0–2). Perturbs the draw without a taste quiz.</summary>
    public int? ChosenEnvelopeIndex { get; set; }
}
