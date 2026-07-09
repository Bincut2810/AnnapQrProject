using System.Text.RegularExpressions;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static partial class BankTransferMemoMatcher
{
    public static string NormalizeMemo(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
            return "";

        var collapsed = WhitespaceCollapse().Replace(memo.Trim(), " ");
        return collapsed.ToUpperInvariant();
    }

    public static bool MemoMatches(string? incomingMemo, string expectedMemo)
    {
        var incoming = NormalizeMemo(incomingMemo);
        var expected = NormalizeMemo(expectedMemo);
        if (string.IsNullOrEmpty(expected))
            return false;
        if (incoming == expected)
            return true;
        return incoming.Contains(expected, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceCollapse();
}
