using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.Sommelier;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Infrastructure.Services;

/// <summary>Predefined mood and flavor tags mapped to house cups—no external models.</summary>
public sealed class SimulatedSommelierService(IApplicationDbContext db, IMenuInventoryGate inventoryGate) : ISommelierService
{
    private sealed record FlavorProfile(
        string[] Tags,
        string DrinkName,
        string TastingNotes,
        string EmotionalTone,
        string Reason,
        string? SenseTag);

    private sealed record FollowPair(string En, string Vi);

    private static readonly IReadOnlyDictionary<string, FollowPair> FollowUpByDrink =
        new Dictionary<string, FollowPair>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sunrise"] = new(
                "If you’d like the cup to settle further, we can move toward something deeper and held.",
                "Nếu muốn ly lắng hơn—có thể sang nhịp sâu và ấm hơn."),
            ["Cold Brew Táo"] = new(
                "When the crispness satisfies, we can let the next pour ease into something warmer and rounder.",
                "Khi độ giòn vừa đủ—lượt kế có thể sang ấm hơn và tròn hơn."),
            ["Three Kick"] = new(
                "When you’re ready to descend, we can trade the heat for cocoa and a slower finish.",
                "Khi muốn hạ nhiệt—có thể sang ca cao và hậu chậm hơn."),
            ["Ginger Singer"] = new(
                "If the warmth gathers too kindly, we can rinse the palate with something more crystalline.",
                "Nếu ấm tụ quá dịu—có thể rửa vị bằng dòng trong hơn."),
            ["Bạc Xỉu"] = new(
                "A second round might keep this softness—or open toward something with a little more presence.",
                "Lượt sau có thể giữ sự êm này—hoặc mở sang ly có thêm chút cá tính."),
            ["Cold Brew"] = new(
                "If depth becomes a conversation, we can lighten the next pour without losing intent.",
                "Nếu sâu thành chuyện dài—lượt kế có thể nhẹ mà không mất ý."),
            ["Latte"] = new(
                "If you’d like the next cup to carry a little more brightness, the house keeps cleaner lines alongside.",
                "Nếu muốn ly sau sáng hơn—nhà vẫn có dòng trong và thanh hơn.")
        };

    private static readonly FlavorProfile[] Profiles =
    [
        new(
            Tags:
            [
                "floral", "flower", "bloom", "blossom", "jasmine", "rose", "delicate",
                "honeysuckle", "lavender", "spring", "light", "lifted", "airy", "petal"
            ],
            DrinkName: "Sunrise",
            TastingNotes:
            "Honeysuckle and white nectarine, a ribbon of Meyer lemon, and a finish like cool linen in morning light.",
            EmotionalTone: "Gentle anticipation · Quiet brightness",
            Reason:
            "You leaned toward something lifted and petalled; Sunrise keeps sweetness whisper-soft while the aromatics carry the conversation.",
            SenseTag: "floral"),
        new(
            Tags:
            [
                "refresh", "refreshing", "crisp", "cool", "clean", "bright", "summer", "thirst", "ice", "iced",
                "invigorating", "wake", "clarity", "mineral", "zest", "citrus", "apple", "tart"
            ],
            DrinkName: "Cold Brew Táo",
            TastingNotes:
            "Green apple, cold river stone, and a long crystalline finish—cool and precise without a heavy note.",
            EmotionalTone: "Stillness after heat · Restorative",
            Reason:
            "Your words asked for air and ease; this cup answers with cool, transparent lines and no weight on the palate.",
            SenseTag: "refreshing"),
        new(
            Tags:
            [
                "adventur", "bold", "daring", "unexpected", "complex", "layer", "spice", "intense", "wild",
                "explore", "curveball", "surprise", "deep", "night", "edge", "kick"
            ],
            DrinkName: "Three Kick",
            TastingNotes:
            "Three distinct pulses—ginger heat, cold brew depth, and a slow citrus finish—moving in deliberate steps.",
            EmotionalTone: "Curiosity rewarded · Confident",
            Reason:
            "You hinted at wanting a cup with opinions; Three Kick moves in deliberate steps rather than a single, polite note.",
            SenseTag: "adventurous"),
        new(
            Tags:
            [
                "warm", "warming", "cozy", "comfort", "ginger", "hug", "fireside", "mulled",
                "evening", "nurture", "soothe", "velvet", "round", "gentle heat"
            ],
            DrinkName: "Ginger Singer",
            TastingNotes:
            "Fresh ginger, a satin ribbon of honey, and a round, held finish—warmth that gathers rather than shouts.",
            EmotionalTone: "Held close · Tender",
            Reason:
            "You reached for language of shelter and glow; this cup keeps the heat polite while the sweetness stays grounded.",
            SenseTag: "warming"),
        new(
            Tags:
            [
                "sweet", "dessert", "treat", "gentle", "soft", "mellow", "easy", "calm", "peace",
                "milk", "white", "smooth", "silky"
            ],
            DrinkName: "Bạc Xỉu",
            TastingNotes:
            "Condensed milk and a whisper of robusta—soft, round, and unhurried. The quietest cup on the menu.",
            EmotionalTone: "Familiar grace · Unhurried",
            Reason:
            "When the mood stays open and gentle, we reach for something classic and quietly generous—Bạc Xỉu holds the room.",
            SenseTag: "comfort"),
        new(
            Tags:
            [
                "chocolate", "cocoa", "rich", "deep", "slow", "sip", "ponder", "study", "read", "contemplative",
                "dark", "grounded", "dense"
            ],
            DrinkName: "Cold Brew",
            TastingNotes:
            "Long-steeped robusta, black cherry, and a whisper of dark cacao. Round, low-acid, patient.",
            EmotionalTone: "Grounded · Afternoon",
            Reason:
            "You seemed to want weight without sharpness; the long steep keeps the cup patient and composed.",
            SenseTag: "cocoa")
    ];

    private static readonly FlavorProfile DefaultProfile = new(
        Tags: ["default"],
        DrinkName: "Latte",
        TastingNotes:
        "Espresso ribbon through steamed milk—clean, balanced, and unhurried. Room for your own thoughts between sips.",
        EmotionalTone: "Balanced · Present",
        Reason:
        "Without a single clear thread, we reach for something honest and composed—a cup that holds without insisting.",
        SenseTag: null);

    public async Task<SommelierSuggestion> SuggestAsync(SommelierGuideRequest request, CancellationToken cancellationToken = default)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken);
        var bias = BuildRefinementBias(request.RefinementKey, request.PreviousLeadName);
        var normalized = (request.SemanticQuery + " " + request.GuestLine + " " + (request.SessionContinuity ?? "") + " " + bias)
            .Trim()
            .ToLowerInvariant();
        if (normalized.Length == 0)
            normalized = " ";

        FlavorProfile? best = null;
        var bestScore = 0;
        foreach (var p in Profiles)
        {
            var score = Score(normalized, p.Tags);
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        var chosen = bestScore > 0 ? best! : DefaultProfile;

        var feedbackNudge = await FeedbackNudgeByMenuItemIdAsync(db, request.MoodKey, cancellationToken);
        var familyKey = BeverageFamilyGrounding.NormalizeFamilyKey(request.BeverageFamilyKey);
        var availablePool = await db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
            .ToListAsync(cancellationToken);
        if (familyKey is not null)
        {
            availablePool = availablePool
                .Where(m => BeverageFamilyGrounding.Matches(
                    familyKey,
                    m.Category.Name,
                    m.Name,
                    m.ItemType,
                    m.IngredientBreakdown,
                    m.FlavorTags))
                .ToList();
        }

        MenuItem? menu = null;
        if (request.RefinementTier == SommelierRefinementTier.Subtle && request.PreviousLeadMenuItemId is Guid stickId)
        {
            menu = availablePool.FirstOrDefault(m => m.Id == stickId);
        }

        if (menu is null)
        {
            menu = availablePool.FirstOrDefault(m =>
                m.Name.Equals(chosen.DrinkName, StringComparison.OrdinalIgnoreCase));
        }

        if (menu is null)
        {
            var selectionHints = SommelierSensoryQueryMapper.FromRequest(request);
            var beverageIntent = BeverageIntelligence.BuildIntent(
                familyKey,
                selectionHints,
                [request.MoodKey, request.RefinementKey, request.FlavorDirectionHint, request.GuestLine, request.SemanticQuery],
                request.MoodKey,
                request.RefinementKey,
                request.GuestLine);
            menu = availablePool
                .OrderByDescending(m => BeverageIntelligence.SpecialtyScore(
                    BeverageIntelligence.Classify(
                        m.Category.Name,
                        m.Name,
                        m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel),
                        m.ItemType,
                        m.IngredientBreakdown,
                        m.FlavorTags),
                    beverageIntent))
                .ThenByDescending(m => feedbackNudge.GetValueOrDefault(m.Id, 0))
                .ThenBy(m => m.Name)
                .FirstOrDefault();
        }

        if (menu is null)
            throw new InvalidOperationException("No available menu items inside the selected beverage family.");

        var profileForTone = Profiles.FirstOrDefault(p =>
                                  string.Equals(p.DrinkName, menu.Name, StringComparison.OrdinalIgnoreCase))
                              ?? chosen;

        var tasting = !string.IsNullOrWhiteSpace(menu.TastingNotes) ? menu.TastingNotes! : profileForTone.TastingNotes;
        var name = menu.Name;
        var vi = GuestOutputLanguage.IsVietnamese(request.OutputLanguage);

        var prev = request.PreviousLeadName?.Trim();
        var sameLead = !string.IsNullOrWhiteSpace(prev) && name.Equals(prev, StringComparison.OrdinalIgnoreCase);
        string opening;
        if (request.RefinementTier == SommelierRefinementTier.Subtle && sameLead)
        {
            opening = vi
                ? $"Chúng tôi giữ {name} trên khay và nghe nhẹ hơn—cùng một ly, đọc chậm hơn trên vòm miệng."
                : $"We keep {name} on the tray and listen softer—same pour, a gentler read for how it moves across the palate.";
        }
        else if (!string.IsNullOrWhiteSpace(prev) && !name.Equals(prev, StringComparison.OrdinalIgnoreCase))
        {
            opening = vi
                ? $"Từ {prev} sang {name}—ghi chú của bạn nghiêng khay mà không đứt nhịp."
                : $"From {prev} toward {name}—your note steers the tray without breaking the thread.";
        }
        else
        {
            opening = vi
                ? $"Nhịp này, có lẽ ta nên mở bằng {name}."
                : $"For this thread, we’d begin with {name}.";
        }

        FollowUpByDrink.TryGetValue(name, out var followPair);
        var follow = followPair is null
            ? (vi
                ? "Nếu phòng đổi, vẫn có thể nghiêng khay—êm hơn, thanh hơn, hoặc chậm hơn—trong thực đơn nhà."
                : "If the room shifts, we can tilt the tray—softer, brighter, or more patient—without leaving the house list.")
            : (vi ? followPair.Vi : followPair.En);
        if (request.RefinementTier == SommelierRefinementTier.Subtle && sameLead)
        {
            follow = vi
                ? "Tiếp theo, vẫn có thể liếc ngang khay—mở hơn, khô hơn, hoặc êm hơn—mà không đổi ly."
                : "Next, we could still glance sideways on the tray—brighter lift, drier finish, or a softer hush—without trading the glass.";
        }

        var emotionalTone = vi ? SimulatedToneVi(request.MoodKey, profileForTone.EmotionalTone) : profileForTone.EmotionalTone;
        var reason = vi ? SimulatedReasonVi(request.MoodKey, guest: request.GuestLine) : profileForTone.Reason;

        DrinkSensoryProfile? prevCup = null;
        if (request.PreviousLeadMenuItemId is Guid pid)
        {
            var pr = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == pid, cancellationToken);
            if (pr is not null)
                prevCup = pr.SensoryProfile.MergeWithLegacyLevels(pr.CaffeineLevel, pr.SweetnessLevel, pr.AcidityLevel);
        }

        var hints = SommelierSensoryQueryMapper.FromRequest(request);
        var altIntent = BeverageIntelligence.BuildIntent(
            familyKey,
            hints,
            [request.MoodKey, request.RefinementKey, request.FlavorDirectionHint, request.GuestLine, request.SemanticQuery],
            request.MoodKey,
            request.RefinementKey,
            request.GuestLine);
        var leadCup = menu.SensoryProfile.MergeWithLegacyLevels(menu.CaffeineLevel, menu.SweetnessLevel, menu.AcidityLevel);
        var altPool = availablePool
            .Where(m => m.Id != menu.Id)
            .ToList();
        var alts = altPool
            .OrderByDescending(m => SimulatedAlternativeScore(m, hints, prevCup, leadCup, request, feedbackNudge, altIntent))
            .Take(2)
            .Select(m => new SommelierAlternativeCup(m.Id, m.Name, m.MoodProfile))
            .ToList();

        return new SommelierSuggestion
        {
            MenuItemId = menu.Id,
            Recommendation = name,
            OpeningLetter = opening,
            TastingNotes = tasting,
            EmotionalTone = emotionalTone,
            Reason = reason,
            FollowUpRefinement = follow,
            SenseTag = profileForTone.SenseTag,
            Alternatives = alts
        };
    }

    private static async Task<Dictionary<Guid, int>> FeedbackNudgeByMenuItemIdAsync(
        IApplicationDbContext db,
        string? moodKey,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-45);
        var mood = moodKey?.Trim().ToLowerInvariant();
        IQueryable<SommelierSuggestionFeedback> q = db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since);
        if (!string.IsNullOrEmpty(mood))
            q = q.Where(f => f.MoodKey != null && f.MoodKey.ToLower() == mood);

        var rows = await q
            .GroupBy(f => f.MenuItemId)
            .Select(g => new
            {
                g.Key,
                Score = g.Sum(f =>
                    f.Outcome == "accepted" || f.Outcome == "ordered"
                        ? 2
                        : f.Outcome == "ignored"
                            ? -1
                            : 0)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Key, x => Math.Clamp(x.Score, -8, 14));
    }

    private static double SimulatedAlternativeScore(
        MenuItem m,
        DrinkSensoryProfile hints,
        DrinkSensoryProfile? prevCup,
        DrinkSensoryProfile leadCup,
        SommelierGuideRequest request,
        IReadOnlyDictionary<Guid, int> feedbackNudge,
        BeverageIntent beverageIntent)
    {
        var cup = m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel);
        var aff = FlavorAffinityEngine.ScoreHintsVsCup(hints, cup);
        aff += BeverageIntelligence.SpecialtyScore(
            BeverageIntelligence.Classify(m.Category?.Name, m.Name, cup, m.ItemType, m.IngredientBreakdown, m.FlavorTags),
            beverageIntent) * 0.38;
        var traj = FlavorAffinityEngine.TrajectoryFromPrevious(prevCup, cup, request.RefinementKey);
        var nPrev = prevCup is null ? 0 : FlavorAffinityEngine.SensoryNeighborStepAffinity(prevCup, cup);
        var nLead = FlavorAffinityEngine.SensoryNeighborStepAffinity(leadCup, cup);
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
        var learn = Math.Clamp(feedbackNudge.GetValueOrDefault(m.Id, 0), -6, 10) * 0.11;
        return aff + traj + evolve + learn;
    }

    private static string SimulatedToneVi(string? moodKey, string fallbackEn)
    {
        return moodKey?.Trim().ToLowerInvariant() switch
        {
            "bright" => "Sáng · trong",
            "slow" => "Ôm · chậm",
            "floral" => "Mềm · kín",
            "adventurous" => "Dấn · có chừng",
            "focus" => "Lặng · rõ",
            _ => string.IsNullOrWhiteSpace(fallbackEn) ? "Lặng · gọn" : fallbackEn
        };
    }

    private static string SimulatedReasonVi(string? moodKey, string guest)
    {
        var g = string.IsNullOrWhiteSpace(guest) ? "nhịp bạn chọn" : guest.Trim();
        if (g.Length > 120)
            g = g[..120].TrimEnd() + "…";
        return moodKey?.Trim().ToLowerInvariant() switch
        {
            "bright" =>
                $"Với {g}, ly này giữ độ trong và ánh—dễ đọc, không ồn.",
            "slow" =>
                $"Với {g}, tìm sự ôm và nhịp chậm—để ở lại trong người.",
            "floral" =>
                $"Với {g}, hương nhẹ và đường kín—như không khí trong phòng nếm.",
            "adventurous" =>
                $"Với {g}, mời một nhịp táo bạo có chừng—lớp đổi mà vẫn nhã.",
            "focus" =>
                $"Với {g}, ưu tiên khoảng trống giữa các ngụm—để nghĩ, đọc, hoặc mưa ngoài kính.",
            _ => $"Hợp {g}—ly này giữ nhịp nhà, gọn và trung thực."
        };
    }

    private static int Score(string haystack, IEnumerable<string> tags)
    {
        var n = 0;
        foreach (var t in tags)
        {
            if (haystack.Contains(t, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    /// <summary>Nudges tag scoring when the guest refines mid-session (no chat transcript).</summary>
    private static string BuildRefinementBias(string? refinementKey, string? previousLeadName)
    {
        if (string.IsNullOrWhiteSpace(refinementKey))
            return "";
        var k = refinementKey.Trim().ToLowerInvariant();
        var lead = previousLeadName?.ToLowerInvariant() ?? "";

        return k switch
        {
            "less_sweet" when lead.Contains("cappuccino", StringComparison.Ordinal) =>
                "filter v60 pour drier transparent lean clarity less milk sugar",
            "less_sweet" when lead.Contains("ginger", StringComparison.Ordinal) =>
                "spice clear ginger restraint dry finish less brown sugar",
            "less_sweet" =>
                "drier restraint transparent lean less sugar cocoa edge",
            "softer" =>
                "velvet hush round gentle mellow soft calm peace",
            "brighter" =>
                "citrus luminous crystalline lifted clean glass yuzu zest air",
            "more_adventurous" =>
                "bold spice shadow layer daring curveball pepper depth",
            "low_caffeine" =>
                "gentle caffeine soft evening decaf steady calm",
            "warmer" =>
                "warm cozy ginger spice hug nurture velvet round cocoa fireside",
            _ => ""
        };
    }
}
