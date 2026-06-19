using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Serializes / restores the guest experience workbench (signatures, guided, discovery).</summary>
public static class ExperienceWorkbenchSerializer
{
    private const int Version = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static async Task<string> SerializeAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var signatures = await db.ExperienceSignatureSlots.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new SignatureDto(
                s.Id,
                s.MenuItemId,
                s.SortOrder,
                s.IsActive,
                s.IsSpotlight,
                s.SeasonalSpotlightEnabled,
                s.EditorialKicker,
                s.EditorialBody))
            .ToListAsync(cancellationToken);

        var questions = await db.ExperienceGuidedQuestions.AsNoTracking()
            .Include(q => q.Options)
            .ThenInclude(o => o.Affinities)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(cancellationToken);

        var qDtos = questions.Select(q => new QuestionDto(
            q.Id,
            q.ExternalKey,
            q.Prompt,
            q.Description,
            q.SortOrder,
            q.IsOptional,
            q.IsEnabled,
            q.Options.OrderBy(o => o.SortOrder).Select(o => new OptionDto(
                o.Id,
                o.QuestionId,
                o.ExternalKey,
                o.Label,
                o.Description,
                o.Subline,
                o.SortOrder,
                o.IsEnabled,
                o.MoodKey,
                o.RefinementKey,
                o.FlavorTagsJson,
                o.WeightMultiplier,
                o.SensoryProfileJson,
                o.Affinities.Select(a => new AffinityDto(a.MenuItemId, a.Weight)).ToList()
            )).ToList()
        )).ToList();

        var sid = ExperienceDiscoverySettingsConfiguration.SingletonId;
        var disc = await db.ExperienceDiscoverySettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);

        var discDto = disc is null
            ? new DiscoverySettingsDto(false, true, true, true, 3, null, null, null, "{}", null)
            : new DiscoverySettingsDto(
                disc.SeasonalOnlyPool,
                disc.AllowSeasonalCups,
                disc.PreferSignaturesFirst,
                disc.AllowRerolls,
                disc.AdventureTone,
                disc.CourierMoodCopy,
                disc.FatigueCopyEvenLeg,
                disc.FatigueCopyOddLeg,
                disc.RerollPacingJson,
                disc.RevealCopyNotes);

        var menuPatches = await db.MenuItems.AsNoTracking()
            .Where(m => !m.IsArchived)
            .Select(m => new MenuDiscoveryDto(
                m.Id,
                m.DiscoveryWeight,
                m.IsHiddenDiscovery,
                m.IsDiscoveryEligible,
                m.DiscoveryStory))
            .ToListAsync(cancellationToken);

        var root = new SnapshotRoot(Version, signatures, qDtos, discDto, menuPatches);
        return JsonSerializer.Serialize(root, JsonOpts);
    }

    public static async Task ApplyAsync(IApplicationDbContext db, string payloadJson, CancellationToken cancellationToken)
    {
        var dbc = (DbContext)db;
        var root = JsonSerializer.Deserialize<SnapshotRoot>(payloadJson, JsonOpts)
                   ?? throw new InvalidOperationException("Invalid snapshot JSON.");
        if (root.V != Version)
            throw new InvalidOperationException($"Unsupported snapshot version {root.V}.");

        await using var tx = await dbc.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var sigTracked = await db.ExperienceSignatureSlots.ToListAsync(cancellationToken);
        db.ExperienceSignatureSlots.RemoveRange(sigTracked);

        foreach (var s in root.Signatures.OrderBy(x => x.SortOrder))
        {
            db.ExperienceSignatureSlots.Add(new ExperienceSignatureSlot
            {
                Id = s.Id,
                MenuItemId = s.MenuItemId,
                SortOrder = s.SortOrder,
                IsActive = s.IsActive,
                IsSpotlight = s.IsSpotlight,
                SeasonalSpotlightEnabled = s.SeasonalSpotlightEnabled,
                EditorialKicker = s.EditorialKicker,
                EditorialBody = s.EditorialBody,
                CreatedAtUtc = now
            });
        }

        db.ExperienceGuidedAffinities.RemoveRange(await db.ExperienceGuidedAffinities.ToListAsync(cancellationToken));
        db.ExperienceGuidedOptions.RemoveRange(await db.ExperienceGuidedOptions.ToListAsync(cancellationToken));
        db.ExperienceGuidedQuestions.RemoveRange(await db.ExperienceGuidedQuestions.ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);

        foreach (var q in root.Questions.OrderBy(x => x.SortOrder))
        {
            db.ExperienceGuidedQuestions.Add(new ExperienceGuidedQuestion
            {
                Id = q.Id,
                ExternalKey = q.ExternalKey,
                Prompt = q.Prompt,
                Description = q.Description,
                SortOrder = q.SortOrder,
                IsOptional = q.IsOptional,
                IsEnabled = q.IsEnabled,
                CreatedAtUtc = now
            });
        }

        foreach (var q in root.Questions)
        {
            foreach (var o in q.Options.OrderBy(x => x.SortOrder))
            {
                db.ExperienceGuidedOptions.Add(new ExperienceGuidedOption
                {
                    Id = o.Id,
                    QuestionId = o.QuestionId,
                    ExternalKey = o.ExternalKey,
                    Label = o.Label,
                    Description = o.Description,
                    Subline = o.Subline,
                    SortOrder = o.SortOrder,
                    IsEnabled = o.IsEnabled,
                    MoodKey = o.MoodKey,
                    RefinementKey = o.RefinementKey,
                    FlavorTagsJson = o.FlavorTagsJson,
                    WeightMultiplier = o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier,
                    SensoryProfileJson = string.IsNullOrWhiteSpace(o.SensoryProfileJson) ? "{}" : o.SensoryProfileJson,
                    CreatedAtUtc = now
                });
                foreach (var a in o.Affinities)
                {
                    db.ExperienceGuidedAffinities.Add(new ExperienceGuidedAffinity
                    {
                        OptionId = o.Id,
                        MenuItemId = a.MenuItemId,
                        Weight = a.Weight < 0 ? 0 : a.Weight,
                        CreatedAtUtc = now
                    });
                }
            }
        }

        var sid = ExperienceDiscoverySettingsConfiguration.SingletonId;
        var d = root.DiscoverySettings;
        var tone = d.AdventureTone is >= 1 and <= 5 ? d.AdventureTone : 3;
        var existing = await db.ExperienceDiscoverySettings.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
        if (existing is null)
        {
            db.ExperienceDiscoverySettings.Add(new ExperienceDiscoverySettings
            {
                Id = sid,
                SeasonalOnlyPool = d.SeasonalOnlyPool,
                AllowSeasonalCups = d.AllowSeasonalCups,
                PreferSignaturesFirst = d.PreferSignaturesFirst,
                AllowRerolls = d.AllowRerolls,
                AdventureTone = tone,
                CourierMoodCopy = d.CourierMoodCopy,
                FatigueCopyEvenLeg = d.FatigueCopyEvenLeg,
                FatigueCopyOddLeg = d.FatigueCopyOddLeg,
                RerollPacingJson = string.IsNullOrWhiteSpace(d.RerollPacingJson) ? "{}" : d.RerollPacingJson,
                RevealCopyNotes = d.RevealCopyNotes,
                CreatedAtUtc = now
            });
        }
        else
        {
            existing.SeasonalOnlyPool = d.SeasonalOnlyPool;
            existing.AllowSeasonalCups = d.AllowSeasonalCups;
            existing.PreferSignaturesFirst = d.PreferSignaturesFirst;
            existing.AllowRerolls = d.AllowRerolls;
            existing.AdventureTone = tone;
            existing.CourierMoodCopy = d.CourierMoodCopy;
            existing.FatigueCopyEvenLeg = d.FatigueCopyEvenLeg;
            existing.FatigueCopyOddLeg = d.FatigueCopyOddLeg;
            existing.RerollPacingJson = string.IsNullOrWhiteSpace(d.RerollPacingJson) ? "{}" : d.RerollPacingJson;
            existing.RevealCopyNotes = d.RevealCopyNotes;
        }

        foreach (var m in root.MenuDiscovery)
        {
            var row = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == m.MenuItemId, cancellationToken);
            if (row is null)
                continue;
            row.DiscoveryWeight = m.DiscoveryWeight < 0 ? 0 : m.DiscoveryWeight;
            row.IsHiddenDiscovery = m.IsHiddenDiscovery;
            row.IsDiscoveryEligible = m.IsDiscoveryEligible;
            row.DiscoveryStory = m.DiscoveryStory;
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public sealed record SnapshotRoot(
        int V,
        IReadOnlyList<SignatureDto> Signatures,
        IReadOnlyList<QuestionDto> Questions,
        DiscoverySettingsDto DiscoverySettings,
        IReadOnlyList<MenuDiscoveryDto> MenuDiscovery);

    public sealed record SignatureDto(
        Guid Id,
        Guid MenuItemId,
        int SortOrder,
        bool IsActive,
        bool IsSpotlight,
        bool SeasonalSpotlightEnabled,
        string? EditorialKicker,
        string? EditorialBody);

    public sealed record QuestionDto(
        Guid Id,
        string ExternalKey,
        string Prompt,
        string? Description,
        int SortOrder,
        bool IsOptional,
        bool IsEnabled,
        IReadOnlyList<OptionDto> Options);

    public sealed record OptionDto(
        Guid Id,
        Guid QuestionId,
        string ExternalKey,
        string Label,
        string? Description,
        string? Subline,
        int SortOrder,
        bool IsEnabled,
        string? MoodKey,
        string? RefinementKey,
        string? FlavorTagsJson,
        decimal WeightMultiplier,
        string SensoryProfileJson,
        IReadOnlyList<AffinityDto> Affinities);

    public sealed record AffinityDto(Guid MenuItemId, decimal Weight);

    public sealed record DiscoverySettingsDto(
        bool SeasonalOnlyPool,
        bool AllowSeasonalCups,
        bool PreferSignaturesFirst,
        bool AllowRerolls,
        int AdventureTone,
        string? CourierMoodCopy,
        string? FatigueCopyEvenLeg,
        string? FatigueCopyOddLeg,
        string? RerollPacingJson,
        string? RevealCopyNotes);

    public sealed record MenuDiscoveryDto(
        Guid MenuItemId,
        decimal DiscoveryWeight,
        bool IsHiddenDiscovery,
        bool IsDiscoveryEligible,
        string? DiscoveryStory);
}
