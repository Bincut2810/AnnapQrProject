using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience;

public sealed class IndexModel(IApplicationDbContext db, IWebHostEnvironment env) : PageModel, IHomepageCompositionHost
{
    [BindProperty]
    public HomepageExperienceSettingsAdmin.FormModel HomepageForm { get; set; } = new();

    public string? HomepageCompositionStatus { get; set; }

    public bool HomepageCompositionShowDedicatedLink => false;

    public string HomepageCompositionFormId => "exp-homepage-form-hub";

    public string HomepageCompositionPostHandler => "SaveHomepageComposition";

    public int HomepageModesVisible => HomepageExperienceSettingsAdmin.VisibleCount(HomepageForm);

    public int ActiveSignatureSlots { get; private set; }

    public int GuidedQuestionsEnabled { get; private set; }

    public int DiscoveryEligibleDrinks { get; private set; }

    public DateTimeOffset? LastExperienceEditUtc { get; private set; }

    public int ExperienceCoveragePercent { get; private set; }

    public DateTimeOffset? LastPublishedUtc { get; private set; }

    public string? LastPublishedRelative { get; private set; }

    public IReadOnlyList<PublishedSnapshotRow> PublishedSnapshots { get; private set; } = [];

    public string ExperiencePreviewSeedJson { get; private set; } = "{}";

    public string? PublishMessage { get; set; }

    public string? PublishError { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            ActiveSignatureSlots = await db.ExperienceSignatureSlots.AsNoTracking()
                .CountAsync(s => s.IsActive, cancellationToken);
            GuidedQuestionsEnabled = await db.ExperienceGuidedQuestions.AsNoTracking()
                .CountAsync(q => q.IsEnabled, cancellationToken);
            DiscoveryEligibleDrinks = await db.MenuItems.AsNoTracking()
                .CountAsync(m =>
                        m.IsAvailable && !m.IsArchived &&
                        m.IsDiscoveryEligible &&
                        !m.IsHiddenDiscovery && m.DiscoveryWeight > 0 &&
                        (m.IsSignature || m.IsFeatured || m.IsSeasonalHighlight),
                    cancellationToken);
        }
        catch
        {
            ActiveSignatureSlots = 0;
            GuidedQuestionsEnabled = 0;
            DiscoveryEligibleDrinks = 0;
        }

        var stamps = new List<DateTimeOffset>();
        try
        {
            stamps.AddRange(await db.ExperienceSignatureSlots.AsNoTracking()
                .Select(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
                .ToListAsync(cancellationToken));
            stamps.AddRange(await db.ExperienceGuidedQuestions.AsNoTracking()
                .Select(q => q.UpdatedAtUtc ?? q.CreatedAtUtc)
                .ToListAsync(cancellationToken));
            stamps.AddRange(await db.ExperienceGuidedOptions.AsNoTracking()
                .Select(o => o.UpdatedAtUtc ?? o.CreatedAtUtc)
                .ToListAsync(cancellationToken));
        }
        catch
        {
            // ignore
        }

        if (stamps.Count > 0)
            LastExperienceEditUtc = stamps.Max();

        try
        {
            var menuStamps = await db.MenuItems.AsNoTracking()
                .Where(m => m.DiscoveryWeight != 1m || m.IsHiddenDiscovery || m.IsDiscoveryEligible == false ||
                            !string.IsNullOrEmpty(m.DiscoveryStory))
                .Select(m => m.UpdatedAtUtc ?? m.CreatedAtUtc)
                .ToListAsync(cancellationToken);
            if (menuStamps.Count > 0)
            {
                var mx = menuStamps.Max();
                LastExperienceEditUtc = LastExperienceEditUtc is null
                    ? mx
                    : (LastExperienceEditUtc.Value > mx ? LastExperienceEditUtc : mx);
            }
        }
        catch
        {
            // ignore
        }

        var sigIdeal = 4d;
        var qIdeal = 3d;
        var dIdeal = 6d;
        var sigPart = Math.Min(1d, ActiveSignatureSlots / sigIdeal);
        var qPart = Math.Min(1d, GuidedQuestionsEnabled / qIdeal);
        var dPart = Math.Min(1d, DiscoveryEligibleDrinks / dIdeal);
        ExperienceCoveragePercent = (int)Math.Round(100 * (sigPart + qPart + dPart) / 3d);

        try
        {
            var lastPub = await db.ExperienceSnapshots.AsNoTracking()
                .Where(s => s.Kind == 1)
                .OrderByDescending(s => s.CreatedAtUtc)
                .Select(s => (DateTimeOffset?)s.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            LastPublishedUtc = lastPub;
            LastPublishedRelative = lastPub is { } u ? Humanize(DateTimeOffset.UtcNow - u) : null;

            PublishedSnapshots = await db.ExperienceSnapshots.AsNoTracking()
                .Where(s => s.Kind == 1)
                .OrderByDescending(s => s.CreatedAtUtc)
                .Take(10)
                .Select(s => new PublishedSnapshotRow(s.Id, s.CreatedAtUtc, s.HouseNote))
                .ToListAsync(cancellationToken);
        }
        catch
        {
            LastPublishedUtc = null;
            PublishedSnapshots = [];
        }

        ExperiencePreviewSeedJson = await BuildHubPreviewSeedAsync(cancellationToken);

        try
        {
            HomepageForm = await HomepageExperienceSettingsAdmin.LoadAsync(db, cancellationToken);
        }
        catch
        {
            HomepageForm = new HomepageExperienceSettingsAdmin.FormModel();
        }
    }

    public async Task<IActionResult> OnPostSaveHomepageCompositionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await HomepageExperienceSettingsAdmin.SaveAsync(db, HomepageForm, cancellationToken);
            HomepageCompositionStatus = "Lobby composition saved — guest phones will reflect this on refresh.";
        }
        catch (Exception ex)
        {
            PublishError = ex.Message;
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveDraftAsync(string? houseNote, CancellationToken cancellationToken)
    {
        try
        {
            var json = await ExperienceWorkbenchSerializer.SerializeAsync(db, cancellationToken);
            db.ExperienceSnapshots.Add(new ExperienceSnapshot
            {
                Kind = 0,
                PayloadJson = json,
                HouseNote = string.IsNullOrWhiteSpace(houseNote) ? null : houseNote.Trim()
            });
            await db.SaveChangesAsync(cancellationToken);
            PublishMessage = "Draft saved for the house files.";
        }
        catch (Exception ex)
        {
            PublishError = ex.Message;
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPublishExperienceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await ExperienceWorkbenchSerializer.SerializeAsync(db, cancellationToken);
            var snap = new ExperienceSnapshot
            {
                Kind = 1,
                PayloadJson = json,
                HouseNote = null
            };
            db.ExperienceSnapshots.Add(snap);
            await db.SaveChangesAsync(cancellationToken);
            db.ExperiencePublishRecords.Add(new ExperiencePublishRecord { SnapshotId = snap.Id });
            await db.SaveChangesAsync(cancellationToken);
            PublishMessage = "Guest journey published — the floor now matches this snapshot.";
        }
        catch (Exception ex)
        {
            PublishError = ex.Message;
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreExperienceAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        try
        {
            var snap = await db.ExperienceSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == snapshotId && s.Kind == 1, cancellationToken);
            if (snap is null)
            {
                PublishError = "That published moment could not be found.";
            }
            else
            {
                await ExperienceWorkbenchSerializer.ApplyAsync(db, snap.PayloadJson, cancellationToken);
                PublishMessage = "House ritual restored from your archive.";
            }
        }
        catch (Exception ex)
        {
            PublishError = ex.Message;
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostApplyPresetAsync(string presetId, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(env.WebRootPath, "data", "experience-presets.json");
            if (!System.IO.File.Exists(path))
            {
                PublishError = "Preset library is not on disk.";
            }
            else
            {
                await using var stream = System.IO.File.OpenRead(path);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!doc.RootElement.TryGetProperty("presets", out var presets) ||
                    !presets.TryGetProperty(presetId, out var preset))
                {
                    PublishError = "That preset is not in the library.";
                }
                else if (preset.TryGetProperty("discovery", out var d))
                {
                    var sid = ExperienceDiscoverySettingsConfiguration.SingletonId;
                    var row = await db.ExperienceDiscoverySettings.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
                    if (row is null)
                    {
                        row = new ExperienceDiscoverySettings { Id = sid, CreatedAtUtc = DateTimeOffset.UtcNow };
                        db.ExperienceDiscoverySettings.Add(row);
                    }

                    ApplyDiscoveryPatch(row, d);
                    await db.SaveChangesAsync(cancellationToken);
                    PublishMessage = "Preset applied to discovery — save other ateliers if you want them in the archive.";
                }
            }
        }
        catch (Exception ex)
        {
            PublishError = ex.Message;
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    private static void ApplyDiscoveryPatch(ExperienceDiscoverySettings row, JsonElement d)
    {
        if (d.TryGetProperty("seasonalOnlyPool", out var sp) && sp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            row.SeasonalOnlyPool = sp.GetBoolean();
        if (d.TryGetProperty("allowSeasonalCups", out var a) && a.ValueKind is JsonValueKind.True or JsonValueKind.False)
            row.AllowSeasonalCups = a.GetBoolean();
        if (d.TryGetProperty("preferSignaturesFirst", out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False)
            row.PreferSignaturesFirst = p.GetBoolean();
        if (d.TryGetProperty("allowRerolls", out var r) && r.ValueKind is JsonValueKind.True or JsonValueKind.False)
            row.AllowRerolls = r.GetBoolean();
        if (d.TryGetProperty("adventureTone", out var t) && t.TryGetInt32(out var ti))
            row.AdventureTone = ti < 1 ? 1 : ti > 5 ? 5 : ti;
        if (d.TryGetProperty("courierMoodCopy", out var c) && c.ValueKind == JsonValueKind.String)
            row.CourierMoodCopy = string.IsNullOrWhiteSpace(c.GetString()) ? null : c.GetString();
        if (d.TryGetProperty("letterRoomContentJson", out var lr) && lr.ValueKind == JsonValueKind.String)
            row.LetterRoomContentJson = string.IsNullOrWhiteSpace(lr.GetString()) ? null : lr.GetString();
    }

    private async Task<string> BuildHubPreviewSeedAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from s in db.ExperienceSignatureSlots.AsNoTracking()
                join m in db.MenuItems.AsNoTracking() on s.MenuItemId equals m.Id
                orderby s.SortOrder
                select new
                {
                    s.IsActive,
                    s.EditorialKicker,
                    s.EditorialBody,
                    m.Id,
                    m.Name,
                    m.Subtitle,
                    m.TastingNotes,
                    m.Price,
                    m.ImageUrl,
                    Cat = m.Category.Name
                })
            .ToListAsync(cancellationToken);

        var sig = rows.Select(s => new
        {
            s.Id,
            s.Name,
            subtitle = s.EditorialKicker ?? s.Subtitle,
            tasting = s.EditorialBody ?? s.TastingNotes,
            s.Price,
            imageUrl = MenuMediaResolver.ResolveCardImageUrl(null, null, s.ImageUrl, null, s.Name, s.Cat),
            s.IsActive
        }).ToList();

        var guidedQs = await db.ExperienceGuidedQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.IsEnabled)
            .OrderBy(q => q.SortOrder)
            .Take(4)
            .ToListAsync(cancellationToken);

        var guided = guidedQs.Select(q => new
        {
            q.Prompt,
            options = q.Options.Where(o => o.IsEnabled).OrderBy(o => o.SortOrder)
                .Take(6)
                .Select(o => new { o.ExternalKey, o.Label, o.Subline })
                .ToList()
        }).ToList();

        var sid = ExperienceDiscoverySettingsConfiguration.SingletonId;
        var disc = await db.ExperienceDiscoverySettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);

        var payload = new
        {
            context = "hub",
            signatures = sig,
            guided = guided,
            discovery = disc is null
                ? null
                : new
                {
                    disc.AdventureTone,
                    disc.AllowRerolls,
                    courier = disc.CourierMoodCopy,
                    disc.SeasonalOnlyPool
                }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public sealed record PublishedSnapshotRow(Guid Id, DateTimeOffset CreatedAtUtc, string? HouseNote);

    private static string Humanize(TimeSpan ago)
    {
        if (ago < TimeSpan.Zero)
            ago = TimeSpan.Zero;
        if (ago.TotalSeconds < 45)
            return "just now";
        if (ago.TotalMinutes < 1.5)
            return "about a minute ago";
        if (ago.TotalMinutes < 45)
            return $"{(int)Math.Round(ago.TotalMinutes)} minutes ago";
        if (ago.TotalHours < 24)
            return $"{(int)Math.Round(ago.TotalHours)} hours ago";
        if (ago.TotalDays < 14)
            return $"{(int)Math.Round(ago.TotalDays)} days ago";
        return $"{(int)Math.Round(ago.TotalDays / 7)} weeks ago";
    }
}
