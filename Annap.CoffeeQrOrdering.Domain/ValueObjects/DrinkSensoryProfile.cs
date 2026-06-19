using System.Text;

namespace Annap.CoffeeQrOrdering.Domain.ValueObjects;

/// <summary>
/// Curated sensory axes for a drink—tokens are intentionally small and composable
/// (not open-ended ecommerce tags). Used for retrieval fusion, continuity, and a lightweight flavor graph.
/// </summary>
public sealed class DrinkSensoryProfile
{
    /// <summary>Weight and presence in the mouth: e.g. tea_like, silky, round, syrupy.</summary>
    public string Body { get; set; } = "";

    /// <summary>Structural brightness: quiet, balanced, lifted, crystalline.</summary>
    public string Acidity { get; set; } = "";

    /// <summary>Perceived sugar and ripeness: dry, restrained, rounded, luscious.</summary>
    public string Sweetness { get; set; } = "";

    /// <summary>How the cup resolves: clean, linger, cooling, smoky.</summary>
    public string Finish { get; set; } = "";

    /// <summary>Human nuance for finish, e.g. &quot;soft floral tail&quot;.</summary>
    public string FinishDetail { get; set; } = "";

    /// <summary>Primary aromatic family: floral, citrus, cocoa, spice, herbal, stone_fruit, malt.</summary>
    public string AromaFamily { get; set; } = "";

    /// <summary>Thermal / affective read: cool, temperate, warming, ember.</summary>
    public string TemperatureEmotion { get; set; } = "";

    /// <summary>Activation in the sip: still, focused, lifted, playful, intense.</summary>
    public string Energy { get; set; } = "";

    /// <summary>Who the cup imagines at the table: solitary, quiet, gathered, celebratory.</summary>
    public string SocialMood { get; set; } = "";

    /// <summary>1 (gentle)–5 (assertive); aligns with menu caffeine scale.</summary>
    public int CaffeineIntensity { get; set; }

    /// <summary>Mouthfeel: crisp, satin, velvet, syrupy, effervescent.</summary>
    public string Texture { get; set; } = "";

    public bool IsEffectivelyEmpty =>
        string.IsNullOrWhiteSpace(Body)
        && string.IsNullOrWhiteSpace(Acidity)
        && string.IsNullOrWhiteSpace(Sweetness)
        && string.IsNullOrWhiteSpace(Finish)
        && string.IsNullOrWhiteSpace(FinishDetail)
        && string.IsNullOrWhiteSpace(AromaFamily)
        && string.IsNullOrWhiteSpace(TemperatureEmotion)
        && string.IsNullOrWhiteSpace(Energy)
        && string.IsNullOrWhiteSpace(SocialMood)
        && string.IsNullOrWhiteSpace(Texture)
        && CaffeineIntensity <= 0;

    /// <summary>Natural-language block for embeddings (semantic, not keyword stuffing).</summary>
    public string ToEmbeddingNarrative()
    {
        var sb = new StringBuilder(320);
        sb.Append("Sensory read: ");
        Append(sb, "body", Body);
        Append(sb, "acidity", Acidity);
        Append(sb, "sweetness", Sweetness);
        Append(sb, "finish", Finish);
        if (!string.IsNullOrWhiteSpace(FinishDetail))
            sb.Append("finish detail ").Append(FinishDetail.Trim()).Append("; ");
        Append(sb, "aroma", AromaFamily);
        Append(sb, "thermal feeling", TemperatureEmotion);
        Append(sb, "energy", Energy);
        Append(sb, "social mood", SocialMood);
        Append(sb, "texture", Texture);
        if (CaffeineIntensity is >= 1 and <= 5)
            sb.Append("caffeine about ").Append(CaffeineIntensity).Append("/5; ");
        var s = sb.ToString().TrimEnd();
        return s.Length <= 480 ? s : s[..480].TrimEnd() + "…";
    }

    /// <summary>One tight line for LLM menu context.</summary>
    public string ToSommelierLine()
    {
        var sb = new StringBuilder(220);
        if (!string.IsNullOrWhiteSpace(Body)) sb.Append(Body).Append(" body · ");
        if (!string.IsNullOrWhiteSpace(Acidity)) sb.Append(Acidity).Append(" acid · ");
        if (!string.IsNullOrWhiteSpace(Sweetness)) sb.Append(Sweetness).Append(" sweet · ");
        if (!string.IsNullOrWhiteSpace(Finish)) sb.Append(Finish).Append(" finish");
        if (!string.IsNullOrWhiteSpace(FinishDetail)) sb.Append(" (").Append(FinishDetail.Trim()).Append(')');
        sb.Append(" · ");
        if (!string.IsNullOrWhiteSpace(AromaFamily)) sb.Append(AromaFamily).Append(" aromatics · ");
        if (!string.IsNullOrWhiteSpace(TemperatureEmotion)) sb.Append(TemperatureEmotion).Append(" warmth · ");
        if (!string.IsNullOrWhiteSpace(Energy)) sb.Append(Energy).Append(" energy · ");
        if (!string.IsNullOrWhiteSpace(SocialMood)) sb.Append(SocialMood).Append(" table · ");
        if (!string.IsNullOrWhiteSpace(Texture)) sb.Append(Texture).Append(" texture · ");
        if (CaffeineIntensity is >= 1 and <= 5) sb.Append("caf ").Append(CaffeineIntensity).Append("/5");
        var s = sb.ToString().Trim(' ', '·');
        return s.Length <= 240 ? s : s[..240].TrimEnd() + "…";
    }

    public DrinkSensoryProfile MergeWithLegacyLevels(int? caffeineLevel, int? sweetnessLevel, int? acidityLevel)
    {
        var copy = new DrinkSensoryProfile
        {
            Body = Body,
            Acidity = Acidity,
            Sweetness = Sweetness,
            Finish = Finish,
            FinishDetail = FinishDetail,
            AromaFamily = AromaFamily,
            TemperatureEmotion = TemperatureEmotion,
            Energy = Energy,
            SocialMood = SocialMood,
            Texture = Texture,
            CaffeineIntensity = CaffeineIntensity > 0 ? CaffeineIntensity : (caffeineLevel ?? 0)
        };

        if (string.IsNullOrWhiteSpace(copy.Acidity) && acidityLevel is int a)
            copy.Acidity = a switch { <= 2 => "quiet", 3 => "balanced", _ => "lifted" };
        if (string.IsNullOrWhiteSpace(copy.Sweetness) && sweetnessLevel is int sw)
            copy.Sweetness = sw switch { <= 2 => "restrained", 3 => "rounded", _ => "luscious" };
        if (copy.CaffeineIntensity <= 0 && caffeineLevel is int c)
            copy.CaffeineIntensity = c;
        if (string.IsNullOrWhiteSpace(copy.Body) && string.IsNullOrWhiteSpace(copy.Texture))
            copy.Texture = "satin";

        return copy;
    }

    private static void Append(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        sb.Append(label).Append(' ').Append(value.Trim()).Append("; ");
    }
}
