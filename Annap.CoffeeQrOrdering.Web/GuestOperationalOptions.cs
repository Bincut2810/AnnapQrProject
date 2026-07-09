namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Café operational tuning for seated QR guests (not dev diagnostics).
/// </summary>
public sealed class GuestOperationalOptions
{
    public const string SectionName = "GuestOperational";

    /// <summary>
    /// When true, the seated arrival screen shows a primary server-rendered link to the menu
    /// before optional experiences. Ordering works without experiential JS.
    /// </summary>
    public bool MenuFirstArrival { get; set; } = true;

    /// <summary>
    /// When true, seated arrival applies <c>ge-root--calm</c> to shorten heavy CSS animations
    /// (better on low-end phones and in-app browsers). Does not remove optional flows.
    /// </summary>
    public bool CalmArrivalAnimations { get; set; } = true;

    /// <summary>
    /// When true, seated QR uses the slim arrival shell instead of the ritual experience.
    /// Default false — ritual AI Sommelier remains the seated arrival path.
    /// </summary>
    public bool UseSlimQrArrival { get; set; }

    /// <summary>
    /// When true, seated slim QR arrival may offer AI Sommelier Lite (ordering assist).
    /// Independent of homepage ritual sommelier CMS toggle.
    /// </summary>
    public bool SommelierLiteOnSeatedQr { get; set; } = true;
}
