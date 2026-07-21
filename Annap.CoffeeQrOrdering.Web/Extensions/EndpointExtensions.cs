using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Application.Integration;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Infrastructure.Sommelier;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Hubs;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Diagnostics;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class EndpointExtensions
{
    public static WebApplication MapAnnapEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.MapGet("/i18n/{lang}.json", (string lang, I18nBundleService i18n) =>
        {
            var normalized = lang.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
            return Results.Content(i18n.GetBundleJson(normalized), "application/json; charset=utf-8");
        }).AllowAnonymous();
        
        app.MapGet("/api/diag/ping", (HttpContext ctx, IWebHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();
            var req = ctx.Request;
            var origin = req.Headers["Origin"].ToString();
            if (string.IsNullOrEmpty(origin))
                origin = req.Headers["Referer"].ToString();
            var host = req.Host.Value;
            var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
            return Results.Json(new
            {
                host,
                remoteIp,
                origin,
                ok = true,
                serverTimeUtc = DateTime.UtcNow.ToString("o"),
                scheme = req.Scheme,
                userAgent = req.Headers.UserAgent.ToString()
            });
        }).AllowAnonymous();
        
        app.MapGet("/api/operations/room-pulse", async (
            AppDbContext db,
            HubConnectionRegistry hubReg,
            IMenuInventoryGate inventoryGate,
            CancellationToken ct) =>
        {
            var dbOk = await db.Database.CanConnectAsync(ct);
            var (guestHub, staffHub) = hubReg.Snapshot();
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            var lowPantry = await db.Ingredients.AsNoTracking()
                .CountAsync(i => i.IsActive && i.CurrentStock <= i.LowStockThreshold, ct);
            var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
            return Results.Ok(new
            {
                database = dbOk ? "Healthy" : "Unhealthy",
                hub = new { guestFollowers = guestHub, staffBoard = staffHub },
                migrationsPending = pending.Count(),
                pantry = new { lowOrHeldLines = lowPantry, menuCupsPaused = blocked.Count },
                pulseUtc = DateTimeOffset.UtcNow
            });
        }).RequireAuthorization("Staff");
        
        app.MapGet("/api/menu", async (HttpContext http, IApplicationDbContext db, IMenuInventoryGate inventoryGate, ILoggerFactory logFactory) =>
        {
            using var linked = AnnapBootstrapExtensions.CreateRequestTimeout(http, 50);
            var ct = linked.Token;
            var log = logFactory.CreateLogger("Api.Menu");
            var sw = Stopwatch.StartNew();
            try
            {
                var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
                var categories = await db.MenuCategories
                    .AsNoTracking()
                    .OrderBy(c => c.SortOrder)
                    .Include(c => c.Items)
                    .ToListAsync(ct);
        
                var asOf = DateTimeOffset.UtcNow;
                return Results.Ok(new
                {
                    asOfUtc = asOf,
                    categories = categories.Select(c => new
                    {
                        c.Id,
                        c.Name,
                        Items = c.Items
                            .Where(i => i.IsAvailable && !i.IsArchived && !blocked.Contains(i.Id))
                            .OrderBy(i => i.DisplaySortOrder)
                            .ThenBy(i => i.Name)
                            .Select(i => new
                            {
                                i.Id,
                                i.Name,
                                i.Description,
                                i.TastingNotes,
                                i.MoodProfile,
                                i.ShortStory,
                                i.IngredientBreakdown,
                                i.CaffeineLevel,
                                i.SweetnessLevel,
                                i.AcidityLevel,
                                sensory = i.SensoryProfile,
                                i.Price,
                                i.DisplaySortOrder,
                                imageUrl = MenuMediaResolver.ResolveCardImageUrl(
                                    null, null, i.ImageUrl, null, i.Name, c.Name, i.DetailPosterImagePath)
                            })
                            .ToList()
                    })
                });
            }
            finally
            {
                sw.Stop();
                log.LogInformation(
                    "GET /api/menu completed in {ElapsedMs}ms (timeoutOrAbort={Cancelled})",
                    sw.ElapsedMilliseconds,
                    ct.IsCancellationRequested);
            }
        }).AllowAnonymous();
        
        app.MapGet("/api/menu/items/{menuItemId:guid}/alternatives", async (
            Guid menuItemId,
            int? take,
            IApplicationDbContext db,
            IMenuInventoryGate inventoryGate,
            CancellationToken ct) =>
        {
            var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
            var lead = await db.MenuItems.AsNoTracking().Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == menuItemId, ct);
            if (lead is null)
                return Results.NotFound();
        
            var pool = await db.MenuItems.AsNoTracking()
                .Include(m => m.Category)
                .Where(m => m.Id != menuItemId && m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
                .ToListAsync(ct);
            if (pool.Count == 0)
                return Results.Ok(Array.Empty<object>());
        
            var hints = lead.SensoryProfile.MergeWithLegacyLevels(lead.CaffeineLevel, lead.SweetnessLevel, lead.AcidityLevel);
            var cap = Math.Clamp(take ?? 4, 1, 8);
            var sorted = pool
                .OrderByDescending(x => FlavorAffinityEngine.ScoreHintsVsCup(
                    hints,
                    x.SensoryProfile.MergeWithLegacyLevels(x.CaffeineLevel, x.SweetnessLevel, x.AcidityLevel)))
                .ThenBy(x => x.CategoryId == lead.CategoryId ? 0 : 1)
                .ThenBy(x => x.Price)
                .Take(cap)
                .ToList();
        
            return Results.Ok(sorted.Select(i => new
            {
                i.Id,
                i.Name,
                i.MoodProfile,
                i.TastingNotes,
                i.Price,
                categoryName = i.Category?.Name
            }));
        });
        
        
        app.MapPost("/api/guest/guided-sommelier/recommend", async (
            GuidedSommelierRecommendRequest body,
            IApplicationDbContext db,
            IMenuInventoryGate inventoryGate,
            IWebHostEnvironment env,
            ILoggerFactory logFactory,
            CancellationToken ct) =>
        {
            var ids = body.OptionIds;
            var recommendLog = logFactory.CreateLogger("Sommelier.Recommend");
            if (ids is null || ids.Count == 0)
            {
                LogGuidedSommelierReject(recommendLog, ids, "empty_option_ids");
                return Results.BadRequest(new { error = "Please answer each question before we read the list." });
            }
        
            var questions = await GuidedSommelierExperienceCatalog.LoadQuestionSeedsAsync(db, ct);
            var expansion = GuidedSommelierExperienceCatalog.ExpandSpecialtyCoffeeShortcut(questions, ids);
            var resolvedIds = expansion.OptionIds;
            var specialtyLog = logFactory.CreateLogger("Sommelier.Specialty");

            if (!GuidedSommelierExperienceCatalog.TryResolveSommelierAnswers(questions, resolvedIds, out var resolved, out var resolveErr))
            {
                LogGuidedSommelierReject(recommendLog, resolvedIds, resolveErr ?? "resolve_failed");
                return Results.BadRequest(new { error = resolveErr });
            }

            if (GuidedSommelierExperienceCatalog.HasCompleteSpecialtyDiscovery(resolvedIds))
            {
                specialtyLog.LogInformation(
                    "Specialty discovery resolved: Q1={Q1}, Q2={Q2}, Flavor={Flavor}, Experience={Experience}",
                    resolved.ElementAtOrDefault(0)?.OptionId ?? "",
                    resolved.ElementAtOrDefault(1)?.OptionId ?? "",
                    resolved.ElementAtOrDefault(2)?.OptionId ?? "",
                    resolved.ElementAtOrDefault(3)?.OptionId ?? "");
            }
        
            var guestHints = GuidedSommelierCatalog.MergeGuestHints(resolved);
            var familyKey = GuidedSommelierRecommendationEngine.ExtractBeverageFamilyKey(resolved);
            var familyDisplay = BeverageFamilyGrounding.DisplayName(familyKey);
            var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
            var affinity = await GuidedSommelierExperienceCatalog.LoadAffinityBoostsAsync(db, resolvedIds, ct);
            var raw = await db.MenuItems
                .AsNoTracking()
                .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Price,
                    m.TastingNotes,
                    m.ShortStory,
                    m.ImageUrl,
                    m.DetailPosterImagePath,
                    m.MoodProfile,
                    m.Description,
                    m.ItemType,
                    m.IngredientBreakdown,
                    m.FlavorTags,
                    m.CategoryId,
                    CatName = m.Category.Name,
                    m.IsSignature,
                    m.Origin,
                    m.Subtitle,
                    m.ProducerStory,
                    m.SensoryProfile,
                    m.CaffeineLevel,
                    m.SweetnessLevel,
                    m.AcidityLevel
                })
                .ToListAsync(ct);

            var retrievedCount = raw.Count;
            var filteredRaw = string.IsNullOrWhiteSpace(familyKey)
                ? raw
                : raw
                    .Where(m => BeverageFamilyGrounding.Matches(
                        familyKey,
                        m.CatName,
                        m.Name,
                        m.ItemType,
                        m.IngredientBreakdown,
                        m.FlavorTags))
                    .ToList();
            var rejected = string.IsNullOrWhiteSpace(familyKey)
                ? new List<object>()
                : raw
                    .Where(m => !BeverageFamilyGrounding.Matches(
                        familyKey,
                        m.CatName,
                        m.Name,
                        m.ItemType,
                        m.IngredientBreakdown,
                        m.FlavorTags))
                    .Select(m => new { m.Id, m.Name, categoryName = m.CatName })
                    .Take(12)
                    .ToList<object>();

            var isSpecialtyCoffee = GuidedSommelierRecommendationEngine.IsSpecialtyCoffeePath(resolved);
            var isClassicCoffee = GuidedSommelierRecommendationEngine.IsClassicCoffeePath(resolved);
            var wantsCompare = GuidedSommelierRecommendationEngine.WantsCompareTwo(resolved);
            var signatureCandidateCount = filteredRaw.Count(m => m.IsSignature);
            var poolBeforeSignature = filteredRaw.Count;
            if (isSpecialtyCoffee)
            {
                var signatureOnly = filteredRaw.Where(m => m.IsSignature).ToList();
                if (signatureOnly.Count > 0)
                    filteredRaw = signatureOnly;
            }
            else if (isClassicCoffee)
            {
                filteredRaw = filteredRaw
                    .Where(m => !string.Equals(m.CatName, "Specialty Coffee", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            var poolAfterSignature = filteredRaw.Count;

            var rows = filteredRaw.Select(m => new MenuItemScoringRow(
                    m.Id,
                    m.Name,
                    m.Price,
                    m.TastingNotes,
                    m.ShortStory,
                    MenuMediaResolver.ResolveCardImageUrl(
                        null, null, m.ImageUrl, null, m.Name, m.CatName, m.DetailPosterImagePath),
                    m.MoodProfile,
                    m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel),
                    m.CatName))
                .ToList();

            var takeCount = isSpecialtyCoffee
                ? (wantsCompare ? 2 : 1)
                : 5;
            var ranked = GuidedSommelierRecommendationEngine.Rank(
                guestHints,
                resolved,
                rows,
                takeCount,
                affinity);
            var invalidRanked = ranked
                .Where(r => !BeverageFamilyGrounding.Matches(familyKey, rows.FirstOrDefault(x => x.Id == r.MenuItemId)?.CategoryName, r.Name))
                .ToList();
            if (invalidRanked.Count > 0)
            {
                var log = logFactory.CreateLogger("Sommelier.Grounding");
                log.LogWarning(
                    "Guided sommelier rejected {Count} category mismatches after ranking for family {Family}: {Items}",
                    invalidRanked.Count,
                    familyDisplay,
                    string.Join(", ", invalidRanked.Select(x => x.Name)));
                ranked = ranked
                    .Where(r => !invalidRanked.Any(x => x.MenuItemId == r.MenuItemId))
                    .ToList();
            }

            var byMenuId = raw.ToDictionary(x => x.Id, x => x);
            if (isSpecialtyCoffee)
            {
                specialtyLog.LogInformation(
                    "Specialty pipeline: family={Family} ({FamilyKey}), raw={RawCount}, familyFiltered={FamilyFiltered}, signatureCandidates={SignatureCandidates}, poolAfterSignature={PoolAfterSignature}, ranked={RankedCount}",
                    familyDisplay,
                    familyKey ?? "",
                    retrievedCount,
                    poolBeforeSignature,
                    signatureCandidateCount,
                    poolAfterSignature,
                    ranked.Count);

                foreach (var r in ranked)
                {
                    byMenuId.TryGetValue(r.MenuItemId, out var item);
                    specialtyLog.LogInformation(
                        "Specialty ranked result: {Name}, origin={Origin}, isSignature={IsSignature}, category={Category}",
                        r.Name,
                        item?.Origin ?? "",
                        item?.IsSignature ?? false,
                        item?.CatName ?? "");
                }
            }

            var reflection = GuidedSommelierRecommendationEngine.ComposePersonalityReflection(resolved);
            var diagnostics = env.IsDevelopment()
                ? new
                {
                    selectedFamily = familyDisplay,
                    selectedFamilyKey = familyKey,
                    candidatePoolCount = filteredRaw.Count,
                    retrievedItems = raw.Select(m => new { m.Id, m.Name, categoryName = m.CatName }).Take(20).ToList(),
                    filteredItems = filteredRaw.Select(m => new { m.Id, m.Name, categoryName = m.CatName }).Take(20).ToList(),
                    rejectedCategoryMismatches = rejected,
                    finalRankedCandidates = ranked.Select(r => new { r.MenuItemId, r.Name }).ToList(),
                    retrievedCount
                }
                : null;
            return Results.Ok(new
            {
                questionSetId = GuidedSommelierCatalog.QuestionSetId,
                ambient = GuidedSommelierRecommendationEngine.ComposeAmbientLine(resolved),
                personalityReflection = reflection,
                isSpecialtyCoffee,
                results = ranked.Select(r => new
                {
                    r.MenuItemId,
                    r.Name,
                    r.Price,
                    categoryName = byMenuId.TryGetValue(r.MenuItemId, out var categorySrc) ? categorySrc.CatName : "",
                    origin = byMenuId.TryGetValue(r.MenuItemId, out var originSrc) ? (originSrc.Origin ?? "") : "",
                    moodProfile = byMenuId.TryGetValue(r.MenuItemId, out var src) ? (src.MoodProfile ?? "") : "",
                    tastingNotes = r.TastingNotes ?? "",
                    shortStory = r.ShortStory ?? "",
                    imageUrl = r.ImageUrl,
                    emotionalExplanation = r.EmotionalExplanation,
                }),
                groundingDiagnostics = diagnostics
            });
        }).AllowAnonymous().RequireRateLimiting("anon-ai-post");
        
        app.MapGet("/api/guest/guided-sommelier/catalog", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var questions = await GuidedSommelierExperienceCatalog.LoadQuestionSeedsAsync(db, ct);
            var json = GuidedSommelierExperienceCatalog.ToClientJson(questions, GuidedSommelierCatalog.QuestionSetId);
            return Results.Text(json, "application/json");
        }).AllowAnonymous();

        app.MapGet("/api/guest/sommelier-lite/config", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var questions = await GuidedSommelierExperienceCatalog.LoadQuestionSeedsAsync(db, ct);
            var compat = GuestSommelierLiteCompatibility.Assess(questions);
            return Results.Json(compat.ToClientDto());
        }).AllowAnonymous();
        
        app.MapPost("/api/guest/discovery/reveal", async (
            GuestDiscoveryRevealRequest body,
            IApplicationDbContext db,
            IMenuInventoryGate inventoryGate,
            CancellationToken ct) =>
        {
            var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
            var cmsSignatureIds = await db.ExperienceSignatureSlots.AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => s.MenuItemId)
                .ToListAsync(ct);
            var cmsSignatureSet = cmsSignatureIds.Count > 0 ? cmsSignatureIds.ToHashSet() : new HashSet<Guid>();
        
            var settings = await db.ExperienceDiscoverySettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var seasonalOnly = settings?.SeasonalOnlyPool ?? false;
            var allowSeasonal = settings?.AllowSeasonalCups ?? true;
            var preferSignatures = settings?.PreferSignaturesFirst ?? true;
        
            var q = db.MenuItems
                .AsNoTracking()
                .Where(m =>
                    m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id) &&
                    m.IsDiscoveryEligible &&
                    !m.IsHiddenDiscovery && m.DiscoveryWeight > 0 &&
                    (m.IsSignature || m.IsFeatured || m.IsSeasonalHighlight));
        
            if (seasonalOnly)
                q = q.Where(m => m.IsSeasonalHighlight);
            else if (!allowSeasonal)
                q = q.Where(m => !m.IsSeasonalHighlight);
        
            var baseOrdered = preferSignatures
                ? q.OrderByDescending(m => m.IsSignature)
                    .ThenByDescending(m => m.IsSeasonalHighlight)
                    .ThenByDescending(m => m.IsFeatured)
                : q.OrderByDescending(m => m.DiscoveryWeight)
                    .ThenByDescending(m => m.IsSignature);
        
            var raw = await baseOrdered
                .ThenByDescending(m => m.UpdatedAtUtc ?? m.CreatedAtUtc)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Price,
                    m.TastingNotes,
                    m.ShortStory,
                    m.MoodProfile,
                    m.Description,
                    CatName = m.Category.Name,
                    m.ImageUrl,
                    m.DetailPosterImagePath,
                    m.FlavorTags,
                    m.MoodTags,
                    m.IsSignature,
                    m.IsFeatured,
                    m.IsSeasonalHighlight,
                    m.UpdatedAtUtc,
                    m.SensoryProfile,
                    m.CaffeineLevel,
                    m.SweetnessLevel,
                    m.AcidityLevel,
                    m.DiscoveryWeight
                })
                .Take(56)
                .ToListAsync(ct);
        
            if (raw.Count == 0)
            {
                raw = await db.MenuItems
                    .AsNoTracking()
                    .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
                    .OrderByDescending(m => m.IsSignature)
                    .ThenByDescending(m => m.UpdatedAtUtc ?? m.CreatedAtUtc)
                    .Select(m => new
                    {
                        m.Id,
                        m.Name,
                        m.Price,
                        m.TastingNotes,
                        m.ShortStory,
                        m.MoodProfile,
                        m.Description,
                        CatName = m.Category.Name,
                        m.ImageUrl,
                        m.DetailPosterImagePath,
                        m.FlavorTags,
                        m.MoodTags,
                        m.IsSignature,
                        m.IsFeatured,
                        m.IsSeasonalHighlight,
                        m.UpdatedAtUtc,
                        m.SensoryProfile,
                        m.CaffeineLevel,
                        m.SweetnessLevel,
                        m.AcidityLevel,
                        m.DiscoveryWeight
                    })
                    .Take(56)
                    .ToListAsync(ct);
            }
        
            if (raw.Count == 0)
                return Results.Json(new { error = "The list is resting — try again in a moment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        
            var pool = raw.Select(m => new GuestDiscoveryCurator.PoolRow(
                    m.Id,
                    m.Name,
                    m.Price,
                    m.TastingNotes,
                    m.ShortStory,
                    m.MoodProfile,
                    m.Description,
                    m.CatName,
                    m.ImageUrl,
                    m.DetailPosterImagePath,
                    m.FlavorTags,
                    m.MoodTags,
                    m.IsSignature || cmsSignatureSet.Contains(m.Id),
                    m.IsFeatured,
                    m.IsSeasonalHighlight,
                    m.UpdatedAtUtc,
                    m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel),
                    m.DiscoveryWeight))
                .ToList();
        
            var hourUtc = DateTime.UtcNow.Hour;
            try
            {
                var envelopeIx = body.ChosenEnvelopeIndex is >= 0 and <= 2 ? body.ChosenEnvelopeIndex.Value : 0;
                var payload = GuestDiscoveryRevealBuilder.BuildResponse(
                    pool,
                    body,
                    hourUtc,
                    row => MenuMediaResolver.ResolveCardImageUrl(
                        null, null, row.ImageUrl, null, row.Name, row.CategoryName, row.DetailPosterImagePath),
                    settings,
                    envelopeIx);
                return Results.Ok(payload);
            }
            catch (InvalidOperationException)
            {
                return Results.Json(new { error = "The list is resting — try again in a moment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous().RequireRateLimiting("anon-ai-post");
        
        app.MapPost("/api/sommelier/suggest", async (
            HttpContext http,
            SommelierSuggestBody body,
            ISommelierService sommelier,
            ISommelierSessionMemory sessionMemory,
            IApplicationDbContext db,
            ILoggerFactory logFactory) =>
        {
            using var linked = AnnapBootstrapExtensions.CreateRequestTimeout(http, 90);
            var ct = linked.Token;
            var log = logFactory.CreateLogger("Api.SommelierSuggest");
            var sw = Stopwatch.StartNew();
            try
            {
            var p = body.Prompt?.Trim() ?? "";
            var r = body.Refinement?.Trim() ?? "";
            var prompt = p.Length == 0 && r.Length == 0
                ? ""
                : p.Length == 0
                    ? r
                    : r.Length == 0
                        ? p
                        : p + " · " + r;
            var lang = GuestOutputLanguage.Normalize(body.Language);
            if (prompt.Length == 0)
            {
                var err = lang == GuestOutputLanguage.Vietnamese
                    ? "Cho chúng tôi vài dòng về bạn muốn cảm thấy thế nào hôm nay."
                    : "Please share a few words about how you would like to feel.";
                return Results.BadRequest(new { error = err });
            }
        
            var sessionId = body.SessionId ?? Guid.NewGuid();
            var state = sessionMemory.GetOrCreate(sessionId);
            state.PreferredLanguage = lang;
        
            SommelierSessionContinuityBuilder.AppendMood(state, body.MoodKey, body.MoodLabel);
            if (!string.IsNullOrWhiteSpace(body.RefinementKey))
                SommelierSessionContinuityBuilder.AppendRefinement(state, body.RefinementKey);
        
            var continuity = SommelierSessionContinuityBuilder.BuildContinuityBlock(state, lang);
            if (state.PreviousLeadMenuItemId is Guid leadSensoryId)
            {
                var prevItem = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == leadSensoryId && !m.IsArchived, ct);
                if (prevItem is not null)
                {
                    var merged = prevItem.SensoryProfile.MergeWithLegacyLevels(
                        prevItem.CaffeineLevel,
                        prevItem.SweetnessLevel,
                        prevItem.AcidityLevel);
                    var sx = merged.ToSommelierLine();
                    if (!string.IsNullOrWhiteSpace(sx))
                    {
                        var vi = lang == GuestOutputLanguage.Vietnamese;
                        var sensoryLine = vi ? "\n- Điều bạn sẽ cảm nhận từ ly trước: " + sx : "\n- Prior lead sensory: " + sx;
                        continuity = string.IsNullOrWhiteSpace(continuity)
                            ? (vi
                                ? "Buổi ngồi này—cùng một nhịp:" + sensoryLine
                                : "SESSION (same sitting—evolve; do not reset as if first meeting):" + sensoryLine)
                            : continuity + sensoryLine;
                    }
                }
            }
        
            var moodLabel = body.MoodLabel?.Trim();
            var guestLine = moodLabel?.Length > 0
                ? $"{moodLabel}: {p}" + (r.Length > 0 ? $" · {r}" : "")
                : prompt;
        
            var semanticCore = r.Length == 0 ? p : $"{p} {r}".Trim();
            var semanticQuery = string.IsNullOrWhiteSpace(state.FlavorDirection)
                ? semanticCore
                : $"{state.FlavorDirection} | {semanticCore}";
            semanticQuery = semanticQuery.Trim();
            if (semanticQuery.Length > 380)
                semanticQuery = semanticQuery[..380].TrimEnd();
        
            var refinementKey = body.RefinementKey?.Trim().ToLowerInvariant();
            var refinementTier = SommelierRefinementTierMapper.FromRefinementKey(refinementKey);
            var beverageFamilyKey = BeverageFamilyGrounding.NormalizeFamilyKey(body.BeverageFamily);
            var request = new SommelierGuideRequest(
                SemanticQuery: semanticQuery,
                GuestLine: guestLine.Trim(),
                SessionContinuity: string.IsNullOrWhiteSpace(continuity) ? null : continuity,
                SessionId: sessionId,
                RefinementKey: string.IsNullOrWhiteSpace(refinementKey) ? null : refinementKey,
                PreviousLeadName: state.PreviousLeadName,
                MoodKey: body.MoodKey?.Trim().ToLowerInvariant(),
                PreviousLeadMenuItemId: state.PreviousLeadMenuItemId,
                FlavorDirectionHint: state.FlavorDirection,
                RefinementTier: refinementTier,
                SessionRefinementDepth: state.RefinementKeys.Count,
                OutputLanguage: lang,
                BeverageFamilyKey: beverageFamilyKey);
        
            var s = await sommelier.SuggestAsync(request, ct);
        
            state.PreviousLeadMenuItemId = s.MenuItemId;
            state.PreviousLeadName = s.Recommendation;
            sessionMemory.Save(state);
        
            return Results.Ok(new
            {
                sessionId = sessionId.ToString("D"),
                s.MenuItemId,
                recommendation = s.Recommendation,
                openingLetter = s.OpeningLetter,
                tastingNotes = s.TastingNotes,
                emotionalTone = s.EmotionalTone,
                reason = s.Reason,
                followUpRefinement = s.FollowUpRefinement,
                senseTag = s.SenseTag,
                alternatives = s.Alternatives.Select(a => new { a.MenuItemId, a.Name, a.Note }).ToList()
            });
            }
            finally
            {
                sw.Stop();
                log.LogInformation(
                    "POST /api/sommelier/suggest finished in {ElapsedMs}ms (cancelled={Cancelled})",
                    sw.ElapsedMilliseconds,
                    ct.IsCancellationRequested);
            }
        }).RequireRateLimiting("anon-ai-post");
        
        app.MapPost("/api/orders", async (
            HttpContext http,
            CreateOrderRequest request,
            AppDbContext db,
            IOrderStatusNotifier notifier,
            IMenuInventoryGate inventoryGate,
            ILoggerFactory logFactory) =>
        {
            using var linked = AnnapBootstrapExtensions.CreateRequestTimeout(http, 65);
            var ct = linked.Token;
            var log = logFactory.CreateLogger("Api.OrdersPost");
            var sw = Stopwatch.StartNew();
            try
            {
            if (request.VenueTableId == Guid.Empty)
                return Results.BadRequest(new { error = "Please scan the table QR at your seat to continue." });
        
            if (request.Items.Count == 0)
                return Results.BadRequest(new { error = "Add at least one cup to your tray." });

            if (request.Items.Count > OrderSubmitLimits.MaxLineItems)
            {
                return Results.BadRequest(new
                {
                    error = $"Your tray can hold at most {OrderSubmitLimits.MaxLineItems} lines — please send two smaller trays."
                });
            }
        
            foreach (var line in request.Items)
            {
                if (line.Quantity <= 0 || line.Quantity > 99)
                {
                    return Results.BadRequest(new { error = "Quantity must be between 1 and 99." });
                }
            }

            var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(ct);
            foreach (var line in request.Items)
            {
                if (blocked.Contains(line.MenuItemId))
                {
                    return Results.BadRequest(new
                    {
                        error = "Something on your tray is resting—the pantry needs a moment. Refresh the menu and choose a neighbor cup."
                    });
                }
            }
        
            var venueTableId = request.VenueTableId;
            var table = await db.VenueTables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == venueTableId && t.IsActive, ct);
            if (table is null)
                return Results.BadRequest(new { error = "This table is not available right now." });
        
            var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToArray();
            var menuItems = await db.MenuItems
                .Where(mi => menuItemIds.Contains(mi.Id) && mi.IsAvailable && !mi.IsArchived)
                .ToDictionaryAsync(mi => mi.Id, ct);
        
            if (menuItems.Count != menuItemIds.Length)
                return Results.BadRequest(new { error = "Something on the menu has changed. Refresh and try again." });
        
            var idemKey = OrderSubmitHelper.NormalizeIdempotencyKey(http.Request, request.IdempotencyKey);
            if (string.IsNullOrEmpty(idemKey))
            {
                return Results.BadRequest(new
                {
                    error = "A tray receipt key is required. Refresh the page and send your tray again."
                });
            }

            foreach (var line in request.Items)
            {
                if (OrderItemCustomerNoteHelper.Normalize(line.CustomerNote, out var itemNoteTooLong) is null
                    && itemNoteTooLong)
                {
                    return Results.BadRequest(new
                    {
                        error = $"Each item note must be at most {OrderItemCustomerNoteHelper.MaxLength} characters."
                    });
                }
            }

            string? paymentMethodRaw = request.PaymentMethod;
            if (!string.IsNullOrWhiteSpace(paymentMethodRaw))
            {
                var normalizedPayment = OrderPaymentMethods.Normalize(paymentMethodRaw);
                if (normalizedPayment is null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid paymentMethod. Use Cash, Card, BankTransfer, or CashOrCardAtCounter."
                    });
                }

                paymentMethodRaw = normalizedPayment;
            }
            else
            {
                paymentMethodRaw = OrderPaymentMethods.Cash;
            }
        
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var existing = await db.Orders.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.SubmitIdempotencyKey == idemKey, ct);
                    if (existing is not null && !string.IsNullOrEmpty(existing.GuestSessionToken))
                    {
                        await tx.CommitAsync(ct);
                        return OrderSubmitHelper.IdempotentOrderResponse(existing, existing.GuestSessionToken!, true);
                    }
        
                    var token = GuestSessionTokens.Create();
                    var order = new Order
                    {
                        VenueTableId = table.Id,
                        TableCode = table.DisplayCode,
                        GuestSessionToken = token,
                        SubmitIdempotencyKey = idemKey,
                        Status = OrderStatus.Submitted,
                        StatusChangedAtUtc = DateTimeOffset.UtcNow,
                        PaymentMethod = paymentMethodRaw,
                        Items = request.Items.Select(i => new OrderItem
                        {
                            MenuItemId = i.MenuItemId,
                            Quantity = i.Quantity,
                            UnitPrice = menuItems[i.MenuItemId].Price,
                            Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim(),
                            CustomerNote = OrderItemCustomerNoteHelper.Normalize(i.CustomerNote, out _),
                            MenuItemName = menuItems[i.MenuItemId].Name
                        }).ToList()
                    };
        
                    order.RecalculateTotals();

                    if (string.Equals(paymentMethodRaw, OrderPaymentMethods.BankTransfer, StringComparison.Ordinal))
                        order.BillNumber = OrderBillHelper.EnsureBillNumber(order);
        
                    var snapshotAt = DateTimeOffset.UtcNow;
                    var outboxMessage = new KiotVietOutboxMessage
                    {
                        OrderId = order.Id,
                        EventType = "OrderSubmitted",
                        Status = KiotVietOutboxStatus.Pending,
                        Payload = KiotVietOutboxPayloadFactory.Build(order, table, menuItems, snapshotAt),
                        CreatedAtUtc = snapshotAt,
                        UpdatedAtUtc = snapshotAt
                    };
        
                    await db.Orders.AddAsync(order, ct);
                    await db.KiotVietOutboxMessages.AddAsync(outboxMessage, ct);
                    await OperationalAudit.AppendAsync(db, "order.created", null, order.Id,
                        $"table={order.TableCode};items={order.Items.Count}", ct);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
        
                    var pulse = order.UpdatedAtUtc ?? order.CreatedAtUtc;
                    await notifier.NotifyStaffBoardAsync(ct);
                    await notifier.NotifyGuestOrderAsync(order.Id, pulse, ct);
                    await notifier.NotifyGuestOrderWorkflowAsync(
                        order.Id,
                        OrderWorkflowEndpoints.BuildWorkflowPulse(order),
                        ct);
        
                    return OrderSubmitHelper.IdempotentOrderResponse(order, token, false);
                }
                catch (DbUpdateException ex) when (OrderSubmitHelper.IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync(ct);
                    var dup = await db.Orders.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.SubmitIdempotencyKey == idemKey, ct);
                    if (dup is not null && !string.IsNullOrEmpty(dup.GuestSessionToken))
                        return OrderSubmitHelper.IdempotentOrderResponse(dup, dup.GuestSessionToken!, true);
                    throw;
                }
                catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
                {
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear(); // detach all Added entities so the next attempt starts clean
                }
            }
        
            return Results.Json(
                new { error = "The floor is busy—please try your tray again in a breath." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex) when (PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex))
            {
                log.LogError(ex, "{Message}", PaymentWorkflowSchemaGuard.StartupFailureMessage);
                return PaymentWorkflowSchemaGuard.MigrationRequiredResult();
            }
            finally
            {
                sw.Stop();
                log.LogInformation(
                    "POST /api/orders finished in {ElapsedMs}ms (cancelled={Cancelled})",
                    sw.ElapsedMilliseconds,
                    ct.IsCancellationRequested);
            }
        }).AllowAnonymous().RequireRateLimiting("anon-order-post");
        
        app.MapGet("/api/orders/{orderId:guid}", async (HttpRequest http, Guid orderId, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        
            if (order is null) return Results.NotFound();
        
            if (!GuestSessionTokens.Matches(order.GuestSessionToken, http.Query["token"].FirstOrDefault()))
                return Results.NotFound();
        
            return Results.Ok(new
            {
                order.Id,
                order.TableCode,
                order.Status,
                order.TotalAmount,
                Items = order.Items.Select(i => new
                {
                    i.MenuItemId,
                    Name = i.MenuItemName ?? i.MenuItem.Name,
                    i.Quantity,
                    i.UnitPrice,
                    i.Notes
                })
            });
        });
        
        app.MapGet("/api/track/orders/{orderId:guid}", async (
            HttpRequest http,
            Guid orderId,
            AppDbContext db,
            BankTransferQrBuilder bankTransferQr,
            CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        
            if (order is null) return Results.NotFound();
        
            if (!GuestSessionTokens.Matches(order.GuestSessionToken, http.Query["token"].FirstOrDefault()))
                return Results.NotFound();
        
            if (order.Status == OrderStatus.Cancelled)
            {
                return Results.Ok(new
                {
                    order.Id,
                    isCancelled = true,
                    message = "This order is no longer active. If you have questions, please speak with a host."
                });
            }
        
            var p = CustomerTrackStatusHelper.Resolve(order.Status);
            var pendingPayment = StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status);
            var showBill = p.showBill;
            var bill = showBill ? OrderBillHelper.BuildPaidReceipt(order) : null;
            var checkBill = pendingPayment ? OrderBillHelper.BuildCheckBill(order) : null;
            var (pendingStatusVi, pendingStatusEn) = OrderPaymentMethods.PendingStatusLabels(order.PaymentMethod);
            var (methodVi, methodEn) = OrderPaymentMethods.Labels(order.PaymentMethod);
            var transferQr = bankTransferQr.BuildForTrack(order);
            return Results.Ok(new
            {
                order.Id,
                order.TableCode,
                order.CreatedAtUtc,
                order.UpdatedAtUtc,
                order.PaidAtUtc,
                order.CompletedAtUtc,
                order.PaymentMethod,
                paymentMethodLabelVi = methodVi,
                paymentMethodLabelEn = methodEn,
                pendingStatusLabelVi = pendingStatusVi,
                pendingStatusLabelEn = pendingStatusEn,
                step = p.step,
                phaseKey = p.key,
                title = p.titleVi,
                line = p.lineVi,
                titleVi = p.titleVi,
                lineVi = p.lineVi,
                titleEn = p.titleEn,
                lineEn = p.lineEn,
                isComplete = p.isComplete,
                pendingPayment,
                showBill,
                showCheckBill = pendingPayment,
                bill,
                checkBill,
                transferQr,
                items = order.Items.Select(i => new { name = i.MenuItemName ?? i.MenuItem.Name, i.Quantity })
            });
        });
        
        app.MapGet("/api/staff/orders", async (
            HttpContext http,
            bool? recentServed,
            AppDbContext db,
            BankTransferQrBuilder bankTransferQr,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (!StaffAuthorizationHelper.CanViewStaffBoard(http.User))
                return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var log = loggerFactory.CreateLogger("Api.StaffOrders");
            try
            {
                var includeRecentCompleted = recentServed ?? true;
                var completedSince = DateTimeOffset.UtcNow.AddHours(-3);

                var open = await db.Orders
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .ThenInclude(i => i.MenuItem)
                    .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                    .OrderBy(o => o.CreatedAtUtc)
                    .ToListAsync(ct);

                List<Order> recentCompleted = [];
                if (includeRecentCompleted)
                {
                    recentCompleted = await db.Orders
                        .AsNoTracking()
                        .Include(o => o.Items)
                        .ThenInclude(i => i.MenuItem)
                        .Where(o => o.Status == OrderStatus.Completed
                            && (o.CompletedAtUtc ?? o.UpdatedAtUtc ?? o.CreatedAtUtc) >= completedSince)
                        .OrderByDescending(o => o.CompletedAtUtc ?? o.UpdatedAtUtc ?? o.CreatedAtUtc)
                        .Take(24)
                        .ToListAsync(ct);
                }

                var submitted = open
                    .Where(o => StaffOrderBoardColumnHelper.ToColumn(o.Status) == StaffOrderBoardColumnHelper.Submitted)
                    .Select(o => OrderWorkflowEndpoints.ProjectStaffBoardOrder(o, bankTransferQr))
                    .ToList();
                var paid = open
                    .Where(o => StaffOrderBoardColumnHelper.ToColumn(o.Status) == StaffOrderBoardColumnHelper.Paid)
                    .Select(o => OrderWorkflowEndpoints.ProjectStaffBoardOrder(o, bankTransferQr))
                    .ToList();
                var completed = recentCompleted
                    .Select(o => OrderWorkflowEndpoints.ProjectStaffBoardOrder(o, bankTransferQr))
                    .ToList();

                return Results.Ok(new
                {
                    youAre = http.User.Identity?.Name?.Trim(),
                    roles = StaffAuthorizationHelper.Roles(http.User),
                    permissions = new
                    {
                        canMarkPaid = StaffAuthorizationHelper.CanMarkPaid(http.User),
                        canComplete = StaffAuthorizationHelper.CanComplete(http.User),
                        canPrepareItems = StaffAuthorizationHelper.CanPrepareItems(http.User),
                        canManageBills = StaffAuthorizationHelper.CanManageBills(http.User)
                    },
                    submitted,
                    paid,
                    completed,
                    active = open.OrderBy(o => o.TableCode).Select(o => OrderWorkflowEndpoints.ProjectStaffBoardOrder(o, bankTransferQr)).ToList(),
                    recentServed = completed
                });
            }
            catch (Exception ex) when (PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex))
            {
                log.LogError(ex, "{Message}", PaymentWorkflowSchemaGuard.StartupFailureMessage);
                return PaymentWorkflowSchemaGuard.MigrationRequiredResult();
            }
        }).RequireAuthorization("StaffBoardAccess");
        
        app.MapPatch("/api/staff/orders/{orderId:guid}/status", async (
            HttpContext http,
            Guid orderId,
            StaffOrderStatusPatchRequest body,
            AppDbContext db,
            IOrderStatusNotifier notifier,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.StaffStatus))
                return Results.BadRequest(new { error = "staffStatus is required." });
        
            OrderStatus next;
            try
            {
                next = StaffOrderStatusHelper.ParseStaffStatus(body.StaffStatus);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Invalid staffStatus. Use: pending, preparing, finishing, ready, served." });
            }
        
            var actor = http.User.Identity?.Name?.Trim();
        
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
                if (order is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound();
                }
        
                if (order.Status == OrderStatus.Cancelled)
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Order is cancelled." });
                }

                var prev = order.Status;
                if (prev != next)
                {
                    if (!LegacyStaffStatusPatchHelper.IsAllowed(prev, next))
                    {
                        await tx.RollbackAsync(ct);
                        return Results.BadRequest(new { error = LegacyStaffStatusPatchHelper.BlockedMessage(prev, next) });
                    }

                    if (!StaffOrderStatusTransitionHelper.IsValidTransition(prev, next))
                    {
                        await tx.RollbackAsync(ct);
                        var error = prev == OrderStatus.Completed
                            ? "Reopen is not supported from this screen."
                            : $"Invalid staff status transition from {prev} to {next}.";
                        return Results.BadRequest(new { error });
                    }

                    order.StatusChangedAtUtc = DateTimeOffset.UtcNow;
                }

                order.Status = next;
                await OperationalAudit.AppendAsync(db, "order.status", actor, order.Id,
                    $"from={prev};to={next}", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
        
                var pulse = order.UpdatedAtUtc ?? DateTimeOffset.UtcNow;
                await notifier.NotifyGuestOrderAsync(order.Id, pulse, ct);
                await notifier.NotifyStaffBoardAsync(ct);
        
                return Results.Ok(new
                {
                    order.Id,
                    staffStatus = StaffOrderStatusHelper.ToStaffStatus(order.Status),
                    order.UpdatedAtUtc,
                    order.StatusChangedAtUtc
                });
            }
            catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
            {
                await tx.RollbackAsync(ct);
                return Results.Json(
                    new { error = "Another hand moved this ticket first—refresh the board for a quiet read." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization("StaffAdmin");
        
        app.MapPatch("/api/staff/orders/{orderId:guid}/ownership", async (
            HttpContext http,
            Guid orderId,
            StaffOrderOwnershipPatchRequest body,
            AppDbContext db,
            IOrderStatusNotifier notifier,
            CancellationToken ct) =>
        {
            var who = http.User.Identity?.Name?.Trim();
            if (string.IsNullOrEmpty(who))
                return Results.Unauthorized();
        
            var actor = who;
        
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
                if (order is null)
                {
                    await tx.RollbackAsync(ct);
                    return Results.NotFound();
                }
        
                if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
                {
                    await tx.RollbackAsync(ct);
                    return Results.BadRequest(new { error = "Ticket is closed." });
                }
        
                if (body.ClaimBrewing == true)
                {
                    if (!string.IsNullOrEmpty(order.BrewingOwnerStaffName) &&
                        !string.Equals(order.BrewingOwnerStaffName, who, StringComparison.Ordinal))
                    {
                        await tx.RollbackAsync(ct);
                        return Results.Json(
                            new { error = "The bar is already held—coordinate quietly or ask them to set it down." },
                            statusCode: StatusCodes.Status409Conflict);
                    }
        
                    order.BrewingOwnerStaffName = who;
                }
                else if (body.ReleaseBrewing == true)
                    order.BrewingOwnerStaffName = null;
        
                if (body.ClaimServing == true)
                {
                    if (!string.IsNullOrEmpty(order.ServingOwnerStaffName) &&
                        !string.Equals(order.ServingOwnerStaffName, who, StringComparison.Ordinal))
                    {
                        await tx.RollbackAsync(ct);
                        return Results.Json(
                            new { error = "The handoff is already taken—please release together before trading." },
                            statusCode: StatusCodes.Status409Conflict);
                    }
        
                    order.ServingOwnerStaffName = who;
                }
                else if (body.ReleaseServing == true)
                    order.ServingOwnerStaffName = null;
        
                await OperationalAudit.AppendAsync(db, "order.ownership", actor, order.Id,
                    $"brew={(body.ClaimBrewing == true ? "claim" : body.ReleaseBrewing == true ? "release" : "—")};serve={(body.ClaimServing == true ? "claim" : body.ReleaseServing == true ? "release" : "—")}",
                    ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
        
                await notifier.NotifyStaffBoardAsync(ct);
        
                return Results.Ok(new
                {
                    order.Id,
                    order.BrewingOwnerStaffName,
                    order.ServingOwnerStaffName
                });
            }
            catch (DbUpdateException ex) when (OrderSubmitHelper.IsSerializationConflict(ex))
            {
                await tx.RollbackAsync(ct);
                return Results.Json(
                    new { error = "Two hands reached at once—take a breath and try again." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization("StaffAdmin");
        
        app.MapGet("/api/staff/kiotviet/status", async (
            AppDbContext db,
            IOptions<KiotVietOptions> kvOpts,
            CancellationToken ct) =>
        {
            var opts = kvOpts.Value;
        
            var pendingCount = await db.KiotVietOutboxMessages
                .CountAsync(m => m.Status == KiotVietOutboxStatus.Pending, ct);
        
            var failedCount = await db.KiotVietOutboxMessages
                .CountAsync(m => m.Status == KiotVietOutboxStatus.Failed, ct);
        
            var deadLetteredCount = await db.KiotVietOutboxMessages
                .CountAsync(m => m.Status == KiotVietOutboxStatus.DeadLettered, ct);
        
            var lastSuccessLog = await db.KiotVietSyncLogs
                .AsNoTracking()
                .Where(l => l.IsSuccess)
                .OrderByDescending(l => l.OccurredAtUtc)
                .Select(l => (DateTimeOffset?)l.OccurredAtUtc)
                .FirstOrDefaultAsync(ct);
        
            return Results.Ok(new
            {
                isEnabled = opts.IsEnabled,
                pendingCount,
                failedCount,
                deadLetteredCount,
                lastSuccessfulPushUtc = lastSuccessLog
            });
        }).RequireAuthorization("StaffAdmin");
        
        app.MapOrderWorkflowEndpoints();
        app.MapBankTransferWebhookEndpoints();
        
        app.MapPost("/api/sommelier/feedback", async (HttpContext http, SommelierFeedbackRequest body, AppDbContext db) =>
        {
            using var linked = AnnapBootstrapExtensions.CreateRequestTimeout(http, 30);
            var ct = linked.Token;
            if (body.SessionId == Guid.Empty || body.MenuItemId == Guid.Empty)
                return Results.BadRequest(new { error = "sessionId and menuItemId are required." });
        
            var outcome = (body.Outcome ?? "ignored").Trim().ToLowerInvariant();
            if (outcome is not ("accepted" or "ignored" or "ordered"))
                outcome = "ignored";
        
            var row = new SommelierSuggestionFeedback
            {
                SessionId = body.SessionId,
                MenuItemId = body.MenuItemId,
                Outcome = outcome,
                MoodKey = string.IsNullOrWhiteSpace(body.MoodKey) ? null : body.MoodKey.Trim(),
                RefinementKey = string.IsNullOrWhiteSpace(body.RefinementKey) ? null : body.RefinementKey.Trim()
            };
            await db.SommelierSuggestionFeedbacks.AddAsync(row, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { row.Id });
        }).AllowAnonymous().RequireRateLimiting("anon-ai-post");
        
        var orderHub = app.MapHub<OrderTrackingHub>("/hubs/orders");
        if (app.Environment.IsDevelopment())
            orderHub.RequireCors("DevelopmentLan");
        
        app.MapRazorPages();
        return app;
    }

    private static void LogGuidedSommelierReject(
        ILogger logger,
        IReadOnlyList<string>? optionIds,
        string reason)
    {
        static string Pick(IReadOnlyList<string>? ids, Func<string, bool> matches) =>
            ids?.FirstOrDefault(id => matches(id)) ?? "";

        logger.LogWarning(
            "Guided sommelier recommend rejected: {Reason}. Q1={Q1}, Q2={Q2}, Flavor={Flavor}, Experience={Experience}",
            reason,
            Pick(optionIds, id => id.StartsWith("q1_", StringComparison.OrdinalIgnoreCase)),
            Pick(optionIds, id => id.StartsWith("q2_", StringComparison.OrdinalIgnoreCase)),
            Pick(optionIds, id => id.StartsWith("q_sc_flavor", StringComparison.OrdinalIgnoreCase)),
            Pick(optionIds, id => id.StartsWith("q_sc_experience", StringComparison.OrdinalIgnoreCase)));
    }
}
