namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Runtime capability for AI Sommelier Lite (not a render gate).</summary>
public enum SommelierLiteUiState
{
    Hidden,
    /// <summary>CTA visible; <see cref="GuestSommelierLiteCompatibilityResult.IsCompatible"/> may still be false.</summary>
    Offered
}

public static class GuestSommelierLiteGating
{
    /// <summary>Show secondary AI CTA on seated slim arrival when CMS sommelier is enabled.</summary>
    public static bool ShowCta(bool showSlimArrival, bool sommelierEnabled)
        => showSlimArrival && sommelierEnabled;

    public static SommelierLiteUiState Resolve(bool showSlimArrival, bool sommelierEnabled)
        => ShowCta(showSlimArrival, sommelierEnabled) ? SommelierLiteUiState.Offered : SommelierLiteUiState.Hidden;
}
