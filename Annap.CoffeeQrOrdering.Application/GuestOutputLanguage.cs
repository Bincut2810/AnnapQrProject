namespace Annap.CoffeeQrOrdering.Application;

/// <summary>Guest-facing copy language for sommelier and ritual UI (not full site localization).</summary>
public static class GuestOutputLanguage
{
    public const string English = "en";
    public const string Vietnamese = "vi";

    public static string Normalize(string? raw) =>
        string.Equals(raw?.Trim(), Vietnamese, StringComparison.OrdinalIgnoreCase)
            ? Vietnamese
            : English;

    public static bool IsVietnamese(string? lang) => Normalize(lang) == Vietnamese;
}
