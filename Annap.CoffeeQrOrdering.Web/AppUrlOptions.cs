namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Public site URL for QR codes, absolute links, and <c>window.__annapRuntime</c> (appsettings:AppUrl).</summary>
public sealed class AppUrlOptions
{
    public const string SectionName = "AppUrl";

    /// <summary>When non-empty, used as the public origin (no trailing slash). When empty, IAppUrlService uses the current HTTP request; Development may post-configure a LAN IPv4.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
