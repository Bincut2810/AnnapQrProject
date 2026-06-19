namespace Annap.CoffeeQrOrdering.Application;

/// <summary>Stable catalog keys and category name for the four flagship specialty coffees.</summary>
public static class AnnapSpecialtyCoffeeCatalog
{
    public const string CategoryName = "Specialty Coffee";

    public const string DufatanyeKey = "03732";
    public const string AbateranankungaKey = "03734";
    public const string RiftValleyKey = "03757";
    public const string NigussieKey = "03028";

    public static readonly string[] ProtectedCatalogKeys =
    [
        DufatanyeKey,
        AbateranankungaKey,
        RiftValleyKey,
        NigussieKey
    ];

    public static bool IsProtectedCatalogKey(string? catalogKey) =>
        !string.IsNullOrWhiteSpace(catalogKey)
        && ProtectedCatalogKeys.Contains(catalogKey.Trim(), StringComparer.OrdinalIgnoreCase);
}
