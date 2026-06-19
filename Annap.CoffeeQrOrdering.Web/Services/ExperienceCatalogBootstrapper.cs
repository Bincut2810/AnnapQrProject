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
}
