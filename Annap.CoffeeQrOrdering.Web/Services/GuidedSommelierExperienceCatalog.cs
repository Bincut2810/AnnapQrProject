using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Loads guided sommelier definition from the experience CMS, with fallback to <see cref="GuidedSommelierCatalog"/>.</summary>
public static class GuidedSommelierExperienceCatalog
{
    public const string SpecialtyCoffeeOptionId = "q2_coffee";
    public const string SpecialtyFlavorQuestionId = "q_sc_flavor";
    public const string SpecialtyExperienceQuestionId = "q_sc_experience";

    public sealed record SpecialtyShortcutExpansion(
        IReadOnlyList<string> OptionIds,
        bool Applied,
        IReadOnlyList<string> InjectedDefaults);

    public static bool HasCompleteSpecialtyDiscovery(IReadOnlyList<string> optionIds) =>
        optionIds.Any(id => string.Equals(id, SpecialtyCoffeeOptionId, StringComparison.OrdinalIgnoreCase))
        && optionIds.Any(id => id.StartsWith("q_sc_flavor_", StringComparison.OrdinalIgnoreCase))
        && optionIds.Any(id => id.StartsWith("q_sc_experience_", StringComparison.OrdinalIgnoreCase))
        && optionIds.Count == 4;

    /// <summary>
    /// Legacy no-op: specialty discovery paths send four explicit answers; incomplete coffee payloads fail at resolve.
    /// </summary>
    public static SpecialtyShortcutExpansion ExpandSpecialtyCoffeeShortcut(
        IReadOnlyList<GuidedQuestionSeed> questions,
        IReadOnlyList<string> optionIds)
    {
        _ = questions;
        if (optionIds is null || optionIds.Count == 0)
            return new SpecialtyShortcutExpansion(optionIds ?? Array.Empty<string>(), false, []);

        if (HasCompleteSpecialtyDiscovery(optionIds))
            return new SpecialtyShortcutExpansion(optionIds, false, []);

        return new SpecialtyShortcutExpansion(optionIds, false, []);
    }

    public static bool TryResolveSommelierAnswers(
        IReadOnlyList<GuidedQuestionSeed> loadedQuestions,
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

        if (HasCompleteSpecialtyDiscovery(optionIds))
            return TryResolveSpecialtyCoffeeDiscovery(optionIds, out resolved, out error);

        var core = ExtractCoreQuestions(loadedQuestions);
        return TryResolveOptions(core, optionIds, out resolved, out error);
    }

    public static bool TryResolveSpecialtyCoffeeDiscovery(
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error)
    {
        resolved = Array.Empty<GuidedOptionSeed>();
        if (!HasCompleteSpecialtyDiscovery(optionIds))
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var coffeeQuestions = new[]
        {
            GuidedSommelierCatalog.Questions[0],
            GuidedSommelierCatalog.Questions[1],
            GuidedSommelierCatalog.SpecialtyCoffeeDiscoveryQuestions[0],
            GuidedSommelierCatalog.SpecialtyCoffeeDiscoveryQuestions[1]
        };

        return TryResolveOptions(coffeeQuestions, optionIds, out resolved, out error);
    }

    private static IReadOnlyList<GuidedQuestionSeed> ExtractCoreQuestions(IReadOnlyList<GuidedQuestionSeed> loaded)
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(loaded);
        return merged
            .Where(q => !GuidedSommelierCatalog.IsSpecialtyDiscoveryQuestionId(q.QuestionId))
            .OrderBy(q => q.QuestionId switch
            {
                "q1" => 0,
                "q2" => 1,
                "q3" => 2,
                "q4" => 3,
                _ => 99
            })
            .Take(4)
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<GuidedQuestionSeed>> LoadQuestionSeedsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken,
        string? setKey = null)
    {
        var key = string.IsNullOrWhiteSpace(setKey) ? GuidedSommelierCatalog.QuestionSetId : setKey.Trim();
        try
        {
            var rows = await db.ExperienceGuidedQuestions
                .AsNoTracking()
                .Where(q => q.SetKey == key && q.IsEnabled)
                .OrderBy(q => q.SortOrder)
                .Select(q => new { q.ExternalKey, q.Prompt, q.Id, q.Description })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

            var opts = await db.ExperienceGuidedOptions
                .AsNoTracking()
                .Where(o => o.IsEnabled)
                .OrderBy(o => o.SortOrder)
                .ToListAsync(cancellationToken);

            var byQ = opts.GroupBy(o => o.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

            var list = new List<GuidedQuestionSeed>(rows.Count);
            foreach (var q in rows)
            {
                if (!byQ.TryGetValue(q.Id, out var oRows) || oRows.Count == 0)
                    continue;
                var seeds = new List<GuidedOptionSeed>(oRows.Count);
                foreach (var o in oRows)
                {
                    var hints = DeserializeSensory(o.SensoryProfileJson);
                    var wm = o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier;
                    var hasSubline = !string.IsNullOrWhiteSpace(o.Subline);
                    var emotional = hasSubline
                        ? o.Subline!.Trim()
                        : (o.Description ?? "").Trim();
                    var guestReflection = hasSubline && !string.IsNullOrWhiteSpace(o.Description)
                        ? o.Description.Trim()
                        : null;
                    seeds.Add(new GuidedOptionSeed(
                        o.ExternalKey,
                        o.Label,
                        emotional,
                        hints,
                        string.IsNullOrWhiteSpace(o.MoodKey) ? null : o.MoodKey.Trim(),
                        string.IsNullOrWhiteSpace(o.RefinementKey) ? null : o.RefinementKey.Trim(),
                        wm,
                        string.IsNullOrWhiteSpace(o.FlavorTagsJson) ? null : o.FlavorTagsJson.Trim(),
                        BeverageFamilyGrounding.ResolveFamilyKey(o.ExternalKey, o.Label, o.Subline, o.Description),
                        guestReflection));
                }

                list.Add(new GuidedQuestionSeed(q.ExternalKey, q.Prompt, seeds, q.Description));
            }

            if (list.Count == 0)
                return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);

            return GuidedSommelierCatalog.MergeClientCatalogQuestions(list);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.Questions);
        }
    }

    public sealed record QuestionSetSummary(string SetKey, int QuestionCount, bool IsActive);

    public static async Task<IReadOnlyList<QuestionSetSummary>> GetAvailableSetsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeKey = GuidedSommelierCatalog.QuestionSetId;
            var rows = await db.ExperienceGuidedQuestions
                .AsNoTracking()
                .GroupBy(q => q.SetKey)
                .Select(g => new { SetKey = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return rows
                .OrderByDescending(r => r.SetKey)
                .Select(r => new QuestionSetSummary(r.SetKey, r.Count, r.SetKey == activeKey))
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    /// <summary>
    /// Serializes the guest-facing catalog (core + specialty discovery questions).
    /// </summary>
    public static string ToClientJson(IReadOnlyList<GuidedQuestionSeed> questions, string setId)
    {
        var merged = GuidedSommelierCatalog.MergeClientCatalogQuestions(questions);
        var dto = new ClientCatalogDto(
            setId,
            merged.Select(q => new ClientQuestionDto(
                q.QuestionId,
                q.Prompt,
                q.Options.Select(o =>
                {
                    var mapped = GuidedSommelierCatalog.ToClientOption(o);
                    return new ClientOptionDto(mapped.OptionId, mapped.Label, mapped.Reflection);
                }).ToList())).ToList());
        return JsonSerializer.Serialize(
            dto,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    public static bool TryResolveOptions(
        IReadOnlyList<GuidedQuestionSeed> questions,
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error)
    {
        resolved = Array.Empty<GuidedOptionSeed>();
        if (optionIds is null || optionIds.Count != questions.Count)
        {
            error = "Please complete each step of the tasting.";
            return false;
        }

        var optionsById = questions.SelectMany(q => q.Options).ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);
        var picked = new GuidedOptionSeed[questions.Count];
        for (var i = 0; i < questions.Count; i++)
        {
            var id = (optionIds[i] ?? "").Trim();
            if (!optionsById.TryGetValue(id, out var opt))
            {
                error = "That choice is not available in this set.";
                return false;
            }

            if (!id.StartsWith(questions[i].QuestionId + "_", StringComparison.OrdinalIgnoreCase))
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

    public static async Task<Dictionary<Guid, decimal>> LoadAffinityBoostsAsync(
        IApplicationDbContext db,
        IReadOnlyList<string> selectedOptionIds,
        CancellationToken cancellationToken)
    {
        if (selectedOptionIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        try
        {
            var optIds = await db.ExperienceGuidedOptions
                .AsNoTracking()
                .Where(o => selectedOptionIds.Contains(o.ExternalKey))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            if (optIds.Count == 0)
                return new Dictionary<Guid, decimal>();

            var affRows = await db.ExperienceGuidedAffinities
                .AsNoTracking()
                .Where(a => optIds.Contains(a.OptionId))
                .Select(a => new { a.MenuItemId, a.Weight })
                .ToListAsync(cancellationToken);

            return affRows
                .GroupBy(x => x.MenuItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Weight));
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return new Dictionary<Guid, decimal>();
        }
    }

    private static DrinkSensoryProfile DeserializeSensory(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DrinkSensoryProfile();
        try
        {
            return JsonSerializer.Deserialize<DrinkSensoryProfile>(json, JsonOpts) ?? new DrinkSensoryProfile();
        }
        catch
        {
            return new DrinkSensoryProfile();
        }
    }

    private sealed record ClientCatalogDto(string SetId, IReadOnlyList<ClientQuestionDto> Questions);

    private sealed record ClientQuestionDto(string QuestionId, string Prompt, IReadOnlyList<ClientOptionDto> Options);

    private sealed record ClientOptionDto(string OptionId, string Label, string? Reflection = null);
}
