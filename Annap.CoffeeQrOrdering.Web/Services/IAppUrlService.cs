namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Resolves the public base URL for QR payloads, absolute links, and browser runtime config.</summary>
public interface IAppUrlService
{
    /// <summary>Origin without trailing slash: DB override → appsettings AppUrl:PublicBaseUrl → current request.</summary>
    string GetBaseUrl(HttpContext? httpContext = null);

    /// <summary>Combines <see cref="GetBaseUrl"/> with a path beginning with <c>/</c>.</summary>
    string BuildAbsoluteUrl(string relativePath, HttpContext? httpContext = null);
}
