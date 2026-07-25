using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Web.GuestExperience;

/// <summary>
/// Menu-driven guided sommelier (atelier_v6).
/// Entry chooses a real menu category; each path is 1–2 barista questions.
/// Specialty Coffee uses a dedicated taste-consultation flow (habit → today → food taste → format).
/// </summary>
public static class GuidedSommelierCatalog
{
    public const string QuestionSetId = "atelier_v6";
    public const string EntryQuestionId = "q0";

    public const string BranchSpecialty = "specialty";
    public const string BranchSignature = "signature";
    public const string BranchEspresso = "espresso";
    public const string BranchTea = "tea";
    public const string BranchSmoothie = "smoothie";
    public const string BranchJuice = "juice";
    public const string BranchColdBrew = "coldbrew";
    public const string BranchVietnamese = "vietnamese";

    // Legacy branch keys kept for ResolveBranchKey / old payloads only.
    public const string BranchCoffee = "coffee";
    public const string BranchMatcha = "matcha";
    public const string BranchFruit = "fruit";

    /// <summary>Entry question only — always first.</summary>
    public static IReadOnlyList<GuidedQuestionSeed> Questions { get; } = [BuildEntryQuestion()];

    /// <summary>All questions for CMS seed + client catalog.</summary>
    public static IReadOnlyList<GuidedQuestionSeed> AllQuestions { get; } =
        Questions.Concat(BuildSpecialtyQuestions())
            .Concat(BuildSignatureQuestions())
            .Concat(BuildEspressoQuestions())
            .Concat(BuildTeaQuestions())
            .Concat(BuildSmoothieQuestions())
            .Concat(BuildJuiceQuestions())
            .Concat(BuildColdBrewQuestions())
            .Concat(BuildVietnameseQuestions())
            .ToList();

    /// <summary>Ordered follow-up question IDs per branch (after entry).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Branches { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [BranchSpecialty] = ["q_sp_body", "q_sp_flavor", "q_sp_explore"],
            [BranchSignature] = ["q_sg_feel"],
            [BranchEspresso] = ["q_es_body", "q_es_detail"],
            [BranchTea] = ["q_te_pick"],
            [BranchSmoothie] = ["q_sm_fruit"],
            [BranchJuice] = ["q_ju_fruit"],
            [BranchColdBrew] = ["q_cb_style", "q_cb_fruit"],
            [BranchVietnamese] = ["q_vn_style", "q_vn_temp"]
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
            || questionId.StartsWith("q_sg_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_es_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_te_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_sm_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_ju_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_cb_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_vn_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_cf_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_ma_", StringComparison.OrdinalIgnoreCase)
            || questionId.StartsWith("q_fr_", StringComparison.OrdinalIgnoreCase));

    public static string? ResolveBranchKey(string? entryOptionId)
    {
        if (string.IsNullOrWhiteSpace(entryOptionId))
            return null;
        return entryOptionId.Trim().ToLowerInvariant() switch
        {
            "q0_specialty" => BranchSpecialty,
            "q0_signature" => BranchSignature,
            "q0_espresso" => BranchEspresso,
            "q0_tea" => BranchTea,
            "q0_smoothie" => BranchSmoothie,
            "q0_juice" => BranchJuice,
            "q0_coldbrew" => BranchColdBrew,
            "q0_vietnamese" => BranchVietnamese,
            // Legacy atelier_v5 entry ids
            "q0_coffee" => BranchEspresso,
            "q0_matcha" => BranchTea,
            "q0_fruit" => BranchJuice,
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

        var hasBaristaTree = loaded.Any(q =>
            string.Equals(q.QuestionId, "q_sg_feel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(q.QuestionId, "q_es_body", StringComparison.OrdinalIgnoreCase));
        if (!hasBaristaTree)
            return AllQuestions;

        var hasEntry = loaded.Any(q =>
            string.Equals(q.QuestionId, EntryQuestionId, StringComparison.OrdinalIgnoreCase));
        if (hasEntry)
            return ReplaceObsoleteSpecialtyConversation(
                loaded.Select(OverlayBuiltinQuestionStructure).ToList());

        return AllQuestions;
    }

    /// <summary>
    /// Specialty conversation replacement: drop any pre-current specialty questions still present in CMS loads.
    /// </summary>
    private static IReadOnlyList<GuidedQuestionSeed> ReplaceObsoleteSpecialtyConversation(
        IReadOnlyList<GuidedQuestionSeed> loaded)
    {
        var desiredIds = SpecialtyCoffeeDiscoveryQuestions
            .Select(q => q.QuestionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var specialtyLoaded = loaded
            .Where(q => q.QuestionId.StartsWith("q_sp_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasCurrent = desiredIds.Count > 0
            && desiredIds.All(id => specialtyLoaded.Any(q =>
                string.Equals(q.QuestionId, id, StringComparison.OrdinalIgnoreCase)));
        var hasExtra = specialtyLoaded.Any(q => !desiredIds.Contains(q.QuestionId));
        if (hasCurrent && !hasExtra)
            return loaded;

        var withoutSpecialty = loaded
            .Where(q => !q.QuestionId.StartsWith("q_sp_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var entryIdx = withoutSpecialty.FindIndex(q =>
            string.Equals(q.QuestionId, EntryQuestionId, StringComparison.OrdinalIgnoreCase));
        var insertAt = entryIdx >= 0 ? entryIdx + 1 : 0;
        withoutSpecialty.InsertRange(insertAt, SpecialtyCoffeeDiscoveryQuestions);
        return withoutSpecialty;
    }

    /// <summary>
    /// CMS does not store EndsBranch / RequiresPriorOptionId — overlay from the built-in barista tree.
    /// </summary>
    public static GuidedOptionSeed OverlayBuiltinOptionStructure(GuidedOptionSeed loaded)
    {
        if (!OptionsById.TryGetValue(loaded.OptionId, out var builtin))
            return loaded;

        return loaded with
        {
            EndsBranch = builtin.EndsBranch,
            RequiresPriorOptionId = builtin.RequiresPriorOptionId,
            CategoryIntentKey = string.IsNullOrWhiteSpace(loaded.CategoryIntentKey)
                ? builtin.CategoryIntentKey
                : loaded.CategoryIntentKey,
            FlavorTagsJson = string.IsNullOrWhiteSpace(loaded.FlavorTagsJson)
                ? builtin.FlavorTagsJson
                : loaded.FlavorTagsJson,
            BranchKey = string.IsNullOrWhiteSpace(loaded.BranchKey) ? builtin.BranchKey : loaded.BranchKey
        };
    }

    private static GuidedQuestionSeed OverlayBuiltinQuestionStructure(GuidedQuestionSeed q) =>
        q with { Options = q.Options.Select(OverlayBuiltinOptionStructure).ToList() };

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
        if (optionIds.Count > path.Count)
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var optionsById = path.SelectMany(q => q.Options)
            .ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);
        var picked = new List<GuidedOptionSeed>(optionIds.Count);
        for (var i = 0; i < optionIds.Count; i++)
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

            if (!string.IsNullOrWhiteSpace(opt.RequiresPriorOptionId))
            {
                var gate = opt.RequiresPriorOptionId.Trim();
                var priorOk = optionIds.Take(i).Any(prev =>
                    string.Equals((prev ?? "").Trim(), gate, StringComparison.OrdinalIgnoreCase));
                if (!priorOk)
                {
                    error = "That choice is not available in this set.";
                    return false;
                }
            }

            picked.Add(opt);
        }

        var last = picked[^1];
        var complete = optionIds.Count == path.Count || last.EndsBranch;
        if (!complete)
        {
            error = "Please complete each step of the tasting.";
            return false;
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

    /// <summary>Exact menu drink names carried on leaf answers (JSON string array).</summary>
    public static IReadOnlyList<string> CollectMenuTargets(IReadOnlyList<GuidedOptionSeed> selectedAnswers)
    {
        var targets = new List<string>();
        foreach (var opt in selectedAnswers)
        {
            if (string.IsNullOrWhiteSpace(opt.FlavorTagsJson))
                continue;
            var raw = opt.FlavorTagsJson.Trim();
            if (!raw.StartsWith('['))
                continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(raw);
                if (parsed is null)
                    continue;
                foreach (var name in parsed)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        targets.Add(name.Trim());
                }
            }
            catch
            {
                /* ignore non-array tags (specialty flavor tags) */
            }
        }

        return targets;
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
                Localized(q.Prompt, q.PromptEn),
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
            Localized(option.Label, option.LabelEn),
            LocalizedOptional(
                string.IsNullOrWhiteSpace(option.GuestReflection) ? null : option.GuestReflection.Trim(),
                string.IsNullOrWhiteSpace(option.GuestReflectionEn) ? null : option.GuestReflectionEn.Trim()),
            option.BranchKey,
            option.EndsBranch ? true : null,
            string.IsNullOrWhiteSpace(option.RequiresPriorOptionId) ? null : option.RequiresPriorOptionId.Trim());

    private static ClientLocalizedText Localized(string vi, string? en) =>
        new(
            (vi ?? "").Trim(),
            string.IsNullOrWhiteSpace(en) ? (vi ?? "").Trim() : en.Trim());

    private static ClientLocalizedText? LocalizedOptional(string? vi, string? en)
    {
        if (string.IsNullOrWhiteSpace(vi) && string.IsNullOrWhiteSpace(en))
            return null;
        return Localized(vi ?? "", en);
    }

    private static string Targets(params string[] names) =>
        JsonSerializer.Serialize(names);

    private static GuidedQuestionSeed BuildEntryQuestion() =>
        Q(
            EntryQuestionId,
            "Hôm nay bạn muốn uống gì?",
            "What are you in the mood for today?",
            [
                Opt("q0_specialty", "☕ Specialty Coffee", "☕ Specialty coffee", "specialty", "specialty",
                    BranchSpecialty, BeverageFamilyGrounding.Coffee,
                    new DrinkSensoryProfile { AromaFamily = "floral", Energy = "focused", CaffeineIntensity = 3 },
                    MoodKey: "focus"),
                Opt("q0_signature", "✨ Signature", "✨ Signature", "signature", "signature",
                    BranchSignature, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "playful", SocialMood = "gathered", Finish = "linger" },
                    MoodKey: "adventurous"),
                Opt("q0_espresso", "☕ Espresso", "☕ Espresso", "espresso", "espresso",
                    BranchEspresso, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "round", CaffeineIntensity = 4 },
                    MoodKey: "focus"),
                Opt("q0_vietnamese", "☕ Cà phê Việt", "☕ Vietnamese coffee", "cà phê việt", "vietnamese coffee",
                    BranchVietnamese, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "syrupy", CaffeineIntensity = 4 },
                    MoodKey: "focus"),
                Opt("q0_coldbrew", "🧊 Cold Brew", "🧊 Cold brew", "cold brew", "cold brew",
                    BranchColdBrew, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Energy = "lifted", CaffeineIntensity = 3 },
                    MoodKey: "bright"),
                Opt("q0_tea", "🍵 Trà", "🍵 Tea", "trà", "tea",
                    BranchTea, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "floral", Energy = "still", CaffeineIntensity = 1 },
                    MoodKey: "calm"),
                Opt("q0_smoothie", "🥭 Sinh tố", "🥭 Smoothie", "sinh tố", "smoothie",
                    BranchSmoothie, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { Texture = "velvet", Sweetness = "gentle", CaffeineIntensity = 1 },
                    MoodKey: "bright"),
                Opt("q0_juice", "🍊 Nước ép", "🍊 Juice", "nước ép", "juice",
                    BranchJuice, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Energy = "lifted", CaffeineIntensity = 1 },
                    MoodKey: "bright")
            ],
            "Chọn đúng nhóm trên menu — barista hỏi ngắn rồi gọi tên ly.",
            "Pick a real menu family — a short barista chat, then a cup.");

    /// <summary>Specialty Coffee — three-step taste consultation (body → flavor → exploration).</summary>
    private static IReadOnlyList<GuidedQuestionSeed> BuildSpecialtyQuestions() =>
    [
        Q(
            "q_sp_body",
            "Bạn thường thích cà phê kiểu nào?",
            "What kind of coffee do you usually prefer?",
            [
                Opt("q_sp_body_light", "Nhẹ, dễ uống", "Light and easy", "nhẹ dễ uống", "light and easy", null, null,
                    new DrinkSensoryProfile { Body = "tea_like", Texture = "satin", Finish = "clean", Energy = "still" },
                    RefinementKey: "sc_body:light", FlavorTagsJson: "Light,Tea,CleanFinish"),
                Opt("q_sp_body_balanced", "Cân bằng", "Balanced", "cân bằng", "balanced", null, null,
                    new DrinkSensoryProfile { Body = "round", Sweetness = "rounded", Acidity = "balanced" },
                    RefinementKey: "sc_body:balanced", FlavorTagsJson: "Balanced,Honey,StoneFruit"),
                Opt("q_sp_body_bold", "Đậm và nhiều hương", "Bold and aromatic", "đậm hương", "bold and aromatic", null, null,
                    new DrinkSensoryProfile { Body = "syrupy", Energy = "intense", Finish = "linger", AromaFamily = "cocoa" },
                    RefinementKey: "sc_body:bold", FlavorTagsJson: "Bold,Chocolate,ComplexFinish")
            ]),
        Q(
            "q_sp_flavor",
            "Nếu chọn một hương vị để bắt đầu, bạn thích gì nhất?",
            "If you pick one flavor to start with, what do you like most?",
            [
                Opt("q_sp_flavor_floral", "Hoa và trà", "Florals and tea", "hoa và trà", "florals and tea", null, null,
                    new DrinkSensoryProfile { AromaFamily = "floral", Body = "tea_like", Acidity = "quiet", Finish = "clean" },
                    RefinementKey: "sc_flavor:floral_tea", FlavorTagsJson: "Floral,Tea,Honey,CleanFinish"),
                Opt("q_sp_flavor_fruit", "Trái cây ngọt", "Sweet fruit", "trái cây ngọt", "sweet fruit", null, null,
                    new DrinkSensoryProfile { AromaFamily = "stone_fruit", Sweetness = "rounded", Energy = "playful" },
                    RefinementKey: "sc_flavor:stone_fruit", FlavorTagsJson: "StoneFruit,Honey,SweetFinish"),
                Opt("q_sp_flavor_chocolate", "Socola và vị sâu", "Chocolate and depth", "socola sâu", "chocolate and depth", null, null,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "syrupy", Finish = "linger" },
                    RefinementKey: "sc_flavor:chocolate", FlavorTagsJson: "Chocolate,Bold,ComplexFinish"),
                Opt("q_sp_flavor_berry", "Quả mọng nổi bật", "Bright berries", "quả mọng", "bright berries", null, null,
                    new DrinkSensoryProfile { Energy = "playful", Finish = "linger", Acidity = "crystalline", AromaFamily = "floral" },
                    RefinementKey: "sc_flavor:berry", FlavorTagsJson: "Berry,Honey,SweetFinish,ComplexFinish")
            ]),
        Q(
            "q_sp_explore",
            "Bạn muốn ly cà phê hôm nay mở ra thế nào trong miệng?",
            "How should today's cup unfold on the palate?",
            [
                Opt("q_sp_explore_linear", "Một dòng vị rõ ràng, sạch", "One clear, clean line", "dòng vị sạch", "clear clean line", null, null,
                    new DrinkSensoryProfile { Body = "tea_like", Finish = "clean", Acidity = "quiet", Texture = "satin" },
                    RefinementKey: "sc_explore:linear", FlavorTagsJson: "Tea,Floral,CleanFinish,Linear"),
                Opt("q_sp_explore_juicy", "Ngọt bao quanh cả ngụm", "Sweetness wrapping the sip", "ngọt bao quanh", "wrapping sweetness", null, null,
                    new DrinkSensoryProfile { Sweetness = "rounded", Texture = "satin", AromaFamily = "stone_fruit", Body = "round" },
                    RefinementKey: "sc_explore:juicy", FlavorTagsJson: "StoneFruit,Honey,SweetFinish,Juicy"),
                Opt("q_sp_explore_deepening", "Đậm dần về phía hậu vị", "Getting deeper toward the finish", "đậm dần", "deepening finish", null, null,
                    new DrinkSensoryProfile { Body = "syrupy", Finish = "linger", AromaFamily = "cocoa", Energy = "focused" },
                    RefinementKey: "sc_explore:deepening", FlavorTagsJson: "Chocolate,Bold,ComplexFinish,Deepening"),
                Opt("q_sp_explore_layered", "Nhiều lớp, đổi nhẹ khi nguội", "Layers that shift as it cools", "nhiều lớp", "shifting layers", null, null,
                    new DrinkSensoryProfile { Finish = "linger", Energy = "playful", Acidity = "crystalline", Body = "round" },
                    RefinementKey: "sc_explore:layered", FlavorTagsJson: "Berry,Honey,ComplexFinish,Layered")
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildSignatureQuestions() =>
    [
        Q(
            "q_sg_feel",
            "Hôm nay bạn muốn cảm giác gì?",
            "What kind of feeling are you after today?",
            [
                Opt("q_sg_feel_creamy", "🥥 Béo ngậy", "🥥 Creamy & rich", "béo ngậy", "creamy",
                    null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Texture = "velvet", Sweetness = "luscious", Body = "round" },
                    MoodKey: "comfort", FlavorTagsJson: Targets("Coco Bơ")),
                Opt("q_sg_feel_green", "🌿 Mát xanh", "🌿 Fresh & green", "mát xanh", "green",
                    null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { Energy = "lifted", Acidity = "lifted", Finish = "clean" },
                    MoodKey: "bright", FlavorTagsJson: Targets("Three Kick")),
                Opt("q_sg_feel_citrus", "🍋 Chua sảng khoái", "🍋 Bright & zesty", "chua sảng khoái", "zesty",
                    null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "crystalline", Energy = "playful" },
                    MoodKey: "bright", FlavorTagsJson: Targets("Ginger Singer", "Sunrise")),
                Opt("q_sg_feel_tropical", "🥭 Trái cây nhiệt đới", "🥭 Tropical fruit", "nhiệt đới", "tropical",
                    null, BeverageFamilyGrounding.Signature,
                    new DrinkSensoryProfile { AromaFamily = "tropical", Sweetness = "gentle", Energy = "lifted" },
                    MoodKey: "bright", FlavorTagsJson: Targets("Dưa Fame", "Sunrise"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildEspressoQuestions() =>
    [
        Q(
            "q_es_body",
            "Bạn muốn ly espresso thế nào?",
            "How do you want your espresso cup?",
            [
                Opt("q_es_body_black", "☕ Đậm cà phê", "☕ Coffee-forward", "đậm cà phê", "coffee-forward",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "dense", CaffeineIntensity = 5 },
                    MoodKey: "focus"),
                Opt("q_es_body_milk", "🥛 Có sữa", "🥛 With milk", "có sữa", "with milk",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { Texture = "velvet", Sweetness = "gentle", CaffeineIntensity = 3 },
                    MoodKey: "comfort")
            ]),
        Q(
            "q_es_detail",
            "Gu nào hợp bạn hơn?",
            "Which direction fits you better?",
            [
                Opt("q_es_detail_espresso", "☕ Espresso", "☕ Espresso", "espresso", "espresso",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { Body = "dense", CaffeineIntensity = 5 },
                    RequiresPriorOptionId: "q_es_body_black", FlavorTagsJson: Targets("Espresso")),
                Opt("q_es_detail_americano", "💧 Americano", "💧 Americano", "americano", "americano",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { Body = "light", CaffeineIntensity = 4 },
                    RequiresPriorOptionId: "q_es_body_black", FlavorTagsJson: Targets("Americano")),
                Opt("q_es_detail_classic", "🥛 Sữa cổ điển", "🥛 Classic milk", "sữa cổ điển", "classic milk",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { Texture = "velvet", Sweetness = "gentle", CaffeineIntensity = 3 },
                    RequiresPriorOptionId: "q_es_body_milk", FlavorTagsJson: Targets("Latte", "Capuchino")),
                Opt("q_es_detail_caramel", "🍮 Caramel", "🍮 Caramel", "caramel", "caramel",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet", CaffeineIntensity = 3 },
                    RequiresPriorOptionId: "q_es_body_milk", FlavorTagsJson: Targets("Salted Caramel Latte")),
                Opt("q_es_detail_chocolate", "🍫 Chocolate", "🍫 Chocolate", "chocolate", "chocolate",
                    null, BeverageFamilyGrounding.Espresso,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Sweetness = "luscious", CaffeineIntensity = 3 },
                    RequiresPriorOptionId: "q_es_body_milk", FlavorTagsJson: Targets("Mocha"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildTeaQuestions() =>
    [
        Q(
            "q_te_pick",
            "Bạn nghiêng về hướng nào?",
            "Which tea path are you leaning toward?",
            [
                Opt("q_te_pick_floral", "🌺 Trái cây / hoa", "🌺 Floral fruit tea", "hoa quả", "floral fruit",
                    null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "floral", Acidity = "lifted", Energy = "lifted" },
                    MoodKey: "bright", FlavorTagsJson: Targets("Hibicus Tea")),
                Opt("q_te_pick_mulberry", "🍓 Dâu tằm", "🍓 Mulberry", "dâu tằm", "mulberry",
                    null, BeverageFamilyGrounding.Tea,
                    new DrinkSensoryProfile { AromaFamily = "berry", Sweetness = "gentle", Finish = "clean" },
                    MoodKey: "bright", FlavorTagsJson: Targets("Trà Dâu Tằm")),
                Opt("q_te_pick_matcha", "🍵 Matcha", "🍵 Matcha", "matcha", "matcha",
                    null, BeverageFamilyGrounding.Matcha,
                    new DrinkSensoryProfile { AromaFamily = "floral", Texture = "satin", CaffeineIntensity = 2 },
                    MoodKey: "focus", FlavorTagsJson: Targets("Matcha Muối"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildSmoothieQuestions() =>
    [
        Q(
            "q_sm_fruit",
            "Bạn muốn trái cây nào?",
            "Which fruit are you craving?",
            [
                Opt("q_sm_fruit_mango", "🥭 Xoài", "🥭 Mango", "xoài", "mango",
                    null, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { AromaFamily = "tropical", Sweetness = "luscious", Texture = "velvet" },
                    FlavorTagsJson: Targets("Sinh Tố Xoài")),
                Opt("q_sm_fruit_avocado", "🥑 Bơ", "🥑 Avocado", "bơ", "avocado",
                    null, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { Texture = "velvet", Sweetness = "gentle", Body = "round" },
                    FlavorTagsJson: Targets("Sinh Tố Bơ")),
                Opt("q_sm_fruit_strawberry", "🍓 Dâu", "🍓 Strawberry", "dâu", "strawberry",
                    null, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { AromaFamily = "berry", Sweetness = "gentle", Energy = "lifted" },
                    FlavorTagsJson: Targets("Sinh Tố Dâu")),
                Opt("q_sm_fruit_banana", "🍌 Dâu chuối", "🍌 Strawberry banana", "dâu chuối", "strawberry banana",
                    null, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet", Body = "round" },
                    FlavorTagsJson: Targets("Sinh Tố Dâu Chuối")),
                Opt("q_sm_fruit_kefir", "🥛 Kefir", "🥛 Kefir", "kefir", "kefir",
                    null, BeverageFamilyGrounding.Smoothie,
                    new DrinkSensoryProfile { Texture = "satin", Acidity = "lifted", Energy = "lifted" },
                    FlavorTagsJson: Targets("Daugurt Kefir"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildJuiceQuestions() =>
    [
        Q(
            "q_ju_fruit",
            "Bạn muốn nước ép gì?",
            "Which juice sounds right?",
            [
                Opt("q_ju_fruit_orange", "🍊 Cam", "🍊 Orange", "cam", "orange",
                    null, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Energy = "lifted" },
                    FlavorTagsJson: Targets("Nước Ép Cam")),
                Opt("q_ju_fruit_pineapple", "🍍 Thơm", "🍍 Pineapple", "thơm", "pineapple",
                    null, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { AromaFamily = "tropical", Sweetness = "gentle", Energy = "lifted" },
                    FlavorTagsJson: Targets("Nước Ép Thơm")),
                Opt("q_ju_fruit_lemon", "🍋 Chanh vàng", "🍋 Yellow lemon", "chanh vàng", "lemon",
                    null, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "crystalline", Finish = "clean" },
                    FlavorTagsJson: Targets("Nước Ép Chanh Vàng")),
                Opt("q_ju_fruit_apple", "🍏 Táo xanh", "🍏 Green apple", "táo xanh", "green apple",
                    null, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted", Finish = "clean" },
                    FlavorTagsJson: Targets("Nước Ép Táo Xanh")),
                Opt("q_ju_fruit_watermelon", "🍉 Dưa hấu", "🍉 Watermelon", "dưa hấu", "watermelon",
                    null, BeverageFamilyGrounding.Juice,
                    new DrinkSensoryProfile { Sweetness = "gentle", Energy = "still", Finish = "clean" },
                    FlavorTagsJson: Targets("Nước Ép Dưa Hấu"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildColdBrewQuestions() =>
    [
        Q(
            "q_cb_style",
            "Cold brew kiểu nào?",
            "How do you like your cold brew?",
            [
                Opt("q_cb_style_pure", "☕ Thuần cà phê", "☕ Pure coffee", "thuần cà phê", "pure coffee",
                    null, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "round", CaffeineIntensity = 4 },
                    EndsBranch: true, FlavorTagsJson: Targets("Cold Brew")),
                Opt("q_cb_style_fruit", "🍓 Cà phê + trái cây", "🍓 Coffee + fruit", "cà phê trái cây", "coffee and fruit",
                    null, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { AromaFamily = "berry", Acidity = "lifted", CaffeineIntensity = 3 })
            ]),
        Q(
            "q_cb_fruit",
            "Trái cây nào đi cùng?",
            "Which fruit with it?",
            [
                Opt("q_cb_fruit_orange", "🍊 Cam", "🍊 Orange", "cam", "orange",
                    null, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { AromaFamily = "citrus", Acidity = "lifted" },
                    RequiresPriorOptionId: "q_cb_style_fruit", FlavorTagsJson: Targets("Cold Brew Cam")),
                Opt("q_cb_fruit_apple", "🍏 Táo", "🍏 Apple", "táo", "apple",
                    null, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { Acidity = "lifted", Finish = "clean" },
                    RequiresPriorOptionId: "q_cb_style_fruit", FlavorTagsJson: Targets("Cold Brew Táo")),
                Opt("q_cb_fruit_mulberry", "🍓 Dâu tằm", "🍓 Mulberry", "dâu tằm", "mulberry",
                    null, BeverageFamilyGrounding.ColdBrew,
                    new DrinkSensoryProfile { AromaFamily = "berry", Sweetness = "gentle" },
                    RequiresPriorOptionId: "q_cb_style_fruit", FlavorTagsJson: Targets("Cold Brew Dâu Tằm"))
            ])
    ];

    private static IReadOnlyList<GuidedQuestionSeed> BuildVietnameseQuestions() =>
    [
        Q(
            "q_vn_style",
            "Bạn muốn đen hay có sữa?",
            "Black, or with milk?",
            [
                Opt("q_vn_style_black", "☕ Đen", "☕ Black", "đen", "black",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { AromaFamily = "cocoa", Body = "dense", CaffeineIntensity = 5 }),
                Opt("q_vn_style_milk", "🥛 Có sữa", "🥛 With milk", "có sữa", "with milk",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet", CaffeineIntensity = 4 }),
                Opt("q_vn_style_bacxiu", "🤍 Rất nhiều sữa", "🤍 Extra milky", "bạc xỉu", "bac xiu",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { Sweetness = "luscious", Texture = "velvet", CaffeineIntensity = 2 },
                    EndsBranch: true, FlavorTagsJson: Targets("Bạc Xỉu"))
            ]),
        Q(
            "q_vn_temp",
            "Nóng hay đá?",
            "Hot or iced?",
            [
                Opt("q_vn_temp_hot_black", "🔥 Nóng", "🔥 Hot", "nóng", "hot",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { TemperatureEmotion = "warming", Body = "dense" },
                    RequiresPriorOptionId: "q_vn_style_black", FlavorTagsJson: Targets("Cà Phê Đen")),
                Opt("q_vn_temp_iced_black", "🧊 Đá", "🧊 Iced", "đá", "iced",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling", Body = "dense" },
                    RequiresPriorOptionId: "q_vn_style_black", FlavorTagsJson: Targets("Đen Đá Sài Gòn")),
                Opt("q_vn_temp_hot_milk", "🔥 Nóng", "🔥 Hot", "nóng", "hot",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { TemperatureEmotion = "warming", Sweetness = "luscious" },
                    RequiresPriorOptionId: "q_vn_style_milk", FlavorTagsJson: Targets("Cà Phê Sữa")),
                Opt("q_vn_temp_iced_milk", "🧊 Đá", "🧊 Iced", "đá", "iced",
                    null, BeverageFamilyGrounding.Vietnamese,
                    new DrinkSensoryProfile { TemperatureEmotion = "cooling", Sweetness = "luscious" },
                    RequiresPriorOptionId: "q_vn_style_milk", FlavorTagsJson: Targets("Sữa Đá Sài Gòn"))
            ])
    ];

    private static GuidedQuestionSeed Q(
        string id,
        string promptVi,
        string promptEn,
        IReadOnlyList<GuidedOptionSeed> options,
        string? descriptionVi = null,
        string? descriptionEn = null) =>
        new(id, promptVi, options, descriptionVi, promptEn, descriptionEn);

    private static GuidedOptionSeed Opt(
        string id,
        string labelVi,
        string labelEn,
        string emotionalVi,
        string emotionalEn,
        string? branchKey,
        string? categoryIntent,
        DrinkSensoryProfile sensory,
        string? MoodKey = null,
        string? RefinementKey = null,
        string? FlavorTagsJson = null,
        bool EndsBranch = false,
        string? RequiresPriorOptionId = null) =>
        new(
            id,
            labelVi,
            emotionalVi,
            sensory,
            MoodKey,
            RefinementKey,
            1m,
            FlavorTagsJson,
            categoryIntent,
            null,
            branchKey,
            labelEn,
            emotionalEn,
            null,
            EndsBranch,
            RequiresPriorOptionId);

    private sealed record ClientCatalogDto(
        string SetId,
        string EntryQuestionId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Branches,
        IReadOnlyList<ClientQuestionDto> Questions);

    private sealed record ClientQuestionDto(
        string QuestionId,
        ClientLocalizedText Prompt,
        IReadOnlyList<ClientOptionDto> Options);

    internal sealed record ClientLocalizedText(string Vi, string En);

    internal sealed record ClientOptionDto(
        string OptionId,
        ClientLocalizedText Label,
        ClientLocalizedText? Reflection = null,
        string? BranchKey = null,
        bool? EndsBranch = null,
        string? RequiresPriorOptionId = null);
}

public sealed record GuidedQuestionSeed(
    string QuestionId,
    string Prompt,
    IReadOnlyList<GuidedOptionSeed> Options,
    string? Description = null,
    string? PromptEn = null,
    string? DescriptionEn = null);

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
    string? BranchKey = null,
    string? LabelEn = null,
    string? EmotionalFragmentEn = null,
    string? GuestReflectionEn = null,
    bool EndsBranch = false,
    string? RequiresPriorOptionId = null);
