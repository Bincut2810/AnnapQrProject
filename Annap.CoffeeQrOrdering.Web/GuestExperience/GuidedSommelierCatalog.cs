using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Seed question set for the guided sommelier ritual. Replace with CMS-backed
/// <see cref="IGuidedSommelierQuestionSource"/> when admin configuration ships.
/// </summary>
public static class GuidedSommelierCatalog
{
    public const string QuestionSetId = "atelier_v4";

    public static IReadOnlyList<GuidedQuestionSeed> Questions { get; } = BuildCoreQuestions();

    /// <summary>Coffee-only calibration steps shown after <c>q2_coffee</c> (not used on tea/juice paths).</summary>
    public static IReadOnlyList<GuidedQuestionSeed> SpecialtyCoffeeDiscoveryQuestions { get; } = BuildSpecialtyCoffeeDiscoveryQuestions();

    public static bool IsSpecialtyDiscoveryQuestionId(string? questionId) =>
        string.Equals(questionId, "q_sc_flavor", StringComparison.OrdinalIgnoreCase)
        || string.Equals(questionId, "q_sc_experience", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<GuidedQuestionSeed> BuildCoreQuestions() =>
    [
        new(
            "q1",
            "Tối nay đang trở thành kiểu buổi tối nào?",
            [
                new GuidedOptionSeed(
                    "q1_light",
                    "Nhẹ và không vội",
                    "nhẹ nhàng",
                    new DrinkSensoryProfile
                    {
                        Energy = "still",
                        Acidity = "quiet",
                        Texture = "satin"
                    },
                    MoodKey: "calm"),
                new GuidedOptionSeed(
                    "q1_alert",
                    "Tỉnh và hiện diện",
                    "tỉnh táo",
                    new DrinkSensoryProfile
                    {
                        Energy = "focused",
                        CaffeineIntensity = 3,
                        Acidity = "balanced"
                    },
                    MoodKey: "focus"),
                new GuidedOptionSeed(
                    "q1_refresh",
                    "Tươi và thoáng",
                    "tươi mát",
                    new DrinkSensoryProfile
                    {
                        Acidity = "lifted",
                        AromaFamily = "citrus",
                        Energy = "lifted",
                        Finish = "clean"
                    },
                    MoodKey: "bright"),
                new GuidedOptionSeed(
                    "q1_curious",
                    "Tò mò, muốn đi xa hơn",
                    "thích khám phá",
                    new DrinkSensoryProfile
                    {
                        Energy = "playful",
                        SocialMood = "gathered",
                        Finish = "linger"
                    },
                    MoodKey: "adventurous")
            ]),
        new(
            "q2",
            "Ly đầu tiên nên cảm thấy thế nào?",
            [
                new GuidedOptionSeed(
                    "q2_coffee",
                    "Cà phê — ấm và có cấu trúc",
                    "cà phê",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "cocoa",
                        Body = "round",
                        Energy = "focused",
                        CaffeineIntensity = 3,
                        TemperatureEmotion = "warming"
                    },
                    MoodKey: "focus",
                    CategoryIntentKey: "coffee"),
                new GuidedOptionSeed(
                    "q2_tea",
                    "Trà — êm và hương hoa",
                    "trà",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "floral",
                        Acidity = "quiet",
                        Energy = "still",
                        CaffeineIntensity = 1,
                        Texture = "satin"
                    },
                    MoodKey: "calm",
                    CategoryIntentKey: "tea"),
                new GuidedOptionSeed(
                    "q2_fruit",
                    "Một chút tươi, trái cây",
                    "nước ép",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "citrus",
                        Acidity = "lifted",
                        Energy = "lifted",
                        CaffeineIntensity = 1,
                        Finish = "clean"
                    },
                    MoodKey: "bright",
                    CategoryIntentKey: "fruit"),
                new GuidedOptionSeed(
                    "q2_juice",
                    "Nước ép",
                    "nước ép",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "citrus",
                        Acidity = "lifted",
                        Energy = "lifted",
                        CaffeineIntensity = 1,
                        Finish = "clean"
                    },
                    MoodKey: "bright",
                    CategoryIntentKey: BeverageFamilyGrounding.Juice),
                new GuidedOptionSeed(
                    "q2_smoothie",
                    "Sinh tố mềm",
                    "sinh tố",
                    new DrinkSensoryProfile
                    {
                        Texture = "velvet",
                        Body = "round",
                        Sweetness = "gentle",
                        Energy = "still",
                        CaffeineIntensity = 1
                    },
                    MoodKey: "calm",
                    CategoryIntentKey: BeverageFamilyGrounding.Smoothie),
                new GuidedOptionSeed(
                    "q2_matcha",
                    "Matcha — xanh và yên",
                    "matcha",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "floral",
                        Texture = "satin",
                        Body = "round",
                        Energy = "focused",
                        CaffeineIntensity = 2
                    },
                    MoodKey: "focus",
                    CategoryIntentKey: BeverageFamilyGrounding.Matcha),
                new GuidedOptionSeed(
                    "q2_signature",
                    "Ly signature của quán",
                    "signature",
                    new DrinkSensoryProfile
                    {
                        Energy = "playful",
                        SocialMood = "gathered",
                        Sweetness = "rounded",
                        Finish = "linger"
                    },
                    MoodKey: "adventurous",
                    CategoryIntentKey: "signature")
            ]),
        new(
            "q3",
            "Mình giữ sáng, hay để dịu xuống?",
            [
                new GuidedOptionSeed(
                    "q3_low",
                    "Giữ sáng — ít ngọt",
                    "ít ngọt",
                    new DrinkSensoryProfile { Sweetness = "restrained", Acidity = "balanced" }),
                new GuidedOptionSeed(
                    "q3_medium",
                    "Ở giữa, cân bằng",
                    "ngọt vừa",
                    new DrinkSensoryProfile { Sweetness = "rounded", Finish = "clean" }),
                new GuidedOptionSeed(
                    "q3_semisweet",
                    "Dịu xuống, hơi ngọt",
                    "hơi ngọt",
                    new DrinkSensoryProfile { Sweetness = "gentle", Body = "round" }),
                new GuidedOptionSeed(
                    "q3_sweet",
                    "Đậm và ngọt hơn",
                    "ngọt nhiều",
                    new DrinkSensoryProfile
                    {
                        Sweetness = "luscious",
                        Body = "syrupy",
                        Texture = "velvet",
                        Finish = "linger"
                    })
            ]),
        new(
            "q4",
            "Tối nay cần độ tỉnh đến đâu?",
            [
                new GuidedOptionSeed(
                    "q4_none",
                    "Không cần — giữ mềm",
                    "không caffeine",
                    new DrinkSensoryProfile { CaffeineIntensity = 1, Energy = "still" }),
                new GuidedOptionSeed(
                    "q4_low",
                    "Một chút tỉnh thôi",
                    "caffeine nhẹ",
                    new DrinkSensoryProfile { CaffeineIntensity = 2, Energy = "still" }),
                new GuidedOptionSeed(
                    "q4_medium",
                    "Vừa đủ, rõ ràng",
                    "caffeine vừa",
                    new DrinkSensoryProfile { CaffeineIntensity = 3, Energy = "focused" },
                    MoodKey: "focus"),
                new GuidedOptionSeed(
                    "q4_strong",
                    "Mạnh như quán có thể",
                    "caffeine mạnh",
                    new DrinkSensoryProfile { CaffeineIntensity = 5, Energy = "intense" })
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildSpecialtyCoffeeDiscoveryQuestions() =>
    [
        new(
            "q_sc_flavor",
            "Khi hình dung ngụm đầu tiên, điều gì thu hút bạn nhất?",
            [
                new GuidedOptionSeed(
                    "q_sc_flavor_floral",
                    "Tinh tế & hoa nhài",
                    "hoa nhẹ",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "floral",
                        Acidity = "quiet",
                        Energy = "still",
                        Finish = "clean"
                    },
                    RefinementKey: "sc_flavor:floral",
                    FlavorTagsJson: "floral,jasmine,gentle"),
                new GuidedOptionSeed(
                    "q_sc_flavor_fruit",
                    "Tươi sáng & mọng nước",
                    "trái cây tươi",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "stone_fruit",
                        Acidity = "lifted",
                        Energy = "playful",
                        Finish = "clean"
                    },
                    RefinementKey: "sc_flavor:fruit_forward",
                    FlavorTagsJson: "fruit,peach,bright"),
                new GuidedOptionSeed(
                    "q_sc_flavor_wine",
                    "Sâu & như rượu vang",
                    "vang đỏ nhẹ",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "cocoa",
                        Acidity = "lifted",
                        Body = "syrupy",
                        Energy = "intense",
                        Finish = "linger"
                    },
                    RefinementKey: "sc_flavor:wine_like",
                    FlavorTagsJson: "wine,jam,complex"),
                new GuidedOptionSeed(
                    "q_sc_flavor_blueberry",
                    "Nhiều lớp & bất ngờ",
                    "blueberry & mật ong",
                    new DrinkSensoryProfile
                    {
                        AromaFamily = "floral",
                        Acidity = "crystalline",
                        Sweetness = "luscious",
                        Energy = "focused",
                        Finish = "linger"
                    },
                    RefinementKey: "sc_flavor:blueberry_honey",
                    FlavorTagsJson: "blueberry,honey,layered")
            ]),
        new(
            "q_sc_experience",
            "Bạn muốn ly này đồng hành với buổi tối thế nào?",
            [
                new GuidedOptionSeed(
                    "q_sc_experience_soft",
                    "Bạn đồng hành lặng lẽ",
                    "mềm mở",
                    new DrinkSensoryProfile
                    {
                        Energy = "still",
                        SocialMood = "quiet",
                        Texture = "satin"
                    },
                    RefinementKey: "sc_experience:soft"),
                new GuidedOptionSeed(
                    "q_sc_experience_balanced",
                    "Quen thuộc, dễ yêu",
                    "cân bằng",
                    new DrinkSensoryProfile
                    {
                        Energy = "still",
                        SocialMood = "gathered",
                        Sweetness = "rounded"
                    },
                    RefinementKey: "sc_experience:balanced"),
                new GuidedOptionSeed(
                    "q_sc_experience_complex",
                    "Khám phá chậm rãi",
                    "mở dần",
                    new DrinkSensoryProfile
                    {
                        Energy = "focused",
                        Finish = "linger",
                        Body = "round"
                    },
                    RefinementKey: "sc_experience:complex"),
                new GuidedOptionSeed(
                    "q_sc_experience_surprising",
                    "Hành trình bất ngờ",
                    "bất ngờ",
                    new DrinkSensoryProfile
                    {
                        Energy = "playful",
                        Finish = "linger",
                        SocialMood = "gathered"
                    },
                    RefinementKey: "sc_experience:surprising")
            ])
    ];

    public static IReadOnlyList<GuidedQuestionSeed> MergeClientCatalogQuestions(
        IReadOnlyList<GuidedQuestionSeed> loaded)
    {
        if (loaded.Count == 0)
            return Questions.Concat(SpecialtyCoffeeDiscoveryQuestions).ToList();

        var hasFlavor = loaded.Any(q => string.Equals(q.QuestionId, "q_sc_flavor", StringComparison.OrdinalIgnoreCase));
        if (hasFlavor)
            return loaded;

        return loaded.Concat(SpecialtyCoffeeDiscoveryQuestions).ToList();
    }

    private static readonly Dictionary<string, GuidedOptionSeed> OptionsById =
        Questions.Concat(SpecialtyCoffeeDiscoveryQuestions)
            .SelectMany(q => q.Options)
            .ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);

    public static bool TryResolveOptions(IReadOnlyList<string> optionIds, out IReadOnlyList<GuidedOptionSeed> resolved, out string? error)
    {
        resolved = Array.Empty<GuidedOptionSeed>();
        if (optionIds is null || optionIds.Count != Questions.Count)
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var picked = new GuidedOptionSeed[Questions.Count];
        for (var i = 0; i < Questions.Count; i++)
        {
            var id = (optionIds[i] ?? "").Trim();
            if (!OptionsById.TryGetValue(id, out var opt))
            {
                error = "That choice is not available in this set.";
                return false;
            }

            if (!id.StartsWith(Questions[i].QuestionId + "_", StringComparison.OrdinalIgnoreCase))
            {
                error = "Each question needs its own answer.";
                return false;
            }

            picked[i] = opt;
        }

        resolved = picked;
        error = null;
        return true;
    }

    public static DrinkSensoryProfile MergeGuestHints(IReadOnlyList<GuidedOptionSeed> options)
    {
        var merged = new DrinkSensoryProfile();
        var cafSum = 0;
        var cafN = 0;

        foreach (var opt in options)
        {
            var h = opt.SensoryHints;
            if (string.IsNullOrWhiteSpace(merged.Body) && !string.IsNullOrWhiteSpace(h.Body))
                merged.Body = h.Body.Trim();
            if (string.IsNullOrWhiteSpace(merged.Acidity) && !string.IsNullOrWhiteSpace(h.Acidity))
                merged.Acidity = h.Acidity.Trim();
            if (string.IsNullOrWhiteSpace(merged.Sweetness) && !string.IsNullOrWhiteSpace(h.Sweetness))
                merged.Sweetness = h.Sweetness.Trim();
            if (string.IsNullOrWhiteSpace(merged.Finish) && !string.IsNullOrWhiteSpace(h.Finish))
                merged.Finish = h.Finish.Trim();
            if (string.IsNullOrWhiteSpace(merged.FinishDetail) && !string.IsNullOrWhiteSpace(h.FinishDetail))
                merged.FinishDetail = h.FinishDetail.Trim();
            if (string.IsNullOrWhiteSpace(merged.AromaFamily) && !string.IsNullOrWhiteSpace(h.AromaFamily))
                merged.AromaFamily = h.AromaFamily.Trim();
            if (string.IsNullOrWhiteSpace(merged.TemperatureEmotion) && !string.IsNullOrWhiteSpace(h.TemperatureEmotion))
                merged.TemperatureEmotion = h.TemperatureEmotion.Trim();
            if (string.IsNullOrWhiteSpace(merged.Energy) && !string.IsNullOrWhiteSpace(h.Energy))
                merged.Energy = h.Energy.Trim();
            if (string.IsNullOrWhiteSpace(merged.SocialMood) && !string.IsNullOrWhiteSpace(h.SocialMood))
                merged.SocialMood = h.SocialMood.Trim();
            if (string.IsNullOrWhiteSpace(merged.Texture) && !string.IsNullOrWhiteSpace(h.Texture))
                merged.Texture = h.Texture.Trim();
            if (h.CaffeineIntensity is >= 1 and <= 5)
            {
                cafSum += h.CaffeineIntensity;
                cafN++;
            }
        }

        if (cafN > 0)
            merged.CaffeineIntensity = Math.Clamp((int)Math.Round((double)cafSum / cafN), 1, 5);

        return merged;
    }

    public static string ToClientJson()
    {
        var dto = new ClientCatalogDto(
            QuestionSetId,
            Questions.Concat(SpecialtyCoffeeDiscoveryQuestions)
                .Select(q => new ClientQuestionDto(
                    q.QuestionId,
                    q.Prompt,
                    q.Options.Select(ToClientOption).ToList()))
                .ToList());
        return JsonSerializer.Serialize(
            dto,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    private sealed record ClientCatalogDto(string SetId, IReadOnlyList<ClientQuestionDto> Questions);

    private sealed record ClientQuestionDto(string QuestionId, string Prompt, IReadOnlyList<ClientOptionDto> Options);

    internal sealed record ClientOptionDto(string OptionId, string Label, string? Reflection = null);

    internal static ClientOptionDto ToClientOption(GuidedOptionSeed option) =>
        new(
            option.OptionId,
            option.Label,
            string.IsNullOrWhiteSpace(option.GuestReflection) ? null : option.GuestReflection.Trim());
}

public sealed record GuidedQuestionSeed(string QuestionId, string Prompt, IReadOnlyList<GuidedOptionSeed> Options, string? Description = null);

public sealed record GuidedOptionSeed(
    string OptionId,
    string Label,
    string EmotionalFragment,
    DrinkSensoryProfile SensoryHints,
    string? MoodKey = null,
    string? RefinementKey = null,
    decimal WeightMultiplier = 1m,
    string? FlavorTagsJson = null,
    string? CategoryIntentKey = null,
    string? GuestReflection = null);
