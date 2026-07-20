using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Category-branch guided sommelier (atelier_v5).
/// Entry chooses a drink family; each family has its own 1–4 question conversation.
/// </summary>
public static class GuidedSommelierCatalog
{
    public const string QuestionSetId = "atelier_v5";
    public const string EntryQuestionId = "q0";

    public const string BranchSpecialty = "specialty";
    public const string BranchCoffee = "coffee";
    public const string BranchTea = "tea";
    public const string BranchMatcha = "matcha";
    public const string BranchFruit = "fruit";
    public const string BranchSignature = "signature";

    /// <summary>Entry question only — always first.</summary>
    public static IReadOnlyList<GuidedQuestionSeed> Questions { get; } = [BuildEntryQuestion()];

    /// <summary>All questions for CMS seed + client catalog.</summary>
    public static IReadOnlyList<GuidedQuestionSeed> AllQuestions { get; } =
        Questions.Concat(BuildSpecialtyQuestions())
            .Concat(BuildCoffeeQuestions())
            .Concat(BuildTeaQuestions())
            .Concat(BuildMatchaQuestions())
            .Concat(BuildFruitQuestions())
            .Concat(BuildSignatureQuestions())
            .ToList();

    /// <summary>Ordered follow-up question IDs per branch (after entry).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Branches { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [BranchSpecialty] = ["q_sp_tried", "q_sp_profile", "q_sp_adventure", "q_sp_format"],
            [BranchCoffee] = ["q_cf_style", "q_cf_sweet", "q_cf_temp"],
            [BranchTea] = ["q_te_feel", "q_te_moment"],
            [BranchMatcha] = ["q_ma_style", "q_ma_sweet", "q_ma_temp"],
            [BranchFruit] = ["q_fr_profile", "q_fr_cold"],
            [BranchSignature] = ["q_sg_intent"]
        };

    /// <summary>Legacy name kept for callers; specialty path is now its own branch.</summary>
    public static IReadOnlyList<GuidedQuestionSeed> SpecialtyCoffeeDiscoveryQuestions { get; } =
        BuildSpecialtyQuestions();

    public static bool IsSpecialtyDiscoveryQuestionId(string? questionId) =>
        !string.IsNullOrWhiteSpace(questionId)
        && questionId.StartsWith("q_sp_", StringComparison.OrdinalIgnoreCase);

    public static bool IsBranchQuestionId(string? questionId) =>
        !string.IsNullOrWhiteSpace(questionId)
        && (questionId.StartsWith("q_sp_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_cf_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_te_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_ma_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_fr_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_sg_", StringComparison.OrdinalIgnoreCase));

    public static string? ResolveBranchKey(string? entryOptionId)
    {
        if (string.IsNullOrWhiteSpace(entryOptionId))
            return null;
        return entryOptionId.Trim().ToLowerInvariant() switch
        {
            "q0_specialty" => BranchSpecialty,
            "q0_coffee" => BranchCoffee,
            "q0_tea" => BranchTea,
            "q0_matcha" => BranchMatcha,
            "q0_fruit" => BranchFruit,
            "q0_signature" => BranchSignature,
            _ => null
        };
    }

    public static IReadOnlyList<GuidedQuestionSeed> QuestionsForBranch(string? branchKey)
    {
        if (string.IsNullOrWhiteSpace(branchKey) || !Branches.TryGetValue(branchKey, out var ids))
            return Questions;

        var byId = AllQuestions.ToDictionary(q => q.QuestionId, StringComparer.OrdinalIgnoreCase);
        var list = new List<GuidedQuestionSeed>(1 + ids.Count) { Questions[0] };
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var q))
                list.Add(q);
        }

        return list;
    }

    public static IReadOnlyList<GuidedQuestionSeed> MergeClientCatalogQuestions(
        IReadOnlyList<GuidedQuestionSeed> loaded)
    {
        if (loaded.Count == 0)
            return AllQuestions;

        var hasEntry = loaded.Any(q =>
            string.Equals(q.QuestionId, EntryQuestionId, StringComparison.OrdinalIgnoreCase));
        if (hasEntry)
            return loaded;

        // Old atelier_v4 (or incomplete CMS) — prefer the built-in branch catalog.
        return AllQuestions;
    }

    private static readonly Dictionary<string, GuidedOptionSeed> OptionsById =
        AllQuestions.SelectMany(q => q.Options)
            .ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);

    public static bool TryResolveOptions(
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error) =>
        TryResolveBranchPath(optionIds, out resolved, out error);

    public static bool TryResolveBranchPath(
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error)
    {
        resolved = Array.Empty<GuidedOptionSeed>();
        if (optionIds is null || optionIds.Count == 0)
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var entryId = (optionIds[0] ?? "").Trim();
        var branch = ResolveBranchKey(entryId);
        if (branch is null)
        {
            error = "Please choose what you would like today.";
            return false;
        }

        var path = QuestionsForBranch(branch);
        if (optionIds.Count != path.Count)
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var optionsById = path.SelectMany(q => q.Options)
            .ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);
        var picked = new GuidedOptionSeed[path.Count];
        for (var i = 0; i < path.Count; i++)
        {
            var id = (optionIds[i] ?? "").Trim();
            if (!optionsById.TryGetValue(id, out var opt))
            {
                error = "That choice is not available in this set.";
                return false;
            }

            if (!id.StartsWith(path[i].QuestionId + "_", StringComparison.OrdinalIgnoreCase))
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

    public static string ToClientJson() =>
        ToClientJson(AllQuestions, QuestionSetId);

    public static string ToClientJson(IReadOnlyList<GuidedQuestionSeed> questions, string setId)
    {
        var merged = MergeClientCatalogQuestions(questions);
        var dto = new ClientCatalogDto(
            setId,
            EntryQuestionId,
            Branches.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.ToList(),
                StringComparer.OrdinalIgnoreCase),
            merged.Select(q => new ClientQuestionDto(
                q.QuestionId,
                q.Prompt,
                q.Options.Select(ToClientOption).ToList())).ToList());
        return JsonSerializer.Serialize(
            dto,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    internal static ClientOptionDto ToClientOption(GuidedOptionSeed option) =>
        new(
            option.OptionId,
            option.Label,
            string.IsNullOrWhiteSpace(option.GuestReflection) ? null : option.GuestReflection.Trim(),
            option.BranchKey);

    private static GuidedQuestionSeed BuildEntryQuestion() =>
        new(
            EntryQuestionId,
            "Hôm nay bạn muốn gì?",
            [
                Opt("q0_specialty", "Specialty Coffee", "specialty coffee", BranchSpecialty, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { AromaFamily = "floral", Energy = "focused", CaffeineIntensity = 3 },
                    MoodKey: "focus"),
                Opt("q0_coffee", "Cà phê", "cà phê", BranchCoffee, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "round", CaffeineIntensity = 3 },
                    MoodKey: "focus"),
                Opt("q0_tea", "Trà", "trà", BranchTea, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "floral", Energy = "still", CaffeineIntensity = 1 },
                    MoodKey: "calm"),
                Opt("q0_matcha", "Matcha", "matcha", BranchMatcha, BeverageFamilyGrounding.Matcha,
                    new DrinkSensoryProfile { AromaFamily = "floral", Texture = "satin", CaffeineIntensity = 2 },
                    MoodKey: "focus"),
                Opt("q0_fruit", "Trái cây / Nước ép", "trái cây", BranchFruit, BeverageFamilyGrounding.Fruit,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Energy = "lifted", CaffeineIntensity = 1 },
                    MoodKey: "bright"),
                Opt("q0_signature", "Signature", "signature", BranchSignature, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "playful", SocialMood = "gathered", Finish = "linger" },
                    MoodKey: "adventurous")
            ],
            "Chọn hướng đồ uống — mỗi hướng có cuộc trò chuyện riêng.");

    private static IReadOnlyList<GuidedQuestionSeed> BuildSpecialtyQuestions() =>
    [
        new(
            "q_sp_tried",
            "Bạn đã từng thử specialty coffee chưa?",
            [
                Opt("q_sp_tried_first", "Lần đầu", "lần đầu", null, null,
                    new DrinkSensoryProfile { Energy = "still", Texture = "satin" },
                    MoodKey: "calm", RefinementKey: "sc_tried:first"),
                Opt("q_sp_tried_occasional", "Thỉnh thoảng", "thỉnh thoảng", null, null,
                    new DrinkSensoryProfile { Energy = "lifted", Acidity = "balanced" },
                    MoodKey: "bright", RefinementKey: "sc_tried:occasional"),
                Opt("q_sp_tried_regular", "Uống thường xuyên", "quen thuộc", null, null,
                    new DrinkSensoryProfile { Energy = "focused", Finish = "linger" },
                    MoodKey: "adventurous", RefinementKey: "sc_tried:regular")
            ]),
        new(
            "q_sp_profile",
            "Hồ sơ nào nghe thú vị nhất với bạn?",
            [
                Opt("q_sp_profile_floral", "Hoa", "hoa nhẹ", null, null,
                    new DrinkSensoryProfile { AromaFamily = "floral", Acidity = "quiet", Finish = "clean" },
                    RefinementKey: "sc_flavor:floral", FlavorTagsJson: "floral,jasmine,gentle"),
                Opt("q_sp_profile_fruit", "Trái cây", "trái cây tươi", null, null,
                    new DrinkSensoryProfile { AromaFamily = "stone_fruit", Acidity = "lifted", Energy = "playful" },
                    RefinementKey: "sc_flavor:fruit_forward", FlavorTagsJson: "fruit,peach,bright"),
                Opt("q_sp_profile_chocolate", "Chocolate", "socola", null, null,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "syrupy", Finish = "linger" },
                    RefinementKey: "sc_flavor:chocolate", FlavorTagsJson: "chocolate,cocoa,jam"),
                Opt("q_sp_profile_surprise", "Để quán bất ngờ", "bất ngờ", null, null,
                    new DrinkSensoryProfile { Energy = "playful", Finish = "linger", Acidity = "crystalline" },
                    RefinementKey: "sc_flavor:surprise", FlavorTagsJson: "blueberry,honey,layered")
            ]),
        new(
            "q_sp_adventure",
            "Bạn muốn mạo hiểm đến mức nào?",
            [
                Opt("q_sp_adventure_safe", "An toàn", "an toàn", null, null,
                    new DrinkSensoryProfile { Energy = "still", SocialMood = "quiet", Texture = "satin" },
                    RefinementKey: "sc_experience:soft"),
                Opt("q_sp_adventure_balanced", "Cân bằng", "cân bằng", null, null,
                    new DrinkSensoryProfile { Energy = "still", SocialMood = "gathered", Sweetness = "rounded" },
                    RefinementKey: "sc_experience:balanced"),
                Opt("q_sp_adventure_experimental", "Thử nghiệm", "thử nghiệm", null, null,
                    new DrinkSensoryProfile { Energy = "playful", Finish = "linger", Body = "round" },
                    RefinementKey: "sc_experience:surprising")
            ]),
        new(
            "q_sp_format",
            "Bạn muốn nhận gì từ quầy?",
            [
                Opt("q_sp_format_one", "Một gợi ý", "một nguồn", null, null,
                    new DrinkSensoryProfile { Energy = "focused" },
                    RefinementKey: "sc_format:one"),
                Opt("q_sp_format_compare", "So sánh hai hạt", "so sánh", null, null,
                    new DrinkSensoryProfile { Energy = "playful", SocialMood = "gathered" },
                    RefinementKey: "sc_format:compare")
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildCoffeeQuestions() =>
    [
        new(
            "q_cf_style",
            "Bạn thường uống kiểu nào?",
            [
                Opt("q_cf_style_black", "Đen", "đen", null, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { Sweetness = "restrained", Body = "round", CaffeineIntensity = 4 },
                    RefinementKey: "cf_style:black"),
                Opt("q_cf_style_milk", "Cà phê sữa", "sữa", null, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { Sweetness = "gentle", Body = "round", Texture = "velvet" },
                    RefinementKey: "cf_style:milk"),
                Opt("q_cf_style_latte", "Latte", "latte", null, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { Sweetness = "gentle", Texture = "satin", Body = "round" },
                    RefinementKey: "cf_style:latte"),
                Opt("q_cf_style_espresso", "Espresso", "espresso", null, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { Body = "syrupy", Energy = "intense", CaffeineIntensity = 5 },
                    RefinementKey: "cf_style:espresso")
            ]),
        new(
            "q_cf_sweet",
            "Bạn thích độ ngọt thế nào?",
            [
                Opt("q_cf_sweet_low", "Ít ngọt", "ít ngọt", null, null,
                    new DrinkSensoryProfile { Sweetness = "restrained" }),
                Opt("q_cf_sweet_medium", "Vừa", "ngọt vừa", null, null,
                    new DrinkSensoryProfile { Sweetness = "gentle" }),
                Opt("q_cf_sweet_high", "Ngọt", "ngọt", null, null,
                    new DrinkSensoryProfile { Sweetness = "luscious" }),
                Opt("q_cf_sweet_any", "Tùy", "tùy", null, null,
                    new DrinkSensoryProfile { Sweetness = "rounded" })
            ]),
        new(
            "q_cf_temp",
            "Nóng hay đá?",
            [
                Opt("q_cf_temp_hot", "Nóng", "nóng", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "warming", Energy = "focused" }),
                Opt("q_cf_temp_iced", "Đá", "lạnh", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling", Energy = "lifted" }),
                Opt("q_cf_temp_any", "Tùy", "tùy", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "temperate" })
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildTeaQuestions() =>
    [
        new(
            "q_te_feel",
            "Bạn muốn trải nghiệm trà thế nào?",
            [
                Opt("q_te_feel_floral", "Hoa", "hoa", null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "floral", Energy = "still", Finish = "clean" },
                    RefinementKey: "te_feel:floral"),
                Opt("q_te_feel_refresh", "Thanh mát", "thanh mát", null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { Acidity = "lifted", Finish = "clean", Energy = "lifted" },
                    RefinementKey: "te_feel:refresh"),
                Opt("q_te_feel_fruit", "Trái cây", "trái cây", null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Energy = "playful" },
                    RefinementKey: "te_feel:fruit"),
                Opt("q_te_feel_rich", "Đậm đà", "đậm", null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { Body = "round", Finish = "linger", Energy = "still" },
                    RefinementKey: "te_feel:rich")
            ]),
        new(
            "q_te_moment",
            "Bạn đang uống vào lúc nào?",
            [
                Opt("q_te_moment_morning", "Buổi sáng", "sáng", null, null,
                    new DrinkSensoryProfile { Energy = "lifted", Acidity = "balanced" },
                    RefinementKey: "te_moment:morning"),
                Opt("q_te_moment_afternoon", "Buổi chiều", "chiều", null, null,
                    new DrinkSensoryProfile { Energy = "focused", SocialMood = "gathered" },
                    RefinementKey: "te_moment:afternoon"),
                Opt("q_te_moment_relax", "Thư giãn", "thư giãn", null, null,
                    new DrinkSensoryProfile { Energy = "still", SocialMood = "quiet", Texture = "satin" },
                    RefinementKey: "te_moment:relax")
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildMatchaQuestions() =>
    [
        new(
            "q_ma_style",
            "Bạn muốn matcha thế nào?",
            [
                Opt("q_ma_style_pure", "Matcha thuần", "thuần", null, BeverageFamilyGrounding.Matcha,
                    new DrinkSensoryProfile { Sweetness = "restrained", Texture = "satin", Body = "round" },
                    RefinementKey: "ma_style:pure"),
                Opt("q_ma_style_milk", "Có sữa", "có sữa", null, BeverageFamilyGrounding.Matcha,
                    new DrinkSensoryProfile { Sweetness = "gentle", Texture = "velvet", Body = "round" },
                    RefinementKey: "ma_style:milk"),
                Opt("q_ma_style_sweet", "Ngọt dịu", "ngọt", null, BeverageFamilyGrounding.Matcha,
                    new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet" },
                    RefinementKey: "ma_style:sweet")
            ]),
        new(
            "q_ma_sweet",
            "Độ ngọt?",
            [
                Opt("q_ma_sweet_low", "Ít ngọt", "ít ngọt", null, null,
                    new DrinkSensoryProfile { Sweetness = "restrained" }),
                Opt("q_ma_sweet_medium", "Vừa", "vừa", null, null,
                    new DrinkSensoryProfile { Sweetness = "gentle" }),
                Opt("q_ma_sweet_high", "Ngọt", "ngọt", null, null,
                    new DrinkSensoryProfile { Sweetness = "luscious" })
            ]),
        new(
            "q_ma_temp",
            "Nóng hay đá?",
            [
                Opt("q_ma_temp_hot", "Nóng", "nóng", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "warming" }),
                Opt("q_ma_temp_iced", "Đá", "lạnh", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling" }),
                Opt("q_ma_temp_any", "Tùy", "tùy", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "temperate" })
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildFruitQuestions() =>
    [
        new(
            "q_fr_profile",
            "Hướng vị nào bạn muốn?",
            [
                Opt("q_fr_profile_sweet", "Ngọt", "ngọt", null, BeverageFamilyGrounding.Fruit,
                    new DrinkSensoryProfile { Sweetness = "luscious", Energy = "still" },
                    RefinementKey: "fr_profile:sweet"),
                Opt("q_fr_profile_sour", "Chua", "chua", null, BeverageFamilyGrounding.Fruit,
                    new DrinkSensoryProfile { Acidity = "lifted", Finish = "clean", Energy = "lifted" },
                    RefinementKey: "fr_profile:sour"),
                Opt("q_fr_profile_fresh", "Thanh", "thanh", null, BeverageFamilyGrounding.Fruit,
                    new DrinkSensoryProfile { Acidity = "crystalline", Finish = "clean", Energy = "lifted" },
                    RefinementKey: "fr_profile:fresh"),
                Opt("q_fr_profile_tropical", "Nhiệt đới", "nhiệt đới", null, BeverageFamilyGrounding.Fruit,
                    new DrinkSensoryProfile { AromaFamily = "tropical", Sweetness = "gentle", Energy = "playful" },
                    RefinementKey: "fr_profile:tropical")
            ]),
        new(
            "q_fr_cold",
            "Mức lạnh?",
            [
                Opt("q_fr_cold_deep", "Rất lạnh", "rất lạnh", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling", Energy = "lifted" },
                    RefinementKey: "fr_cold:deep"),
                Opt("q_fr_cold_light", "Mát nhẹ", "mát nhẹ", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "temperate" },
                    RefinementKey: "fr_cold:light"),
                Opt("q_fr_cold_any", "Tùy", "tùy", null, null,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling" },
                    RefinementKey: "fr_cold:any")
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildSignatureQuestions() =>
    [
        new(
            "q_sg_intent",
            "Bạn muốn trải nghiệm gì?",
            [
                Opt("q_sg_intent_famous", "Món nổi tiếng của quán", "nổi tiếng", null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { SocialMood = "gathered", Finish = "linger" },
                    MoodKey: "focus", RefinementKey: "sg_intent:famous"),
                Opt("q_sg_intent_surprise", "Bất ngờ tôi đi", "bất ngờ", null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "playful", Finish = "linger" },
                    MoodKey: "adventurous", RefinementKey: "sg_intent:surprise"),
                Opt("q_sg_intent_instagram", "Đẹp để chụp", "đẹp", null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "playful", SocialMood = "gathered", Sweetness = "gentle" },
                    MoodKey: "bright", RefinementKey: "sg_intent:instagram"),
                Opt("q_sg_intent_barista", "Gợi ý của barista", "barista", null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "focused", Finish = "linger" },
                    MoodKey: "focus", RefinementKey: "sg_intent:barista")
            ])
    ];

    private static GuidedOptionSeed Opt(
        string id,
        string label,
        string emotional,
        string? branchKey,
        string? categoryIntent,
        DrinkSensoryProfile sensory,
        string? MoodKey = null,
        string? RefinementKey = null,
        string? FlavorTagsJson = null) =>
        new(
            id,
            label,
            emotional,
            sensory,
            MoodKey,
            RefinementKey,
            1m,
            FlavorTagsJson,
            categoryIntent,
            null,
            branchKey);

    private sealed record ClientCatalogDto(
        string SetId,
        string EntryQuestionId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Branches,
        IReadOnlyList<ClientQuestionDto> Questions);

    private sealed record ClientQuestionDto(string QuestionId, string Prompt, IReadOnlyList<ClientOptionDto> Options);

    internal sealed record ClientOptionDto(
        string OptionId,
        string Label,
        string? Reflection = null,
        string? BranchKey = null);
}

public sealed record GuidedQuestionSeed(
    string QuestionId,
    string Prompt,
    IReadOnlyList<GuidedOptionSeed> Options,
    string? Description = null);

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
    string? GuestReflection = null,
    string? BranchKey = null);
