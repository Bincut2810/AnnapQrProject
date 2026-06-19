using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience.Group;

public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    [BindProperty]
    public GroupExperienceForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var sid = ExperienceGroupSettingsConfiguration.SingletonId;
        var row = await db.ExperienceGroupSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
        Form = row is null ? new GroupExperienceForm() : Map(row);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var sid = ExperienceGroupSettingsConfiguration.SingletonId;
        var existing = await db.ExperienceGroupSettings.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
        var min = ClampInt(Form.MinGuests, 1, 12);
        var max = ClampInt(Form.MaxGuests, min, 20);

        if (existing is null)
        {
            db.ExperienceGroupSettings.Add(new ExperienceGroupSettings
            {
                Id = sid,
                ArrivalKicker = NullIfEmpty(Form.ArrivalKicker) ?? "",
                GuestCountPrompt = string.IsNullOrWhiteSpace(Form.GuestCountPrompt)
                    ? "How many guests are joining?"
                    : Form.GuestCountPrompt.Trim(),
                GuestCountLead = NullIfEmpty(Form.GuestCountLead),
                MinGuests = min,
                MaxGuests = max,
                GuestTabsIntro = NullIfEmpty(Form.GuestTabsIntro),
                GuestDoneHint = NullIfEmpty(Form.GuestDoneHint),
                SummaryHeadline = string.IsNullOrWhiteSpace(Form.SummaryHeadline)
                    ? "Your table"
                    : Form.SummaryHeadline.Trim(),
                SummaryLead = NullIfEmpty(Form.SummaryLead),
                HospitalityClosing = NullIfEmpty(Form.HospitalityClosing),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.ArrivalKicker = NullIfEmpty(Form.ArrivalKicker) ?? "";
            existing.GuestCountPrompt = string.IsNullOrWhiteSpace(Form.GuestCountPrompt)
                ? "How many guests are joining?"
                : Form.GuestCountPrompt.Trim();
            existing.GuestCountLead = NullIfEmpty(Form.GuestCountLead);
            existing.MinGuests = min;
            existing.MaxGuests = max;
            existing.GuestTabsIntro = NullIfEmpty(Form.GuestTabsIntro);
            existing.GuestDoneHint = NullIfEmpty(Form.GuestDoneHint);
            existing.SummaryHeadline = string.IsNullOrWhiteSpace(Form.SummaryHeadline)
                ? "Your table"
                : Form.SummaryHeadline.Trim();
            existing.SummaryLead = NullIfEmpty(Form.SummaryLead);
            existing.HospitalityClosing = NullIfEmpty(Form.HospitalityClosing);
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    private static GroupExperienceForm Map(ExperienceGroupSettings x) => new()
    {
        ArrivalKicker = x.ArrivalKicker ?? "",
        GuestCountPrompt = x.GuestCountPrompt,
        GuestCountLead = x.GuestCountLead ?? "",
        MinGuests = x.MinGuests,
        MaxGuests = x.MaxGuests,
        GuestTabsIntro = x.GuestTabsIntro ?? "",
        GuestDoneHint = x.GuestDoneHint ?? "",
        SummaryHeadline = x.SummaryHeadline,
        SummaryLead = x.SummaryLead ?? "",
        HospitalityClosing = x.HospitalityClosing ?? ""
    };

    private static int ClampInt(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public sealed class GroupExperienceForm
    {
        public string ArrivalKicker { get; set; } = "";

        public string GuestCountPrompt { get; set; } = "How many guests?";

        public string GuestCountLead { get; set; } = "";

        public int MinGuests { get; set; } = 1;

        public int MaxGuests { get; set; } = 10;

        public string GuestTabsIntro { get; set; } = "";

        public string GuestDoneHint { get; set; } = "";

        public string SummaryHeadline { get; set; } = "Your table";

        public string SummaryLead { get; set; } = "";

        public string HospitalityClosing { get; set; } = "";
    }
}
