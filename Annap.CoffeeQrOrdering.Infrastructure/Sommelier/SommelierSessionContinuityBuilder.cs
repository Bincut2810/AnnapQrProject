using System.Text;
using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

public static class SommelierSessionContinuityBuilder
{
    public static string BuildContinuityBlock(SommelierSessionState s, string? outputLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(s.PreviousLeadName) && s.MoodKeys.Count == 0 && s.RefinementKeys.Count == 0)
            return "";

        return GuestOutputLanguage.IsVietnamese(outputLanguage)
            ? BuildContinuityBlockVi(s)
            : BuildContinuityBlockEn(s);
    }

    private static string BuildContinuityBlockEn(SommelierSessionState s)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("SESSION (same sitting—evolve; do not reset as if first meeting):");
        if (!string.IsNullOrWhiteSpace(s.PreviousLeadName))
        {
            sb.Append("- Previous lead: ").Append(s.PreviousLeadName);
            if (s.PreviousLeadMenuItemId is Guid id)
                sb.Append(" (").Append(id.ToString("N")[..8]).Append("…)");
            sb.AppendLine();
        }

        if (s.MoodLabels.Count > 0)
            sb.Append("- Moods visited: ").AppendLine(string.Join(" → ", s.MoodLabels.TakeLast(5)));

        if (s.RefinementKeys.Count > 0)
            sb.Append("- Refinement path: ").AppendLine(string.Join(" → ", s.RefinementKeys.TakeLast(8)));

        if (!string.IsNullOrWhiteSpace(s.FlavorDirection))
            sb.Append("- Flavor drift: ").AppendLine(s.FlavorDirection);

        sb.AppendLine("Continue this arc in your opening and reasoning; acknowledge the shift quietly.");
        return sb.ToString().TrimEnd();
    }

    private static string BuildContinuityBlockVi(SommelierSessionState s)
    {
        var sb = new StringBuilder(640);
        sb.AppendLine("Buổi ngồi này—cùng một nhịp, xin đừng mở lại như lần đầu ghé:");
        if (!string.IsNullOrWhiteSpace(s.PreviousLeadName))
        {
            sb.Append("- Ly vừa rồi: ").Append(s.PreviousLeadName);
            if (s.PreviousLeadMenuItemId is Guid id)
                sb.Append(" (").Append(id.ToString("N")[..8]).Append("…)");
            sb.AppendLine();
        }

        if (s.MoodLabels.Count > 0)
            sb.Append("- Những tông bạn đã chạm: ").AppendLine(string.Join(" → ", s.MoodLabels.TakeLast(5)));

        if (s.RefinementKeys.Count > 0)
        {
            sb.Append("- Nhịp chỉnh nhỏ: ");
            sb.AppendLine(string.Join(" → ", s.RefinementKeys.TakeLast(8).Select(RefinementKeyVi)));
        }

        var driftVi = RecomputeFlavorDirectionVi(s);
        if (!string.IsNullOrWhiteSpace(driftVi))
            sb.Append("- Dòng vị đang dời: ").AppendLine(driftVi);

        sb.AppendLine("Giữ nhịp trong lời mở và lý do; ghi nhận thay đổi thật nhẹ.");
        return sb.ToString().TrimEnd();
    }

    private static string RefinementKeyVi(string key)
    {
        return key.Trim().ToLowerInvariant() switch
        {
            "softer" => "êm thêm",
            "brighter" => "mở và thanh hơn",
            "less_sweet" => "khô và trong hơn",
            "more_adventurous" => "dấn thêm",
            "low_caffeine" => "caffeine êm hơn",
            "warmer" => "ôm ấm thêm",
            _ => key
        };
    }

    /// <summary>Vietnamese drift line for session memory (not used for English sensory hints).</summary>
    public static string? RecomputeFlavorDirectionVi(SommelierSessionState state)
    {
        var parts = new HashSet<string>();
        foreach (var k in state.RefinementKeys)
        {
            if (k is "softer" or "less_sweet")
                parts.Add("êm hơn · ít ngọt");
            if (k == "brighter")
                parts.Add("thanh và mở hơn");
            if (k == "more_adventurous")
                parts.Add("dấn thêm một nhịp");
            if (k == "low_caffeine")
                parts.Add("gu caffeine êm hơn");
            if (k == "warmer")
                parts.Add("ôm ấm thêm");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    public static void AppendMood(SommelierSessionState state, string? moodKey, string? moodLabel)
    {
        if (!string.IsNullOrWhiteSpace(moodKey))
        {
            var k = moodKey.Trim().ToLowerInvariant();
            if (state.MoodKeys.Count == 0 || state.MoodKeys[^1] != k)
                state.MoodKeys.Add(k);
        }

        if (!string.IsNullOrWhiteSpace(moodLabel))
        {
            var label = moodLabel.Trim();
            if (state.MoodLabels.Count == 0 || !string.Equals(state.MoodLabels[^1], label, StringComparison.Ordinal))
                state.MoodLabels.Add(label);
        }

        while (state.MoodKeys.Count > 12)
            state.MoodKeys.RemoveAt(0);
        while (state.MoodLabels.Count > 12)
            state.MoodLabels.RemoveAt(0);
    }

    public static void AppendRefinement(SommelierSessionState state, string? refinementKey)
    {
        if (string.IsNullOrWhiteSpace(refinementKey))
            return;
        var k = refinementKey.Trim().ToLowerInvariant();
        state.RefinementKeys.Add(k);
        while (state.RefinementKeys.Count > 16)
            state.RefinementKeys.RemoveAt(0);

        state.FlavorDirection = RecomputeFlavorDirection(state);
    }

    public static string? RecomputeFlavorDirection(SommelierSessionState state)
    {
        var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in state.RefinementKeys)
        {
            if (k is "softer" or "less_sweet")
                parts.Add("softer / less sweet");
            if (k == "brighter")
                parts.Add("brighter lift");
            if (k == "more_adventurous")
                parts.Add("more adventurous");
            if (k == "low_caffeine")
                parts.Add("gentler caffeine");
            if (k == "warmer")
                parts.Add("warmer rounder");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
