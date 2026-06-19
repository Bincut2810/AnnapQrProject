using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience.Discovery;

public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public ExperienceDiscoverySettingsVm Settings { get; private set; } = new();

    public IReadOnlyList<DiscoveryMenuRowVm> MenuRows { get; private set; } = [];

    [BindProperty]
    public ExperienceDiscoverySettingsVm SettingsForm { get; set; } = new();

    [BindProperty]
    public List<DiscoveryMenuRowForm> MenuForms { get; set; } = [];

    public string ExperiencePreviewSeedJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var row = await db.ExperienceDiscoverySettings.AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        Settings = row is null
            ? new ExperienceDiscoverySettingsVm()
            : new ExperienceDiscoverySettingsVm
            {
                SeasonalOnlyPool = row.SeasonalOnlyPool,
                AllowSeasonalCups = row.AllowSeasonalCups,
                PreferSignaturesFirst = row.PreferSignaturesFirst,
                AllowRerolls = row.AllowRerolls,
                AdventureTone = row.AdventureTone is >= 1 and <= 5 ? row.AdventureTone : 3,
                CourierMoodCopy = row.CourierMoodCopy ?? "",
                FatigueCopyEvenLeg = row.FatigueCopyEvenLeg ?? "",
                FatigueCopyOddLeg = row.FatigueCopyOddLeg ?? "",
                RerollPacingJson = row.RerollPacingJson ?? "{}",
                RevealCopyNotes = row.RevealCopyNotes ?? "",
                LetterRoomContentJson = row.LetterRoomContentJson ?? ""
            };
        SettingsForm = Settings;

        MenuRows = await db.MenuItems.AsNoTracking()
            .Where(m => !m.IsArchived)
            .OrderByDescending(m => m.IsSignature)
            .ThenBy(m => m.Name)
            .Select(m => new DiscoveryMenuRowVm(
                m.Id,
                m.Name,
                m.DiscoveryWeight,
                m.IsHiddenDiscovery,
                m.IsDiscoveryEligible,
                m.DiscoveryStory ?? "",
                m.IsSeasonalHighlight))
            .Take(72)
            .ToListAsync(cancellationToken);

        MenuForms = MenuRows.Select(r => new DiscoveryMenuRowForm
        {
            MenuItemId = r.Id,
            DiscoveryWeight = r.DiscoveryWeight,
            IsHiddenDiscovery = r.IsHiddenDiscovery,
            IsDiscoveryEligible = r.IsDiscoveryEligible,
            DiscoveryStory = r.DiscoveryStory
        }).ToList();

        SetExperiencePreviewSeedJson();
    }

    private void SetExperiencePreviewSeedJson()
    {
        var rows = MenuRows.Zip(MenuForms, (r, f) => new
        {
            r.Id,
            r.Name,
            r.IsSeasonalHighlight,
            f.DiscoveryWeight,
            f.IsHiddenDiscovery,
            f.IsDiscoveryEligible,
            f.DiscoveryStory
        }).ToList();

        ExperiencePreviewSeedJson = JsonSerializer.Serialize(new
        {
            context = "discovery",
            settings = SettingsForm,
            cups = rows
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var sid = ExperienceDiscoverySettingsConfiguration.SingletonId;
        var existing = await db.ExperienceDiscoverySettings.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
        if (existing is null)
        {
            db.ExperienceDiscoverySettings.Add(new ExperienceDiscoverySettings
            {
                Id = sid,
                SeasonalOnlyPool = SettingsForm.SeasonalOnlyPool,
                AllowSeasonalCups = SettingsForm.AllowSeasonalCups,
                PreferSignaturesFirst = SettingsForm.PreferSignaturesFirst,
                AllowRerolls = SettingsForm.AllowRerolls,
                AdventureTone = ClampAdventure(SettingsForm.AdventureTone),
                CourierMoodCopy = NullIfEmpty(SettingsForm.CourierMoodCopy),
                FatigueCopyEvenLeg = NullIfEmpty(SettingsForm.FatigueCopyEvenLeg),
                FatigueCopyOddLeg = NullIfEmpty(SettingsForm.FatigueCopyOddLeg),
                RerollPacingJson = string.IsNullOrWhiteSpace(SettingsForm.RerollPacingJson) ? "{}" : SettingsForm.RerollPacingJson.Trim(),
                RevealCopyNotes = NullIfEmpty(SettingsForm.RevealCopyNotes),
                LetterRoomContentJson = NullIfEmpty(SettingsForm.LetterRoomContentJson),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.SeasonalOnlyPool = SettingsForm.SeasonalOnlyPool;
            existing.AllowSeasonalCups = SettingsForm.AllowSeasonalCups;
            existing.PreferSignaturesFirst = SettingsForm.PreferSignaturesFirst;
            existing.AllowRerolls = SettingsForm.AllowRerolls;
            existing.AdventureTone = ClampAdventure(SettingsForm.AdventureTone);
            existing.CourierMoodCopy = NullIfEmpty(SettingsForm.CourierMoodCopy);
            existing.FatigueCopyEvenLeg = NullIfEmpty(SettingsForm.FatigueCopyEvenLeg);
            existing.FatigueCopyOddLeg = NullIfEmpty(SettingsForm.FatigueCopyOddLeg);
            existing.RerollPacingJson = string.IsNullOrWhiteSpace(SettingsForm.RerollPacingJson) ? "{}" : SettingsForm.RerollPacingJson.Trim();
            existing.RevealCopyNotes = NullIfEmpty(SettingsForm.RevealCopyNotes);
            existing.LetterRoomContentJson = NullIfEmpty(SettingsForm.LetterRoomContentJson);
        }

        foreach (var row in MenuForms)
        {
            var m = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == row.MenuItemId, cancellationToken);
            if (m is null)
                continue;
            m.DiscoveryWeight = row.DiscoveryWeight < 0 ? 0 : row.DiscoveryWeight;
            m.IsHiddenDiscovery = row.IsHiddenDiscovery;
            m.IsDiscoveryEligible = row.IsDiscoveryEligible;
            m.DiscoveryStory = string.IsNullOrWhiteSpace(row.DiscoveryStory) ? null : row.DiscoveryStory.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    private static int ClampAdventure(int v) => v < 1 ? 1 : v > 5 ? 5 : v;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public sealed class ExperienceDiscoverySettingsVm
    {
        public bool SeasonalOnlyPool { get; set; }

        public bool AllowSeasonalCups { get; set; } = true;

        public bool PreferSignaturesFirst { get; set; } = true;

        public bool AllowRerolls { get; set; } = true;

        /// <summary>1 quiet — 5 adventurous.</summary>
        public int AdventureTone { get; set; } = 3;

        public string CourierMoodCopy { get; set; } = "";
        public string FatigueCopyEvenLeg { get; set; } = "";
        public string FatigueCopyOddLeg { get; set; } = "";
        public string RerollPacingJson { get; set; } = "{}";

        public string RevealCopyNotes { get; set; } = "";

        /// <summary>Optional JSON for Letter Room desk (title, envelopes[], CTAs, refusalLines, insideLetterLines, paperTheme).</summary>
        public string LetterRoomContentJson { get; set; } = "";
    }

    public sealed record DiscoveryMenuRowVm(
        Guid Id,
        string Name,
        decimal DiscoveryWeight,
        bool IsHiddenDiscovery,
        bool IsDiscoveryEligible,
        string DiscoveryStory,
        bool IsSeasonalHighlight);

    public sealed class DiscoveryMenuRowForm
    {
        public Guid MenuItemId { get; set; }
        public decimal DiscoveryWeight { get; set; } = 1m;
        public bool IsHiddenDiscovery { get; set; }
        public bool IsDiscoveryEligible { get; set; } = true;
        public string DiscoveryStory { get; set; } = "";
    }
}
