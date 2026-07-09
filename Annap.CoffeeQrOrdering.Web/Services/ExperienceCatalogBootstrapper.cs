using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Seeds experience CMS tables from the built-in catalog when the target SetKey does not yet exist.
/// Safe to call on every startup — idempotent per SetKey.
/// </summary>
public static class ExperienceCatalogBootstrapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task EnsureGuidedAndDiscoveryAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var setKey = GuidedSommelierCatalog.QuestionSetId;

        // Skip if this version is already seeded.
        if (await db.ExperienceGuidedQuestions.AnyAsync(q => q.SetKey == setKey, cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;
        var sort = 0;
        foreach (var q in GuidedSommelierCatalog.Questions)
        {
            db.ExperienceGuidedQuestions.Add(new ExperienceGuidedQuestion
            {
                ExternalKey = q.QuestionId,
                SetKey = setKey,
                Prompt = q.Prompt,
                Description = string.IsNullOrWhiteSpace(q.Description) ? null : q.Description.Trim(),
                SortOrder = sort++,
                IsOptional = false,
                IsEnabled = true,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var qMap = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == setKey)
            .ToDictionaryAsync(x => x.ExternalKey, x => x.Id, cancellationToken);

        foreach (var q in GuidedSommelierCatalog.Questions)
        {
            var qid = qMap[q.QuestionId];
            var oSort = 0;
            foreach (var o in q.Options)
            {
                var sensoryJson = JsonSerializer.Serialize(o.SensoryHints, JsonOpts);
                db.ExperienceGuidedOptions.Add(new ExperienceGuidedOption
                {
                    QuestionId = qid,
                    ExternalKey = o.OptionId,
                    Label = o.Label,
                    Description = null,
                    Subline = o.EmotionalFragment,
                    SortOrder = oSort++,
                    IsEnabled = true,
                    MoodKey = string.IsNullOrWhiteSpace(o.MoodKey) ? null : o.MoodKey.Trim(),
                    RefinementKey = string.IsNullOrWhiteSpace(o.RefinementKey) ? null : o.RefinementKey.Trim(),
                    FlavorTagsJson = null,
                    WeightMultiplier = o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier,
                    SensoryProfileJson = string.IsNullOrWhiteSpace(sensoryJson) ? "{}" : sensoryJson,
                    CreatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureSpecialtyCoffeeDiscoveryQuestionsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var setKey = GuidedSommelierCatalog.QuestionSetId;
        var exists = await db.ExperienceGuidedQuestions
            .AnyAsync(q => q.SetKey == setKey && q.ExternalKey == "q_sc_flavor", cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        var maxSort = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == setKey)
            .Select(q => (int?)q.SortOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var sort = (maxSort ?? 3) + 1;
        foreach (var q in GuidedSommelierCatalog.SpecialtyCoffeeDiscoveryQuestions)
        {
            db.ExperienceGuidedQuestions.Add(new ExperienceGuidedQuestion
            {
                ExternalKey = q.QuestionId,
                SetKey = setKey,
                Prompt = q.Prompt,
                Description = string.IsNullOrWhiteSpace(q.Description) ? null : q.Description.Trim(),
                SortOrder = sort++,
                IsOptional = true,
                IsEnabled = true,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var qMap = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == setKey && (q.ExternalKey == "q_sc_flavor" || q.ExternalKey == "q_sc_experience"))
            .ToDictionaryAsync(x => x.ExternalKey, x => x.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var q in GuidedSommelierCatalog.SpecialtyCoffeeDiscoveryQuestions)
        {
            if (!qMap.TryGetValue(q.QuestionId, out var qid))
                continue;
            var oSort = 0;
            foreach (var o in q.Options)
            {
                var sensoryJson = JsonSerializer.Serialize(o.SensoryHints, JsonOpts);
                db.ExperienceGuidedOptions.Add(new ExperienceGuidedOption
                {
                    QuestionId = qid,
                    ExternalKey = o.OptionId,
                    Label = o.Label,
                    Description = null,
                    Subline = o.EmotionalFragment,
                    SortOrder = oSort++,
                    IsEnabled = true,
                    MoodKey = string.IsNullOrWhiteSpace(o.MoodKey) ? null : o.MoodKey.Trim(),
                    RefinementKey = string.IsNullOrWhiteSpace(o.RefinementKey) ? null : o.RefinementKey.Trim(),
                    FlavorTagsJson = string.IsNullOrWhiteSpace(o.FlavorTagsJson) ? null : o.FlavorTagsJson.Trim(),
                    WeightMultiplier = o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier,
                    SensoryProfileJson = string.IsNullOrWhiteSpace(sensoryJson) ? "{}" : sensoryJson,
                    CreatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Development-only: refresh guided question prompts and option labels from built-in defaults.
    /// Production copy remains admin/CMS controlled.
    /// </summary>
    public static async Task SyncGuidedDisplayCopyInDevelopmentAsync(
        IApplicationDbContext db,
        bool isDevelopment,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
            return;

        var setKey = GuidedSommelierCatalog.QuestionSetId;
        var defaults = GuidedSommelierCatalog.Questions
            .Concat(GuidedSommelierCatalog.SpecialtyCoffeeDiscoveryQuestions)
            .ToList();

        var dbQuestions = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == setKey)
            .ToListAsync(cancellationToken);

        if (dbQuestions.Count == 0)
            return;

        var qMap = dbQuestions.ToDictionary(q => q.ExternalKey, q => q.Id, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var q in defaults)
        {
            if (!qMap.TryGetValue(q.QuestionId, out var qid))
                continue;

            var dbQ = dbQuestions.First(x => x.Id == qid);
            dbQ.Prompt = q.Prompt;
            dbQ.UpdatedAtUtc = now;

            var dbOpts = await db.ExperienceGuidedOptions
                .Where(o => o.QuestionId == qid)
                .ToListAsync(cancellationToken);

            foreach (var o in q.Options)
            {
                var dbO = dbOpts.FirstOrDefault(x =>
                    string.Equals(x.ExternalKey, o.OptionId, StringComparison.OrdinalIgnoreCase));
                if (dbO is null)
                    continue;
                dbO.Label = o.Label;
                dbO.Subline = o.EmotionalFragment;
                dbO.IsEnabled = true;
                dbO.UpdatedAtUtc = now;
            }

            var defaultOptionIds = q.Options.Select(o => o.OptionId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var dbO in dbOpts)
            {
                if (defaultOptionIds.Contains(dbO.ExternalKey))
                    continue;
                dbO.IsEnabled = false;
                dbO.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
