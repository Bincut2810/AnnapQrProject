using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience.Signatures;

public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public IReadOnlyList<MenuPickVm> MenuPicks { get; private set; } = [];

    [BindProperty]
    public List<SlotFormRow> SlotForms { get; set; } = [];

    public string? SaveError { get; set; }

    public string ExperiencePreviewSeedJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        MenuPicks = await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived)
            .OrderBy(m => m.Name)
            .Select(m => new MenuPickVm(
                m.Id,
                m.Name,
                m.Subtitle,
                m.TastingNotes,
                m.Price,
                m.ImageUrl,
                m.DetailPosterImagePath,
                m.Category.Name))
            .ToListAsync(cancellationToken);

        var slots = await db.ExperienceSignatureSlots.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id,
                s.MenuItemId,
                s.SortOrder,
                s.IsSpotlight,
                s.SeasonalSpotlightEnabled,
                s.EditorialKicker,
                s.EditorialBody,
                s.IsActive
            })
            .ToListAsync(cancellationToken);

        if (slots.Count == 0)
        {
            SlotForms =
            [
                new SlotFormRow { SortOrder = 0, IsActive = true, IsSpotlight = true }
            ];
            AppendBlankRowIfRoom();
            SetExperiencePreviewSeedJson();
            return;
        }

        SlotForms = slots.Select(s => new SlotFormRow
        {
            SlotId = s.Id,
            SortOrder = s.SortOrder,
            MenuItemId = s.MenuItemId,
            IsSpotlight = s.IsSpotlight,
            SeasonalSpotlightEnabled = s.SeasonalSpotlightEnabled,
            EditorialKicker = s.EditorialKicker,
            EditorialBody = s.EditorialBody,
            IsActive = s.IsActive
        }).ToList();

        AppendBlankRowIfRoom();

        SetExperiencePreviewSeedJson();
    }

    private void SetExperiencePreviewSeedJson()
    {
        ExperiencePreviewSeedJson = JsonSerializer.Serialize(new
        {
            context = "signatures",
            menu = MenuPicks.Select(m => new
            {
                m.Id,
                m.Name,
                m.Subtitle,
                m.TastingNotes,
                m.Price,
                imageUrl = MenuMediaResolver.ResolveCardImageUrl(
                    null, null, m.ImageUrl, null, m.Name, m.CategoryName, m.DetailPosterImagePath)
            }).ToList()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private void AppendBlankRowIfRoom()
    {
        if (SlotForms.Count >= 8)
            return;
        SlotForms.Add(new SlotFormRow
        {
            SortOrder = SlotForms.Count,
            IsActive = false
        });
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (SlotForms is null || SlotForms.Count == 0)
            return RedirectToPage();

        var filled = SlotForms
            .Where(r => r.MenuItemId is Guid mid && mid != Guid.Empty)
            .ToList();
        var activeCount = filled.Count(r => r.IsActive);
        if (activeCount > 4)
        {
            SaveError = "At most four cups may be active on the group rail.";
            await OnGetAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var tracked = await db.ExperienceSignatureSlots.ToListAsync(cancellationToken);
        db.ExperienceSignatureSlots.RemoveRange(tracked);
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var order = 0;
        foreach (var row in SlotForms.OrderBy(r => r.SortOrder))
        {
            if (row.MenuItemId is null || row.MenuItemId == Guid.Empty)
                continue;
            db.ExperienceSignatureSlots.Add(new ExperienceSignatureSlot
            {
                MenuItemId = row.MenuItemId.Value,
                SortOrder = order++,
                IsActive = row.IsActive,
                IsSpotlight = row.IsSpotlight,
                SeasonalSpotlightEnabled = row.SeasonalSpotlightEnabled,
                EditorialKicker = string.IsNullOrWhiteSpace(row.EditorialKicker) ? null : row.EditorialKicker.Trim(),
                EditorialBody = string.IsNullOrWhiteSpace(row.EditorialBody) ? null : row.EditorialBody.Trim(),
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    public sealed record MenuPickVm(
        Guid Id,
        string Name,
        string? Subtitle,
        string? TastingNotes,
        decimal Price,
        string? ImageUrl,
        string? DetailPosterImagePath,
        string CategoryName);

    public sealed class SlotFormRow
    {
        public Guid? SlotId { get; set; }
        public int SortOrder { get; set; }
        public Guid? MenuItemId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSpotlight { get; set; }
        public bool SeasonalSpotlightEnabled { get; set; }
        public string? EditorialKicker { get; set; }
        public string? EditorialBody { get; set; }
    }
}
