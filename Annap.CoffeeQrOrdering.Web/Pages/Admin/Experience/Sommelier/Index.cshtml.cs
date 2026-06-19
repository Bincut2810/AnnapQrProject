using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience.Sommelier;

public sealed class IndexModel(IApplicationDbContext db, IMenuInventoryGate inventoryGate) : PageModel
{
    public IReadOnlyList<GuidedRecommendationRow> PreviewRows { get; private set; } = [];
    public string PreviewAmbient { get; private set; } = "";
    public IReadOnlyList<MenuPickVm> MenuPicks { get; private set; } = [];
    public IReadOnlyList<GuidedSommelierExperienceCatalog.QuestionSetSummary> AvailableSets { get; private set; } = [];
    public string ActiveSetKey { get; private set; } = GuidedSommelierCatalog.QuestionSetId;

    [BindProperty]
    public List<QuestionFormRow> QuestionsForm { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? SetKey { get; set; }

    public string ExperiencePreviewSeedJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ActiveSetKey = string.IsNullOrWhiteSpace(SetKey) ? GuidedSommelierCatalog.QuestionSetId : SetKey.Trim();

        MenuPicks = await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived)
            .OrderBy(m => m.Name)
            .Select(m => new MenuPickVm(m.Id, m.Name, m.TastingNotes, m.MoodProfile))
            .ToListAsync(cancellationToken);

        AvailableSets = await GuidedSommelierExperienceCatalog.GetAvailableSetsAsync(db, cancellationToken);

        var questions = await db.ExperienceGuidedQuestions
            .AsNoTracking()
            .Where(q => q.SetKey == ActiveSetKey)
            .Include(q => q.Options)
            .ThenInclude(o => o.Affinities)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(cancellationToken);

        var nameByMenuId = MenuPicks.ToDictionary(m => m.Id, m => m.Name);

        QuestionsForm = questions.Select(q => new QuestionFormRow
        {
            Id = q.Id,
            ExternalKey = q.ExternalKey,
            Prompt = q.Prompt,
            Description = q.Description ?? "",
            SortOrder = q.SortOrder,
            IsOptional = q.IsOptional,
            IsEnabled = q.IsEnabled,
            Options = q.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new OptionFormRow
                {
                    Id = o.Id,
                    QuestionId = q.Id,
                    ExternalKey = o.ExternalKey,
                    Label = o.Label,
                    Description = o.Description ?? "",
                    Subline = o.Subline ?? "",
                    SortOrder = o.SortOrder,
                    IsEnabled = o.IsEnabled,
                    MoodKey = o.MoodKey ?? "",
                    RefinementKey = o.RefinementKey ?? "",
                    WeightMultiplier = o.WeightMultiplier <= 0 ? 1m : o.WeightMultiplier,
                    AffinityRows = o.Affinities
                        .Select(a => new AffinityRowVm(a.Id, a.MenuItemId, a.Weight,
                            nameByMenuId.TryGetValue(a.MenuItemId, out var nm) ? nm : "Cup"))
                        .ToList()
                }).ToList()
        }).ToList();

        SetExperiencePreviewSeedJson();
        await FillPreviewAsync(cancellationToken);
    }

    private void SetExperiencePreviewSeedJson()
    {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        ExperiencePreviewSeedJson = JsonSerializer.Serialize(new
        {
            context = "guided",
            menu = MenuPicks.Select(m => new { m.Id, m.Name, m.TastingNotes, m.MoodProfile }).ToList(),
            questions = QuestionsForm.OrderBy(q => q.SortOrder).Select(q => new
            {
                q.Id,
                q.ExternalKey,
                q.Prompt,
                q.Description,
                q.SortOrder,
                q.IsEnabled,
                q.IsOptional,
                options = q.Options.OrderBy(o => o.SortOrder).Select(o => new
                {
                    o.Id,
                    o.ExternalKey,
                    o.Label,
                    o.Subline,
                    o.Description,
                    o.SortOrder,
                    o.IsEnabled,
                    o.MoodKey,
                    o.RefinementKey,
                    o.WeightMultiplier,
                    affinities = o.AffinityRows.Select(a => new { a.MenuItemId, a.Weight }).ToList()
                }).ToList()
            }).ToList()
        }, opts);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var setKey = string.IsNullOrWhiteSpace(SetKey) ? GuidedSommelierCatalog.QuestionSetId : SetKey.Trim();

        foreach (var qf in QuestionsForm)
        {
            var q = await db.ExperienceGuidedQuestions
                .FirstOrDefaultAsync(x => x.Id == qf.Id && x.SetKey == setKey, cancellationToken);
            if (q is null)
                continue;
            q.Prompt = (qf.Prompt ?? "").Trim();
            q.Description = string.IsNullOrWhiteSpace(qf.Description) ? null : qf.Description.Trim();
            q.SortOrder = qf.SortOrder;
            q.IsOptional = qf.IsOptional;
            q.IsEnabled = qf.IsEnabled;

            foreach (var of in qf.Options)
            {
                var o = await db.ExperienceGuidedOptions.FirstOrDefaultAsync(
                    x => x.Id == of.Id && x.QuestionId == q.Id, cancellationToken);
                if (o is null)
                    continue;
                o.Label = (of.Label ?? "").Trim();
                o.Description = string.IsNullOrWhiteSpace(of.Description) ? null : of.Description.Trim();
                o.Subline = string.IsNullOrWhiteSpace(of.Subline) ? null : of.Subline.Trim();
                o.SortOrder = of.SortOrder;
                o.IsEnabled = of.IsEnabled;
                o.MoodKey = string.IsNullOrWhiteSpace(of.MoodKey) ? null : of.MoodKey.Trim();
                o.RefinementKey = string.IsNullOrWhiteSpace(of.RefinementKey) ? null : of.RefinementKey.Trim();
                o.WeightMultiplier = of.WeightMultiplier <= 0 ? 1m : Math.Min(of.WeightMultiplier, 8m);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { set = setKey });
    }

    public async Task<IActionResult> OnPostAddQuestionAsync(string? setKey, CancellationToken cancellationToken)
    {
        var sk = string.IsNullOrWhiteSpace(setKey) ? GuidedSommelierCatalog.QuestionSetId : setKey.Trim();
        var maxSort = await db.ExperienceGuidedQuestions
            .Where(q => q.SetKey == sk)
            .MaxAsync(q => (int?)q.SortOrder, cancellationToken) ?? -1;
        var idx = 1;
        string ext;
        do
        {
            ext = $"q-{idx++}";
        } while (await db.ExperienceGuidedQuestions.AnyAsync(q => q.SetKey == sk && q.ExternalKey == ext, cancellationToken));

        db.ExperienceGuidedQuestions.Add(new ExperienceGuidedQuestion
        {
            ExternalKey = ext,
            SetKey = sk,
            Prompt = "New question",
            SortOrder = maxSort + 1,
            IsOptional = false,
            IsEnabled = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { set = sk });
    }

    public async Task<IActionResult> OnPostAddOptionAsync(Guid questionId, string? label, string? setKey, CancellationToken cancellationToken)
    {
        var sk = string.IsNullOrWhiteSpace(setKey) ? GuidedSommelierCatalog.QuestionSetId : setKey.Trim();
        var q = await db.ExperienceGuidedQuestions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == questionId && x.SetKey == sk, cancellationToken);
        if (q is null)
            return RedirectToPage(new { set = sk });

        var baseLabel = string.IsNullOrWhiteSpace(label) ? "New answer" : label.Trim();
        var baseKey = MakeOptionExternalKey(q.ExternalKey, baseLabel);
        var key = baseKey;
        var n = 1;
        while (await db.ExperienceGuidedOptions.AnyAsync(o => o.QuestionId == questionId && o.ExternalKey == key, cancellationToken))
            key = $"{baseKey}-{++n}";

        var maxSort = await db.ExperienceGuidedOptions.Where(o => o.QuestionId == questionId)
            .MaxAsync(o => (int?)o.SortOrder, cancellationToken) ?? -1;

        db.ExperienceGuidedOptions.Add(new ExperienceGuidedOption
        {
            QuestionId = questionId,
            ExternalKey = key,
            Label = baseLabel,
            Description = null,
            Subline = null,
            SortOrder = maxSort + 1,
            IsEnabled = true,
            MoodKey = null,
            RefinementKey = null,
            FlavorTagsJson = null,
            WeightMultiplier = 1m,
            SensoryProfileJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { set = sk });
    }

    public async Task<IActionResult> OnPostAddAffinityAsync(
        Guid optionId, Guid menuItemId, decimal weight, string? setKey, CancellationToken cancellationToken)
    {
        var sk = string.IsNullOrWhiteSpace(setKey) ? GuidedSommelierCatalog.QuestionSetId : setKey.Trim();
        if (weight < 0)
            weight = 0;
        if (!await db.ExperienceGuidedAffinities.AnyAsync(
                a => a.OptionId == optionId && a.MenuItemId == menuItemId, cancellationToken))
        {
            db.ExperienceGuidedAffinities.Add(new ExperienceGuidedAffinity
            {
                OptionId = optionId,
                MenuItemId = menuItemId,
                Weight = weight,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToPage(new { set = sk });
    }

    public async Task<IActionResult> OnPostRemoveAffinityAsync(Guid affinityId, string? setKey, CancellationToken cancellationToken)
    {
        var sk = string.IsNullOrWhiteSpace(setKey) ? GuidedSommelierCatalog.QuestionSetId : setKey.Trim();
        var row = await db.ExperienceGuidedAffinities.FirstOrDefaultAsync(a => a.Id == affinityId, cancellationToken);
        if (row is not null)
        {
            db.ExperienceGuidedAffinities.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToPage(new { set = sk });
    }

    /// <summary>Creates a new campaign questionnaire by copying the active set with a new key.</summary>
    public async Task<IActionResult> OnPostCreateCampaignAsync(string? campaignName, CancellationToken cancellationToken)
    {
        var sourcek = GuidedSommelierCatalog.QuestionSetId;
        var slug = Regex.Replace((campaignName ?? "campaign").ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "campaign";
        var newKey = $"campaign-{slug}";

        // Ensure uniqueness.
        var n = 1;
        while (await db.ExperienceGuidedQuestions.AnyAsync(q => q.SetKey == newKey, cancellationToken))
            newKey = $"campaign-{slug}-{++n}";

        var now = DateTimeOffset.UtcNow;
        var sourceQuestions = await db.ExperienceGuidedQuestions
            .AsNoTracking()
            .Where(q => q.SetKey == sourcek)
            .Include(q => q.Options)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var sq in sourceQuestions)
        {
            var newQ = new ExperienceGuidedQuestion
            {
                ExternalKey = sq.ExternalKey,
                SetKey = newKey,
                Prompt = sq.Prompt,
                Description = sq.Description,
                SortOrder = sq.SortOrder,
                IsOptional = sq.IsOptional,
                IsEnabled = false,  // new campaigns start disabled until ready
                CreatedAtUtc = now
            };
            db.ExperienceGuidedQuestions.Add(newQ);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var so in sq.Options.OrderBy(o => o.SortOrder))
            {
                db.ExperienceGuidedOptions.Add(new ExperienceGuidedOption
                {
                    QuestionId = newQ.Id,
                    ExternalKey = so.ExternalKey,
                    Label = so.Label,
                    Description = so.Description,
                    Subline = so.Subline,
                    SortOrder = so.SortOrder,
                    IsEnabled = so.IsEnabled,
                    MoodKey = so.MoodKey,
                    RefinementKey = so.RefinementKey,
                    FlavorTagsJson = so.FlavorTagsJson,
                    WeightMultiplier = so.WeightMultiplier,
                    SensoryProfileJson = so.SensoryProfileJson,
                    CreatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { set = newKey });
    }

    private async Task FillPreviewAsync(CancellationToken cancellationToken)
    {
        var questions = await GuidedSommelierExperienceCatalog.LoadQuestionSeedsAsync(db, cancellationToken, ActiveSetKey);
        if (questions.Count == 0)
            return;

        var defaultIds = new List<string>(questions.Count);
        foreach (var q in questions)
        {
            var first = q.Options.FirstOrDefault();
            if (first is null)
                return;
            defaultIds.Add(first.OptionId);
        }

        if (!GuidedSommelierExperienceCatalog.TryResolveOptions(questions, defaultIds, out var resolved, out _))
            return;

        var guestHints = GuidedSommelierCatalog.MergeGuestHints(resolved);
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken);
        var affinity = await GuidedSommelierExperienceCatalog.LoadAffinityBoostsAsync(db, defaultIds, cancellationToken);

        var raw = await db.MenuItems
            .AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
            .Select(m => new
            {
                m.Id, m.Name, m.Price, m.TastingNotes, m.ShortStory, m.ImageUrl, m.MoodProfile,
                CatName = m.Category.Name, m.SensoryProfile, m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel
            })
            .ToListAsync(cancellationToken);

        var rows = raw.Select(m => new MenuItemScoringRow(
                m.Id, m.Name, m.Price, m.TastingNotes, m.ShortStory,
                MenuMediaResolver.ResolveCardImageUrl(null, null, m.ImageUrl, null, m.Name, m.CatName),
                m.MoodProfile,
                m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel),
                m.CatName))
            .ToList();

        PreviewAmbient = GuidedSommelierRecommendationEngine.ComposeAmbientLine(resolved);
        PreviewRows = GuidedSommelierRecommendationEngine.Rank(guestHints, resolved, rows, 5, affinity);
    }

    private static string MakeOptionExternalKey(string questionExternalKey, string label)
    {
        var slug = Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "choice";
        return $"{questionExternalKey}_{slug}";
    }

    public sealed record MenuPickVm(Guid Id, string Name, string? TastingNotes, string? MoodProfile);
    public sealed record AffinityRowVm(Guid Id, Guid MenuItemId, decimal Weight, string MenuItemName);

    public sealed class QuestionFormRow
    {
        public Guid Id { get; set; }
        public string ExternalKey { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string Description { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsOptional { get; set; }
        public bool IsEnabled { get; set; }
        public List<OptionFormRow> Options { get; set; } = [];
    }

    public sealed class OptionFormRow
    {
        public Guid Id { get; set; }
        public Guid QuestionId { get; set; }
        public string ExternalKey { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public string Subline { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsEnabled { get; set; }
        public string MoodKey { get; set; } = "";
        public string RefinementKey { get; set; } = "";
        public decimal WeightMultiplier { get; set; } = 1m;
        public List<AffinityRowVm> AffinityRows { get; set; } = [];
    }
}
