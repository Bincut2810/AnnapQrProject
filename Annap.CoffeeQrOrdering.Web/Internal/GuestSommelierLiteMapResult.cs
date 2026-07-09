namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Outcome of mapping slim QR preferences to guided sommelier <c>optionIds</c>.</summary>
public sealed class GuestSommelierLiteMapResult
{
    public bool Success { get; init; }

    /// <summary>False when the live catalog cannot satisfy AI Lite (missing q1–q4 or required options).</summary>
    public bool CatalogCompatible { get; init; } = true;

    public IReadOnlyList<string> OptionIds { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static GuestSommelierLiteMapResult Ok(IReadOnlyList<string> optionIds)
        => new()
        {
            Success = true,
            CatalogCompatible = true,
            OptionIds = optionIds
        };

    public static GuestSommelierLiteMapResult CatalogIncompatible(string? reasonCode, string? message = null)
        => new()
        {
            Success = false,
            CatalogCompatible = false,
            ErrorCode = reasonCode ?? "catalog_incompatible",
            ErrorMessage = message
        };

    public static GuestSommelierLiteMapResult InvalidPreference(string? message = null)
        => new()
        {
            Success = false,
            CatalogCompatible = true,
            ErrorCode = "invalid_preference",
            ErrorMessage = message
        };
}
