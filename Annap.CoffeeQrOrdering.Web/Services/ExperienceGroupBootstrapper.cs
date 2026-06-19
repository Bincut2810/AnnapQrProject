using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

public static class ExperienceGroupBootstrapper
{
    public static async Task EnsureDefaultsAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var id = ExperienceGroupSettingsConfiguration.SingletonId;
        if (await db.ExperienceGroupSettings.AnyAsync(x => x.Id == id, cancellationToken))
            return;

        db.ExperienceGroupSettings.Add(new ExperienceGroupSettings
        {
            Id = id,
            ArrivalKicker = "Together at the table",
            GuestCountPrompt = "How many guests?",
            GuestCountLead = "Each guest will have a quiet card — you can move between them when everyone is ready.",
            MinGuests = 1,
            MaxGuests = 10,
            GuestTabsIntro = "Choose who is ordering, then add cups to the shared tray.",
            GuestDoneHint = "When a guest has finished choosing, mark their card complete.",
            SummaryHeadline = "Table summary",
            SummaryLead = "A single tray — composed like a tasting flight.",
            HospitalityClosing = "When you are ready, open the cart and send the order to the bar.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
