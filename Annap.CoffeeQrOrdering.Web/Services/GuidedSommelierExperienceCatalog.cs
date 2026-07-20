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
    public const string SpecialtyCoffeeOptionId = "q0_specialty";
    public const string SpecialtyFlavorQuestionId = "q_sp_profile";
    public const string SpecialtyExperienceQuestionId = "q_sp_adventure";

    public sealed record SpecialtyShortcutExpansion(
        IReadOnlyList<string> OptionIds,
        bool Applied,
        IReadOnlyList<string> InjectedDefaults);

    public static bool HasCompleteSpecialtyDiscovery(IReadOnlyList<string> optionIds)
    {
        if (optionIds is null || optionIds.Count == 0)
            return false;

        var branch = GuidedSommelierCatalog.ResolveBranchKey(optionIds[0]);
        if (!string.Equals(branch, GuidedSommelierCatalog.BranchSpecialty, StringComparison.OrdinalIgnoreCase))
            return false;

        var expected = GuidedSommelierCatalog.QuestionsForBranch(GuidedSommelierCatalog.BranchSpecialty);
        return optionIds.Count == expected.Count
            && optionIds.Any(id => id.StartsWith("q_sp_profile_", StringComparison.OrdinalIgnoreCase))
            && optionIds.Any(id => id.StartsWith("q_sp_adventure_", StringComparison.OrdinalIgnoreCase));
    }

    public static SpecialtyShortcutExpansion ExpandSpecialtyCoffeeShortcut(
        IReadOnlyList<GuidedQuestionSeed> questions,
        IReadOnlyList<string> optionIds)
    {
        _ = questions;
        if (optionIds is null || optionIds.Count == 0)
            return new SpecialtyShortcutExpansion(optionIds ?? Array.Empty<string>(), false, []);

        return new SpecialtyShortcutExpansion(optionIds, false, []);
    }

    public static bool TryResolveSommelierAnswers(
        IReadOnlyList<GuidedQuestionSeed> loadedQuestions,
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error)
    {
        _ = loadedQuestions;
        return GuidedSommelierCatalog.TryResolveBranchPath(optionIds, out resolved, out error);
    }

    public static bool TryResolveSpecialtyCoffeeDiscovery(
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error) =>
        GuidedSommelierCatalog.TryResolveBranchPath(optionIds, out resolved, out error);

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
                return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);

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
                    var branchKey = GuidedSommelierCatalog.ResolveBranchKey(o.ExternalKey);
                    seeds.Add(new GuidedOptionSeed(
                        o.ExternalKey,
                        o.Label,
                        emotional,
                        hints,
                        string.IsNullOrWhiteSpace(o.MoodKey) ? null : o.MoodKey.Trim(),
                        string.IsNullOrWhiteSpace(o.RefinementKey) ? null : o.RefinementKey.Trim(),
                        wm,
                        string.IsNullOrWhiteSpace(o.FlavorTagsJson) ? null : o.FlavorTagsJson.Trim(),
                        BeverageFamilyGrounding.ResolveFamilyKey(o.ExternalKey, o.Label, o.Subline, o.Description)
                            ?? GuidedSommelierCatalog.ResolveBranchKey(o.ExternalKey) switch
                            {
                                GuidedSommelierCatalog.BranchSpecialty => BeverageFamilyGrounding.Coffee,
                                GuidedSommelierCatalog.BranchCoffee => BeverageFamilyGrounding.Coffee,
                                GuidedSommelierCatalog.BranchTea => BeverageFamilyGrounding.Tea,
                                GuidedSommelierCatalog.BranchMatcha => BeverageFamilyGrounding.Matcha,
                                GuidedSommelierCatalog.BranchFruit => BeverageFamilyGrounding.Fruit,
                                GuidedSommelierCatalog.BranchSignature => BeverageFamilyGrounding.Signature,
                                _ => null
                            },
                        guestReflection,
                        branchKey));
                }

                list.Add(new GuidedQuestionSeed(q.ExternalKey, q.Prompt, seeds, q.Description));
            }

            if (list.Count == 0)
                return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);

            return GuidedSommelierCatalog.MergeClientCatalogQuestions(list);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return GuidedSommelierCatalog.MergeClientCatalogQuestions(GuidedSommelierCatalog.AllQuestions);
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

    public static string ToClientJson(IReadOnlyList<GuidedQuestionSeed> questions, string setId) =>
        GuidedSommelierCatalog.ToClientJson(questions, setId);

    public static bool TryResolveOptions(
        IReadOnlyList<GuidedQuestionSeed> questions,
        IReadOnlyList<string> optionIds,
        out IReadOnlyList<GuidedOptionSeed> resolved,
        out string? error)
    {
        // Prefer built-in branch validation so CMS reorder cannot break path length.
        if (GuidedSommelierCatalog.TryResolveBranchPath(optionIds, out resolved, out error))
            return true;

        // Fallback: strict sequential resolve against provided question list.
        resolved = Array.Empty<GuidedOptionSeed>();
        if (optionIds is null || optionIds.Count != questions.Count)
        {
            error ??= "Please complete each step of the tasting.";
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
}
