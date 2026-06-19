using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>
/// RAG sommelier: embed guest mood → pgvector menu retrieval → short GPT completion grounded on candidates only.
/// Falls back to <see cref="SimulatedSommelierService"/> on missing key, timeouts, retries exhausted, or invalid output.
/// </summary>
public sealed class OpenAiRagSommelierService(
    IOptions<SommelierOpenAiOptions> options,
    SimulatedSommelierService fallback,
    SommelierVectorRetriever retriever,
    SommelierMenuEmbeddingIndexer indexer,
    IMemoryCache cache,
    IApplicationDbContext db,
    IMenuInventoryGate inventoryGate,
    ILogger<OpenAiRagSommelierService> logger) : ISommelierService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SommelierOpenAiOptions _opts = options.Value;

    public Task<SommelierSuggestion> SuggestAsync(SommelierGuideRequest request, CancellationToken cancellationToken = default) =>
        SuggestInternalAsync(request, cancellationToken);

    private async Task<SommelierSuggestion> SuggestInternalAsync(SommelierGuideRequest request, CancellationToken cancellationToken)
    {
        var semantic = string.IsNullOrWhiteSpace(request.SemanticQuery)
            ? (string.IsNullOrWhiteSpace(request.GuestLine) ? " " : request.GuestLine.Trim())
            : request.SemanticQuery.Trim();
        if (semantic.Length == 0)
            semantic = " ";

        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            return await fallback.SuggestAsync(request, cancellationToken);

        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken);

        var cacheKey = BuildCacheKey(request, semantic, blocked);
        if (cache.TryGetValue(cacheKey, out SommelierSuggestion? hit) && hit is not null)
            return hit;

        float[] queryVector;
        try
        {
            queryVector = await RunWithRetriesAsync(
                async ct => await EmbedQueryAsync(semantic, ct),
                cancellationToken,
                "embedding");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sommelier embedding failed after retries; using fallback.");
            return await fallback.SuggestAsync(request, cancellationToken);
        }

        var take = Math.Clamp(_opts.MaxRetrievedMenuItems, 1, 12);
        var pool = Math.Clamp(take * 4, take, 40);
        var familyKey = BeverageFamilyGrounding.NormalizeFamilyKey(request.BeverageFamilyKey);
        var familyDisplay = BeverageFamilyGrounding.DisplayName(familyKey);
        var raw = await retriever.RetrieveNearestAsync(queryVector, pool, familyKey, cancellationToken);
        var retrievedCount = raw.Count;
        var rejectedForFamily = familyKey is null ? 0 : raw.Count(c => !IsCandidateInFamily(c, familyKey));
        raw = ApplyFamilyLock(raw, familyKey);
        raw = raw.Where(c => !blocked.Contains(c.Id)).ToList();
        raw = await PrependMissingPreviousLeadRowAsync(request, raw, blocked, familyKey, cancellationToken);
        var candidates = await FuseAndLoadAsync(request, raw, take, cancellationToken);
        candidates = ApplyFamilyLock(candidates, familyKey);
        candidates = candidates.Where(c => !blocked.Contains(c.Id)).ToList();
        if (candidates.Count == 0)
        {
            logger.LogInformation("No embedded menu rows for RAG; refreshing embeddings then retrying once.");
            try
            {
                await indexer.EnsureEmbeddingsCurrentAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Embedding refresh failed.");
            }

            raw = await retriever.RetrieveNearestAsync(queryVector, pool, familyKey, cancellationToken);
            retrievedCount += raw.Count;
            rejectedForFamily += familyKey is null ? 0 : raw.Count(c => !IsCandidateInFamily(c, familyKey));
            raw = ApplyFamilyLock(raw, familyKey);
            raw = raw.Where(c => !blocked.Contains(c.Id)).ToList();
            raw = await PrependMissingPreviousLeadRowAsync(request, raw, blocked, familyKey, cancellationToken);
            candidates = await FuseAndLoadAsync(request, raw, take, cancellationToken);
            candidates = ApplyFamilyLock(candidates, familyKey);
            candidates = candidates.Where(c => !blocked.Contains(c.Id)).ToList();
            if (candidates.Count == 0)
            {
                logger.LogInformation(
                    "RAG retrieval empty inside beverage family {Family}; using simulated sommelier without cross-family drift.",
                    familyDisplay);
                return await fallback.SuggestAsync(request, cancellationToken);
            }
        }

        if (candidates.Count == 0)
        {
            logger.LogInformation("No in-stock candidates after pantry filter; using simulated sommelier.");
            return await fallback.SuggestAsync(request, cancellationToken);
        }

        logger.LogInformation(
            "Sommelier grounding: family={Family}; retrieved={Retrieved}; rejectedFamily={Rejected}; candidatePool={Candidates}; finalCandidates={Items}",
            familyDisplay,
            retrievedCount,
            rejectedForFamily,
            candidates.Count,
            string.Join(", ", candidates.Select(c => c.Name + " [" + c.CategoryName + "]")));

        SommelierSuggestion? fromModel = null;
        try
        {
            fromModel = await RunWithRetriesAsync(
                async ct => await CompleteSommelierAsync(request, candidates, ct),
                cancellationToken,
                "chat");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sommelier chat completion failed after retries.");
        }

        var result = fromModel is not null && ValidateAgainstMenu(fromModel, candidates, familyKey)
            ? fromModel
            : await BuildGroundedFallbackAsync(request, candidates, cancellationToken);

        if (!ValidateAgainstMenu(result, candidates, familyKey))
        {
            logger.LogWarning(
                "Sommelier final validation rejected a cross-family result for family {Family}; rebuilding from scoped candidates.",
                familyDisplay);
            result = await BuildGroundedFallbackAsync(request, candidates, cancellationToken);
        }

        cache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Clamp(_opts.CacheDurationMinutes, 1, 120))
            });

        return result;
    }

    private string BuildCacheKey(SommelierGuideRequest request, string semanticQuery, IReadOnlySet<Guid> blocked)
    {
        var norm = semanticQuery.Trim().ToLowerInvariant();
        var scope = request.SessionId?.ToString("N") ?? "anon";
        var rk = request.RefinementKey?.Trim().ToLowerInvariant() ?? "";
        var tier = ((byte)request.RefinementTier).ToString();
        var depth = request.SessionRefinementDepth?.ToString() ?? "0";
        var ol = GuestOutputLanguage.Normalize(request.OutputLanguage);
        var family = BeverageFamilyGrounding.NormalizeFamilyKey(request.BeverageFamilyKey) ?? "";
        var inv = blocked.Count == 0
            ? "0"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', blocked.OrderBy(x => x)))))[..16];
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                norm + "|" + scope + "|" + rk + "|" + tier + "|" + depth + "|" + ol + "|" + family + "|" + inv + "|" + _opts.ChatModel + "|" +
                _opts.EmbeddingModel));
        return "sommelier:rag:" + Convert.ToHexString(bytes);
    }

    private async Task<IReadOnlyList<SommelierMenuCandidate>> PrependMissingPreviousLeadRowAsync(
        SommelierGuideRequest request,
        IReadOnlyList<SommelierMenuCandidate> vectorOrdered,
        IReadOnlySet<Guid> blocked,
        string? familyKey,
        CancellationToken cancellationToken)
    {
        if (request.PreviousLeadMenuItemId is not Guid pid)
            return vectorOrdered;
        if (blocked.Contains(pid))
            return vectorOrdered;
        if (vectorOrdered.Any(c => c.Id == pid))
            return vectorOrdered;

        var row = await db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == pid && m.IsAvailable && !m.IsArchived, cancellationToken);
        if (row is null)
            return vectorOrdered;
        if (!BeverageFamilyGrounding.Matches(familyKey, row.Category.Name, row.Name, row.ItemType, row.IngredientBreakdown, row.FlavorTags))
            return vectorOrdered;

        var injected = new SommelierMenuCandidate(
            row.Id,
            row.Name,
            row.TastingNotes,
            row.MoodProfile,
            row.Price,
            row.Category.Name,
            row.SensoryProfile,
            row.CaffeineLevel,
            row.SweetnessLevel,
            row.AcidityLevel);

        var merged = new List<SommelierMenuCandidate>(vectorOrdered.Count + 1) { injected };
        merged.AddRange(vectorOrdered);
        return merged;
    }

    private async Task<IReadOnlyList<SommelierMenuCandidate>> FuseAndLoadAsync(
        SommelierGuideRequest request,
        IReadOnlyList<SommelierMenuCandidate> vectorOrdered,
        int take,
        CancellationToken cancellationToken)
    {
        if (vectorOrdered.Count == 0)
            return [];
        DrinkSensoryProfile? prev = null;
        if (request.PreviousLeadMenuItemId is Guid pid)
            prev = await LoadMergedSensoryAsync(pid, cancellationToken);
        var hints = SommelierSensoryQueryMapper.FromRequest(request);
        var beverageIntent = BuildBeverageIntent(request, hints);
        return SommelierCandidateFusion.Fuse(
            vectorOrdered,
            hints,
            prev,
            request.RefinementKey,
            take,
            request.PreviousLeadMenuItemId,
            request.RefinementTier,
            beverageIntent);
    }

    private async Task<DrinkSensoryProfile?> LoadMergedSensoryAsync(Guid menuItemId, CancellationToken cancellationToken)
    {
        var row = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);
        if (row is null)
            return null;
        return row.SensoryProfile.MergeWithLegacyLevels(row.CaffeineLevel, row.SweetnessLevel, row.AcidityLevel);
    }

    private async Task<float[]> EmbedQueryAsync(string prompt, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _opts.RequestTimeoutSeconds)));

        var credential = new ApiKeyCredential(_opts.ApiKey);
        var openAiOptions = new OpenAIClientOptions();
        var client = new EmbeddingClient(_opts.EmbeddingModel, credential, openAiOptions);
        var response = await client.GenerateEmbeddingAsync(prompt, cancellationToken: timeoutCts.Token);
        return response.Value.ToFloats().ToArray();
    }

    private async Task<SommelierSuggestion?> CompleteSommelierAsync(
        SommelierGuideRequest request,
        IReadOnlyList<SommelierMenuCandidate> candidates,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _opts.RequestTimeoutSeconds)));

        var system = """
You are the house sommelier for a refined specialty coffee atelier—not a chatbot.
You only speak about drinks listed in MENU_CONTEXT. Never invent names, origins, or items not in that list.
Tone: warm, precise, unhurried, premium hospitality—like a host at a quiet atelier, not customer support.
No bullet lists. No "As an AI". Short clauses; generous silences implied.
If SESSION_MEMORY is present, continue the same sitting: evolve from the prior lead and refinement path; do not reset as a first meeting. Vary your opening; avoid repeating the same formula as an obvious prior beat unless the guest truly returned to the same place.
When REFINEMENT_BRIDGE appears in the user turn, follow its POLICY for whether the lead should stay or step, how language should reinterpret vs transition, and how alternatives must evolve.
Output: a single JSON object only (no markdown), keys exactly:
menuItemId (uuid string from context), recommendation (drink name exactly as in context),
openingLetter (one or two short sentences, editorial; may acknowledge a gentle shift from the prior cup when memory says so),
tastingNotes (max ~260 chars; sensory, not marketing),
emotionalTone (max ~90 chars),
reason (max ~300 chars; why it fits this guest mood and trajectory),
followUpRefinement (max ~220 chars; quiet suggestion of where the tray could wander next, still grounded),
senseTag (optional, max ~40 chars),
alternatives (array of 0–2 objects only; each object: menuItemId uuid from context, name exact from context, note max ~100 chars whisper why this other cup). Exclude the lead menuItemId from alternatives.
Each MENU_CONTEXT row ends with a compact sensory signature—use it to choose alternatives that sit as gentle neighbors in that sensory space (body, energy, aroma), not generic alternates.
If unsure, pick the single best-fitting lead row by id from MENU_CONTEXT.
""";
        if (GuestOutputLanguage.IsVietnamese(request.OutputLanguage))
        {
            system += """

OUTPUT_LANGUAGE_VI: openingLetter, tastingNotes, emotionalTone, reason, followUpRefinement, senseTag, and alternatives[].note must be written as first-language Vietnamese for a premium specialty café in Vietnam—calm, sensory, contemporary, warm, minimal; not calqued from English; avoid textbook coffee jargon and productivity tone. Keep recommendation and every alternative name exactly as printed in MENU_CONTEXT. Stay concise.
""";
        }

        var familyLine = BeverageFamilyGrounding.PromptConstraintLine(request.BeverageFamilyKey);
        if (!string.IsNullOrWhiteSpace(familyLine))
        {
            system += "\n\nBEVERAGE_FAMILY_LOCK: " + familyLine;
        }
        var coffeePolicy = BeverageIntelligence.PromptPolicyLine(request.BeverageFamilyKey);
        if (!string.IsNullOrWhiteSpace(coffeePolicy))
        {
            system += "\n\n" + coffeePolicy;
        }

        var ctx = new StringBuilder(1024);
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            ctx.Append(i + 1).Append(". id=").Append(c.Id).Append(" | ").Append(c.Name);
            ctx.Append(" | ").Append(c.CategoryName);
            if (!string.IsNullOrWhiteSpace(c.MoodProfile))
                ctx.Append(" | mood:").Append(Clamp(c.MoodProfile, 100));
            if (!string.IsNullOrWhiteSpace(c.TastingNotes))
                ctx.Append(" | notes:").Append(Clamp(c.TastingNotes, 200));
            var sx = c.SensoryLineForModel();
            if (!string.IsNullOrWhiteSpace(sx))
                ctx.Append(" | sensory:").Append(Clamp(sx, 200));
            var profile = BeverageIntelligence.Classify(c.CategoryName, c.Name, c.EffectiveSensory);
            ctx.Append(" | beverage intelligence:").Append(Clamp(profile.SpecialtyLine(), 220));
            ctx.AppendLine();
        }

        var user = new StringBuilder(2400);
        if (!string.IsNullOrWhiteSpace(request.SessionContinuity))
        {
            user.Append("SESSION_MEMORY:\n");
            user.Append(Clamp(request.SessionContinuity, 1200));
            user.Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(familyLine))
            user.Append("LOCKED_BEVERAGE_FAMILY: ").AppendLine(BeverageFamilyGrounding.DisplayName(request.BeverageFamilyKey));
        user.Append("GUEST_MOOD: ").Append(Clamp(request.GuestLine, 500)).Append("\nMENU_CONTEXT:\n").Append(ctx);
        user.Append("\nOUTPUT_LANGUAGE: ").AppendLine(GuestOutputLanguage.Normalize(request.OutputLanguage));
        AppendRefinementBridge(user, request);

        var credential = new ApiKeyCredential(_opts.ApiKey);
        var openAiOptions = new OpenAIClientOptions();
        var chat = new ChatClient(_opts.ChatModel, credential, openAiOptions);

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(system),
            new UserChatMessage(user.ToString())
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = Math.Clamp(_opts.MaxOutputTokens, 120, 520),
            Temperature = request.RefinementTier switch
            {
                SommelierRefinementTier.Subtle => 0.58f,
                SommelierRefinementTier.Moderate => 0.55f,
                SommelierRefinementTier.Bold => 0.62f,
                _ => 0.52f
            }
        };

        var completion = await chat.CompleteChatAsync(messages, options, timeoutCts.Token);
        var raw = ExtractAssistantText(completion.Value);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = StripCodeFences(raw);
        SommelierJsonDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SommelierJsonDto>(raw, JsonOptions);
        }
        catch
        {
            return null;
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Recommendation))
            return null;

        SommelierMenuCandidate? pick = null;
        if (Guid.TryParse(dto.MenuItemId, out var gid))
            pick = candidates.FirstOrDefault(c => c.Id == gid);
        if (pick is null)
        {
            var want = dto.Recommendation.Trim();
            pick = candidates.FirstOrDefault(c =>
                c.Name.Equals(want, StringComparison.OrdinalIgnoreCase));
        }

        if (pick is null)
            return null;

        var hints = SommelierSensoryQueryMapper.FromRequest(request);
        DrinkSensoryProfile? prevCup = null;
        if (request.PreviousLeadMenuItemId is Guid prevId)
            prevCup = await LoadMergedSensoryAsync(prevId, cancellationToken);

        var alts = ParseAndValidateAlternatives(dto, pick, candidates, hints, prevCup, request);

        return new SommelierSuggestion
        {
            MenuItemId = pick.Id,
            Recommendation = pick.Name,
            OpeningLetter = string.IsNullOrWhiteSpace(dto.OpeningLetter)
                ? null
                : Clamp(dto.OpeningLetter, 320),
            TastingNotes = Clamp(dto.TastingNotes ?? "", 340),
            EmotionalTone = Clamp(dto.EmotionalTone ?? "", 100),
            Reason = Clamp(dto.Reason ?? "", 380),
            FollowUpRefinement = string.IsNullOrWhiteSpace(dto.FollowUpRefinement)
                ? null
                : Clamp(dto.FollowUpRefinement, 260),
            SenseTag = string.IsNullOrWhiteSpace(dto.SenseTag) ? null : Clamp(dto.SenseTag, 48),
            Alternatives = alts
        };
    }

    private static void AppendRefinementBridge(StringBuilder user, SommelierGuideRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefinementKey) && request.SessionRefinementDepth is not > 0)
            return;

        user.Append("\n\nREFINEMENT_BRIDGE:\n");
        user.Append("- chip: ").AppendLine(request.RefinementKey ?? "none");
        user.Append("- intensity_tier: ").AppendLine(request.RefinementTier.ToString().ToLowerInvariant());
        if (request.SessionRefinementDepth is int depth)
            user.Append("- sitting_refinement_beat: ").AppendLine(depth.ToString());
        if (request.PreviousLeadMenuItemId is Guid pl)
            user.Append("PRIOR_LEAD_MENU_ID: ").AppendLine(pl.ToString("D"));
        user.AppendLine(RefinementPolicyLines(request));
    }

    private static string RefinementPolicyLines(SommelierGuideRequest request) =>
        request.RefinementTier switch
        {
            SommelierRefinementTier.Subtle =>
                "POLICY (subtle): Prefer the same lead as PRIOR_LEAD_MENU_ID when that id appears in MENU_CONTEXT—the guest asked for nuance, not a new glass. Reinterpret with gentler tastingNotes and a fresh openingLetter; followUpRefinement should suggest a believable sideways glance. Alternatives must be other rows and must not echo the prior beat verbatim.",
            SommelierRefinementTier.Moderate =>
                "POLICY (moderate): You may move the lead one thoughtful step if another row expresses the refinement arc better; justify with adjacent sensory signatures. Alternatives should sit as gentle neighbors to the chosen lead.",
            SommelierRefinementTier.Bold =>
                "POLICY (bold): The guest invited a stronger evolution—lead may shift when a MENU_CONTEXT neighbor explains the move (caffeine, energy, adventure) without abandoning the mood thread. Alternatives can stretch further along that direction while staying on-list.",
            _ =>
                "POLICY (open): Choose the best-fitting lead from MENU_CONTEXT; alternatives as gentle neighbors in sensory space."
        };

    private static double AlternativeCompositeScore(
        SommelierMenuCandidate row,
        DrinkSensoryProfile hints,
        DrinkSensoryProfile? prevCup,
        DrinkSensoryProfile leadCup,
        SommelierGuideRequest request)
    {
        var aff = FlavorAffinityEngine.ScoreHintsVsCup(hints, row.EffectiveSensory);
        aff += BeverageIntelligence.SpecialtyScore(
            BeverageIntelligence.Classify(row.CategoryName, row.Name, row.EffectiveSensory),
            BuildBeverageIntent(request, hints)) * 0.38;
        var traj = FlavorAffinityEngine.TrajectoryFromPrevious(prevCup, row.EffectiveSensory, request.RefinementKey);
        var nPrev = prevCup is null ? 0 : FlavorAffinityEngine.SensoryNeighborStepAffinity(prevCup, row.EffectiveSensory);
        var nLead = FlavorAffinityEngine.SensoryNeighborStepAffinity(leadCup, row.EffectiveSensory);
        var beat = request.SessionRefinementDepth ?? 0;
        var evolve = request.RefinementTier switch
        {
            SommelierRefinementTier.Subtle => nLead * 1.38 + nPrev * 0.38,
            SommelierRefinementTier.Moderate => nLead * 1.52 + nPrev * 0.62,
            SommelierRefinementTier.Bold => nLead * 1.22 + nPrev * 1.08,
            _ => nLead * 0.95 + nPrev * 0.42
        };
        if (request.RefinementTier != SommelierRefinementTier.None || beat > 0)
            evolve += Math.Min(beat, 14) * 0.068;
        return aff + traj + evolve;
    }

    private static BeverageIntent BuildBeverageIntent(SommelierGuideRequest request, DrinkSensoryProfile hints) =>
        BeverageIntelligence.BuildIntent(
            request.BeverageFamilyKey,
            hints,
            [request.MoodKey, request.RefinementKey, request.FlavorDirectionHint, request.GuestLine, request.SemanticQuery],
            request.MoodKey,
            request.RefinementKey,
            request.GuestLine);

    private static IReadOnlyList<SommelierAlternativeCup> ParseAndValidateAlternatives(
        SommelierJsonDto dto,
        SommelierMenuCandidate lead,
        IReadOnlyList<SommelierMenuCandidate> candidates,
        DrinkSensoryProfile hints,
        DrinkSensoryProfile? prevCup,
        SommelierGuideRequest request)
    {
        var list = new List<SommelierAlternativeCup>();
        foreach (var a in dto.Alternatives ?? [])
        {
            if (list.Count >= 2)
                break;
            if (!Guid.TryParse(a.MenuItemId, out var aid) || aid == lead.Id)
                continue;
            var row = candidates.FirstOrDefault(c => c.Id == aid);
            if (row is null)
                continue;
            var note = string.IsNullOrWhiteSpace(a.Note) ? row.MoodProfile : Clamp(a.Note, 120);
            list.Add(new SommelierAlternativeCup(row.Id, row.Name, note));
        }

        if (list.Count > 0)
            return OrderAlternatives(list, candidates, hints, prevCup, lead, request);

        foreach (var row in candidates
                     .Where(c => c.Id != lead.Id)
                     .OrderByDescending(c => AlternativeCompositeScore(c, hints, prevCup, lead.EffectiveSensory, request))
                     .Take(2))
            list.Add(new SommelierAlternativeCup(row.Id, row.Name, row.MoodProfile));

        return list;
    }

    private static IReadOnlyList<SommelierAlternativeCup> OrderAlternatives(
        List<SommelierAlternativeCup> list,
        IReadOnlyList<SommelierMenuCandidate> candidates,
        DrinkSensoryProfile hints,
        DrinkSensoryProfile? prevCup,
        SommelierMenuCandidate lead,
        SommelierGuideRequest request)
    {
        if (list.Count <= 1)
            return list;
        double ScoreAlt(SommelierAlternativeCup a)
        {
            var row = candidates.FirstOrDefault(c => c.Id == a.MenuItemId);
            if (row is null)
                return 0;
            return AlternativeCompositeScore(row, hints, prevCup, lead.EffectiveSensory, request);
        }

        list.Sort((a, b) => ScoreAlt(b).CompareTo(ScoreAlt(a)));
        return list;
    }

    private static string ExtractAssistantText(ChatCompletion completion)
    {
        if (completion.Content is null)
            return "";
        var sb = new StringBuilder();
        foreach (var part in completion.Content)
        {
            if (part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrEmpty(part.Text))
                sb.Append(part.Text);
        }

        return sb.ToString();
    }

    private static string StripCodeFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal))
            return s;
        var firstNl = s.IndexOf('\n');
        if (firstNl > 0)
            s = s[(firstNl + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal))
            s = s[..^3].Trim();
        return s;
    }

    private static bool ValidateAgainstMenu(
        SommelierSuggestion s,
        IReadOnlyList<SommelierMenuCandidate> candidates,
        string? familyKey)
    {
        if (s.MenuItemId is not Guid id)
            return false;
        var row = candidates.FirstOrDefault(c => c.Id == id);
        if (row is null)
            return false;
        if (!string.Equals(s.Recommendation.Trim(), row.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (!row.Name.Contains(s.Recommendation, StringComparison.OrdinalIgnoreCase) &&
                !s.Recommendation.Contains(row.Name, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return IsCandidateInFamily(row, familyKey);
    }

    private static SommelierMenuCandidate SelectFallbackLead(SommelierGuideRequest request, IReadOnlyList<SommelierMenuCandidate> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("MENU_CONTEXT was empty.");
        if (request.RefinementTier == SommelierRefinementTier.Subtle &&
            request.PreviousLeadMenuItemId is Guid pid)
        {
            var stayed = candidates.FirstOrDefault(c => c.Id == pid);
            if (stayed is not null)
                return stayed;
        }

        return candidates[0];
    }

    private static IReadOnlyList<SommelierMenuCandidate> ApplyFamilyLock(
        IReadOnlyList<SommelierMenuCandidate> candidates,
        string? familyKey)
    {
        if (BeverageFamilyGrounding.NormalizeFamilyKey(familyKey) is null)
            return candidates;

        return candidates.Where(c => IsCandidateInFamily(c, familyKey)).ToList();
    }

    private static bool IsCandidateInFamily(SommelierMenuCandidate candidate, string? familyKey) =>
        BeverageFamilyGrounding.Matches(familyKey, candidate.CategoryName, candidate.Name);

    private async Task<SommelierSuggestion> BuildGroundedFallbackAsync(
        SommelierGuideRequest request,
        IReadOnlyList<SommelierMenuCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var vi = GuestOutputLanguage.IsVietnamese(request.OutputLanguage);
        var top = SelectFallbackLead(request, candidates);
        var tasting = !string.IsNullOrWhiteSpace(top.TastingNotes)
            ? Clamp(top.TastingNotes, 300)
            : vi
                ? "Gọn, trung thực, từ thực đơn đang mở—hỏi quầy nếu bạn muốn thêm chi tiết trong ngày."
                : "Structured, honest, and drawn from our current list—ask the barista for the micro-detail of the day.";

        var tone = !string.IsNullOrWhiteSpace(top.MoodProfile)
            ? Clamp(top.MoodProfile, 90)
            : vi
                ? "Lặng · có chừng"
                : "Quiet · Intentional";

        var guest = Clamp(request.GuestLine, 160);
        var hasMemory = !string.IsNullOrWhiteSpace(request.SessionContinuity);
        var sameLeadStay = request.PreviousLeadMenuItemId is Guid gid && top.Id == gid;
        string opening;
        if (request.RefinementTier == SommelierRefinementTier.Subtle && sameLeadStay)
        {
            opening = vi
                ? "Chúng tôi giữ " + top.Name +
                  " trên khay—nghe nhẹ hơn; cùng một ly, đọc chậm và êm hơn."
                : "We keep " + top.Name +
                  " on the tray—listen a touch softer; same pour, a gentler reading of how it moves.";
        }
        else if (hasMemory && !string.IsNullOrWhiteSpace(request.PreviousLeadName) &&
                 !top.Name.Equals(request.PreviousLeadName, StringComparison.OrdinalIgnoreCase))
        {
            opening = vi
                ? "Nối từ ly trước—" + top.Name + " là điểm gần nhất với tông bạn đang tới."
                : "Carrying your last thread forward—" + top.Name + " sits closest to where the light moved.";
        }
        else
        {
            opening = vi
                ? "Nhịp này, có lẽ nên mở bằng " + top.Name +
                  "—từ thực đơn hôm nay, với cùng sự chăm như ở quầy."
                : "For this moment, we’d begin with " + top.Name +
                  "—drawn from tonight’s list with the same care as the bar.";
        }

        DrinkSensoryProfile? prevCup = null;
        if (request.PreviousLeadMenuItemId is Guid prevId)
            prevCup = await LoadMergedSensoryAsync(prevId, cancellationToken);
        var hints = SommelierSensoryQueryMapper.FromRequest(request);
        var alts = candidates
            .Where(c => c.Id != top.Id)
            .OrderByDescending(c => AlternativeCompositeScore(c, hints, prevCup, top.EffectiveSensory, request))
            .Take(2)
            .Select(c => new SommelierAlternativeCup(c.Id, c.Name, c.MoodProfile))
            .ToList();

        var reason = vi
            ? "Với nhịp bạn chọn (" + guest + "), ly này gần nhất với điều bạn mô tả—có mặt, gọn, an."
            : "Given your mood (" + guest + "), this cup sits closest to what you described—honest, available, composed.";
        var follow = sameLeadStay && request.RefinementTier == SommelierRefinementTier.Subtle
            ? vi
                ? "Khi bạn sẵn sàng, chúng tôi vẫn có thể nghiêng khay sang bên—thanh hơn, khô hơn, hoặc êm hơn—mà không đổi ly."
                : "When you’re ready, we can still tilt the tray sideways—brighter lift, drier finish, or a softer hush—without trading the glass."
            : vi
                ? "Nếu phòng đổi, có thể nghiêng khay nhẹ, hoặc để ly này tự kết trong im."
                : "If the light in the room shifts, we can tilt toward something softer on the tray, or let this one finish in silence.";

        return new SommelierSuggestion
        {
            MenuItemId = top.Id,
            Recommendation = top.Name,
            OpeningLetter = opening,
            TastingNotes = tasting,
            EmotionalTone = tone,
            Reason = reason,
            FollowUpRefinement = follow,
            SenseTag = null,
            Alternatives = alts
        };
    }

    private async Task<T> RunWithRetriesAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct, string label)
    {
        var max = Math.Clamp(_opts.MaxRetries, 0, 5);
        Exception? last = null;
        for (var attempt = 0; attempt <= max; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                last = new TimeoutException("OpenAI request timed out.");
            }
            catch (Exception ex) when (IsTransient(ex, ct))
            {
                last = ex;
            }

            if (attempt == max)
                break;

            var delay = TimeSpan.FromMilliseconds(_opts.RetryBaseDelayMilliseconds * Math.Pow(2, attempt));
            logger.LogDebug("Sommelier {Label} retry {Attempt} after {Delay}.", label, attempt + 1, delay);
            await Task.Delay(delay, ct);
        }

        throw last ?? new InvalidOperationException("Sommelier operation failed.");
    }

    private static bool IsTransient(Exception ex, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return false;
        if (ex is OperationCanceledException)
            return true;
        if (ex is not HttpRequestException h)
            return false;
        if (h.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout)
            return true;
        return h.StatusCode is { } code && (int)code >= 500;
    }

    private static string Clamp(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }

    private sealed class SommelierJsonDto
    {
        public string? MenuItemId { get; set; }
        public string? Recommendation { get; set; }
        public string? OpeningLetter { get; set; }
        public string? TastingNotes { get; set; }
        public string? EmotionalTone { get; set; }
        public string? Reason { get; set; }
        public string? FollowUpRefinement { get; set; }
        public string? SenseTag { get; set; }
        public List<SommelierAltJsonDto>? Alternatives { get; set; }
    }

    private sealed class SommelierAltJsonDto
    {
        public string? MenuItemId { get; set; }
        public string? Name { get; set; }
        public string? Note { get; set; }
    }
}
