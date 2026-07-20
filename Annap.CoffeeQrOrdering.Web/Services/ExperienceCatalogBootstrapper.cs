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
        foreach (var q in GuidedSommelierCatalog.AllQuestions)
        {
            db.ExperienceGuidedQuestions.Add(new ExperienceGuidedQuestion
            {
                ExternalKey = q.QuestionId,
                SetKey = setKey,
                Prompt = q.Prompt,
                Description = string.IsNullOrWhiteSpace(q.Description) ? null : q.Description.Trim(),
                SortOrder = sort++,
                IsOptional = GuidedSommelierCatalog.IsBranchQuestionId(q.QuestionId),
                IsEnabled = true,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var qMap = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == setKey)
            .ToDictionaryAsync(x => x.ExternalKey, x => x.Id, cancellationToken);

        foreach (var q in GuidedSommelierCatalog.AllQuestions)
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
                    FlavorTagsJson = string.IsNullOrWhiteSpace(o.FlavorTagsJson) ? null : o.FlavorTagsJson.Trim(),
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
        // atelier_v5 seeds specialty questions with AllQuestions in EnsureGuidedAndDiscoveryAsync.
        var setKey = GuidedSommelierCatalog.QuestionSetId;
        if (await db.ExperienceGuidedQuestions.AnyAsync(q => q.SetKey == setKey && q.ExternalKey == "q_sp_profile", cancellationToken))
            return;

        await EnsureGuidedAndDiscoveryAsync(db, cancellationToken);
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
        var defaults = GuidedSommelierCatalog.AllQuestions.ToList();

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
            if (!string.Equals(dbQ.Prompt, q.Prompt, StringComparison.Ordinal))
                dbQ.Prompt = q.Prompt;
            if (!string.Equals(dbQ.Description ?? "", q.Description ?? "", StringComparison.Ordinal))
                dbQ.Description = string.IsNullOrWhiteSpace(q.Description) ? null : q.Description.Trim();
            dbQ.UpdatedAtUtc = now;
        }

        var dbOptions = await db.ExperienceGuidedOptions
            .Where(o => qMap.Values.Contains(o.QuestionId))
            .ToListAsync(cancellationToken);

        foreach (var q in defaults)
        {
            if (!qMap.TryGetValue(q.QuestionId, out var qid))
                continue;
            foreach (var o in q.Options)
            {
                var row = dbOptions.FirstOrDefault(x =>
                    x.QuestionId == qid
                    && string.Equals(x.ExternalKey, o.OptionId, StringComparison.OrdinalIgnoreCase));
                if (row is null)
                    continue;
                row.Label = o.Label;
                row.Subline = o.EmotionalFragment;
                row.MoodKey = string.IsNullOrWhiteSpace(o.MoodKey) ? null : o.MoodKey.Trim();
                row.RefinementKey = string.IsNullOrWhiteSpace(o.RefinementKey) ? null : o.RefinementKey.Trim();
                row.FlavorTagsJson = string.IsNullOrWhiteSpace(o.FlavorTagsJson) ? null : o.FlavorTagsJson.Trim();
                row.SensoryProfileJson = JsonSerializer.Serialize(o.SensoryHints, JsonOpts);
                row.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
