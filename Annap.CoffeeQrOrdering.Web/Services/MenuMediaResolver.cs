using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Guest-facing image pipeline — local Annap drink assets only. No remote URLs or demo placeholders.
/// Menu cards prefer lightweight WebP thumbs; detail views use full poster/card WebP.
/// </summary>
public static class MenuMediaResolver
{
    private static DrinkAssetResolver? _assets;
    private static Func<string, bool>? _webRootFileExists;

    public static void BindAssetResolver(DrinkAssetResolver resolver) => _assets = resolver;

    public static void BindWebRootFileExists(Func<string, bool> exists) => _webRootFileExists = exists;

    public static string? TryResolveCardImageUrl(
        string? cardImageUrl,
        string? heroImageUrl,
        string? imageUrl,
        string? thumbnailUrl,
        string drinkName,
        string categoryName)
    {
        var sawManagedUpload = false;
        foreach (var raw in new[] { thumbnailUrl, cardImageUrl, heroImageUrl, imageUrl })
        {
            if (IsManagedUploadUrl(raw))
                sawManagedUpload = true;

            var resolved = ResolveManagedCardUrl(raw);
            if (resolved is not null)
                return resolved;
        }

        if (sawManagedUpload)
            return null;

        return _assets?.ResolveWebUrl(categoryName, drinkName);
    }

    public static string? TryResolveCardImageUrl(MenuItem item, string categoryName) =>
        TryResolveCardImageUrl(null, null, item.ImageUrl, null, item.Name, categoryName);

    public static string? TryResolveDetailPosterUrl(
        string? detailPosterImagePath,
        string? imageUrl,
        string drinkName,
        string categoryName)
    {
        var sawManagedUpload = false;
        if (IsAllowedLocalUrl(detailPosterImagePath))
        {
            var poster = NormalizeWebRelative(detailPosterImagePath!);
            if (MenuImagePaths.IsManagedUrl(poster))
            {
                sawManagedUpload = true;
                poster = MenuImagePaths.NormalizeToWebpUrl(poster) ?? poster;
            }
            if (WebRootExists(poster))
                return poster;
        }

        if (IsAllowedLocalUrl(imageUrl))
        {
            var hero = NormalizeWebRelative(imageUrl!);
            if (MenuImagePaths.IsManagedUrl(hero))
            {
                sawManagedUpload = true;
                hero = MenuImagePaths.NormalizeToWebpUrl(hero) ?? hero;
            }
            if (WebRootExists(hero))
                return hero;
        }

        if (sawManagedUpload)
            return null;

        return _assets?.ResolveWebUrl(categoryName, drinkName);
    }

    /// <summary>Legacy callers — returns empty string when no local asset (use <see cref="TryResolveCardImageUrl"/> in new code).</summary>
    public static string ResolveCardImageUrl(
        string? cardImageUrl,
        string? heroImageUrl,
        string? imageUrl,
        string? thumbnailUrl,
        string drinkName,
        string categoryName) =>
        TryResolveCardImageUrl(cardImageUrl, heroImageUrl, imageUrl, thumbnailUrl, drinkName, categoryName) ?? "";

    public static string ResolveCardImageUrl(MenuItem item, string categoryName) =>
        ResolveCardImageUrl(null, null, item.ImageUrl, null, item.Name, categoryName);

    public static string ResolveDetailPosterUrl(
        string? detailPosterImagePath,
        string? imageUrl,
        string drinkName,
        string categoryName) =>
        TryResolveDetailPosterUrl(detailPosterImagePath, imageUrl, drinkName, categoryName) ?? "";

    public static bool HasLocalImage(string? url) => IsAllowedLocalUrl(url);

    public static string NormalizeWebRelative(string url)
    {
        var t = url.Trim();
        if (t.Length == 0)
            return t;
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return "";
        if (t.StartsWith("/", StringComparison.Ordinal))
            return t;
        return "/" + t.TrimStart('/');
    }

    private static bool IsAllowedLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var t = url.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool IsManagedUploadUrl(string? url)
    {
        if (!IsAllowedLocalUrl(url))
            return false;
        return MenuImagePaths.IsManagedUrl(NormalizeWebRelative(url!.Trim()));
    }

    private static bool IsRuntimeRasterUrl(string url)
    {
        var ext = Path.GetExtension(url);
        return string.Equals(ext, MenuImagePaths.WebpExtension, StringComparison.OrdinalIgnoreCase)
               || string.Equals(ext, ".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveManagedCardUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        var normalized = NormalizeWebRelative(t);
        if (!MenuImagePaths.IsManagedUrl(normalized))
            return IsRuntimeRasterUrl(normalized) ? normalized : null;

        normalized = MenuImagePaths.NormalizeToWebpUrl(normalized) ?? normalized;

        var thumb = MenuImagePaths.DeriveThumbUrl(normalized);
        if (thumb is not null && WebRootExists(thumb))
            return thumb;

        return WebRootExists(normalized) ? normalized : null;
    }

    private static bool WebRootExists(string webRelative)
    {
        return _webRootFileExists?.Invoke(webRelative) ?? true;
    }
}
