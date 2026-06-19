using System.Text.Json;
using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Merges admin JSON with defaults for the Letter Room discovery desk (envelopes, copy, pacing hints).
/// </summary>
public static class GuestLetterRoomDesk
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public sealed class EnvelopeDto
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("hint")]
        public string? Hint { get; set; }

        /// <summary>kraft | cream | ink</summary>
        [JsonPropertyName("texture")]
        public string? Texture { get; set; }
    }

    public sealed class LetterRoomInputDto
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        [JsonPropertyName("deskHint")]
        public string? DeskHint { get; set; }

        [JsonPropertyName("envelopes")]
        public List<EnvelopeDto>? Envelopes { get; set; }

        [JsonPropertyName("ctaPrimary")]
        public string? CtaPrimary { get; set; }

        [JsonPropertyName("rerollCta")]
        public string? RerollCta { get; set; }

        [JsonPropertyName("earnedKicker")]
        public string? EarnedKicker { get; set; }

        [JsonPropertyName("refusalLines")]
        public List<string>? RefusalLines { get; set; }

        [JsonPropertyName("insideLetterLines")]
        public List<string>? InsideLetterLines { get; set; }

        [JsonPropertyName("paperTheme")]
        public string? PaperTheme { get; set; }
    }

    public sealed record ResolvedLetterRoom(
        string Title,
        string Subtitle,
        string DeskHint,
        IReadOnlyList<EnvelopeClient> Envelopes,
        string CtaPrimary,
        string RerollCta,
        string EarnedKicker,
        IReadOnlyList<string> RefusalLines,
        IReadOnlyList<string> InsideLetterLines,
        string PaperTheme);

    public sealed record EnvelopeClient(string Label, string Hint, string Texture);

    private static readonly string[] DefaultInsideLines =
    [
        "You’ve been leaning toward softer things lately — this letter says so, quietly.",
        "This feels like the kind of drink that stretches time without asking permission.",
        "Some cups are meant to interrupt the day; this one arrives as a gentle comma.",
        "The desk went still when this name rose — we did not argue with it.",
        "Whatever you were circling on the menu, this line cuts through the noise.",
        "Not every envelope tells the truth; this one matched the room’s temperature.",
        "There is patience folded into this pour — read it slowly.",
        "Tonight the ink wanted something composed, legible, and a little brave."
    ];

    private static readonly string[] DefaultRefusals =
    [
        "The desk has gone quiet.",
        "No more letters tonight.",
        "The café stands by this recommendation."
    ];

    public static ResolvedLetterRoom Resolve(string? json, int adventureTone)
    {
        var tone = adventureTone is >= 1 and <= 5 ? adventureTone : 3;
        var baseRoom = Defaults(tone);
        if (string.IsNullOrWhiteSpace(json))
            return baseRoom;

        try
        {
            var dto = JsonSerializer.Deserialize<LetterRoomInputDto>(json, JsonOpts);
            if (dto is null)
                return baseRoom;

            var envs = MergeEnvelopes(dto.Envelopes, baseRoom.Envelopes);
            var inside = MergeLines(dto.InsideLetterLines, baseRoom.InsideLetterLines);
            var refuse = MergeLines(dto.RefusalLines, baseRoom.RefusalLines);

            return new ResolvedLetterRoom(
                Coalesce(dto.Title, baseRoom.Title),
                Coalesce(dto.Subtitle, baseRoom.Subtitle),
                Coalesce(dto.DeskHint, baseRoom.DeskHint),
                envs,
                Coalesce(dto.CtaPrimary, baseRoom.CtaPrimary),
                Coalesce(dto.RerollCta, baseRoom.RerollCta),
                Coalesce(dto.EarnedKicker, baseRoom.EarnedKicker),
                refuse,
                inside,
                NormalizeTheme(dto.PaperTheme, baseRoom.PaperTheme));
        }
        catch
        {
            return baseRoom;
        }
    }

    public static string ToClientJson(string? json, int adventureTone)
    {
        var r = Resolve(json, adventureTone);
        return JsonSerializer.Serialize(
            new
            {
                title = r.Title,
                subtitle = r.Subtitle,
                deskHint = r.DeskHint,
                envelopes = r.Envelopes.Select(e => new { e.Label, e.Hint, e.Texture }).ToList(),
                ctaPrimary = r.CtaPrimary,
                rerollCta = r.RerollCta,
                earnedKicker = r.EarnedKicker,
                refusalLines = r.RefusalLines,
                insideLetterLines = r.InsideLetterLines,
                paperTheme = r.PaperTheme
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public static object ToClientObject(ResolvedLetterRoom r) =>
        new
        {
            title = r.Title,
            subtitle = r.Subtitle,
            deskHint = r.DeskHint,
            envelopes = r.Envelopes.Select(e => new { e.Label, e.Hint, e.Texture }).ToList(),
            ctaPrimary = r.CtaPrimary,
            rerollCta = r.RerollCta,
            earnedKicker = r.EarnedKicker,
            refusalLines = r.RefusalLines,
            insideLetterLines = r.InsideLetterLines,
            paperTheme = r.PaperTheme
        };

    public static string PickInsideLine(IReadOnlyList<string> lines, int salt)
    {
        if (lines.Count == 0)
            return DefaultInsideLines[Math.Abs(salt) % DefaultInsideLines.Length];
        return lines[Math.Abs(salt) % lines.Count];
    }

    private static readonly string[] TastingBreaths =
    [
        "The list moves in your periphery — we only sharpened one name until it rang true.",
        "Ink, paper, steam: three textures, one verdict. The tray can carry the rest.",
        "No dice, no slots — just weight and patience until a cup insisted on being named.",
        "We let the envelopes argue; this line won without raising its voice.",
        "Fast hands, slow judgment — the bar’s favorite contradiction."
    ];

    public static string PickTastingBreath(int salt) =>
        TastingBreaths[Math.Abs(salt) % TastingBreaths.Length];

    private static ResolvedLetterRoom Defaults(int tone)
    {
        var spice = tone >= 4 ? " A little mischief is allowed." : "";
        return new ResolvedLetterRoom(
            Title: "Tonight’s envelope",
            Subtitle: "Three sealed notes arrived at the desk — one is meant for your hand.",
            DeskHint: "Tap one seal. No quiz, no checklist — just paper, ink, and a pour worth the pause." + spice,
            Envelopes:
            [
                new EnvelopeClient("For slow evenings", "Cream stock · soft edge", "cream"),
                new EnvelopeClient("Open when undecided", "Kraft · honest grain", "kraft"),
                new EnvelopeClient("Not for rushing", "Cool gray · wax whispers", "ink")
            ],
            CtaPrimary: "Send this to my table",
            RerollCta: "Open another envelope",
            EarnedKicker: "This arrived for you.",
            RefusalLines: DefaultRefusals,
            InsideLetterLines: [.. DefaultInsideLines],
            PaperTheme: "desk");
    }

    private static IReadOnlyList<EnvelopeClient> MergeEnvelopes(
        List<EnvelopeDto>? incoming,
        IReadOnlyList<EnvelopeClient> fallback)
    {
        if (incoming is null || incoming.Count == 0)
            return fallback;

        var list = new List<EnvelopeClient>(3);
        for (var i = 0; i < 3; i++)
        {
            var d = i < incoming.Count ? incoming[i] : null;
            var fb = fallback[Math.Min(i, fallback.Count - 1)];
            list.Add(new EnvelopeClient(
                Coalesce(d?.Label, fb.Label),
                Coalesce(d?.Hint, fb.Hint),
                NormalizeTexture(d?.Texture, fb.Texture)));
        }

        return list;
    }

    private static IReadOnlyList<string> MergeLines(List<string>? incoming, IReadOnlyList<string> fallback)
    {
        if (incoming is null || incoming.Count == 0)
            return fallback;
        var cleaned = incoming
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Take(12)
            .ToList();
        return cleaned.Count == 0 ? fallback : cleaned;
    }

    private static string Coalesce(string? a, string b) =>
        string.IsNullOrWhiteSpace(a) ? b : a.Trim();

    private static string NormalizeTexture(string? t, string fallback)
    {
        var x = (t ?? "").Trim().ToLowerInvariant();
        return x is "kraft" or "cream" or "ink" ? x : fallback;
    }

    private static string NormalizeTheme(string? t, string fallback)
    {
        var x = (t ?? "").Trim().ToLowerInvariant();
        return x is "desk" or "night" or "dawn" ? x : fallback;
    }
}
