using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Menu;

public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public List<MenuCategory> Categories { get; private set; } = [];

    /// <summary>Optional table handoff from home (<c>?vt=</c> or session on client).</summary>
    public Guid? VenueTableId { get; private set; }

    public string? TableGuestLabel { get; private set; }

    public bool HasSeatedTable => VenueTableId is not null;

    public async Task OnGetAsync(Guid? vt, CancellationToken cancellationToken)
    {
        if (vt is Guid g)
        {
            var t = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == g && v.IsActive, cancellationToken);
            if (t is not null)
            {
                VenueTableId = t.Id;
                TableGuestLabel = string.IsNullOrWhiteSpace(t.DisplayLabel) ? t.DisplayCode : t.DisplayLabel;
            }
        }

        Categories = await db.MenuCategories
            .AsNoTracking()
            .Include(c => c.Items)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var cat in Categories)
        {
            cat.Items.RemoveAll(i => !i.IsAvailable || i.IsArchived);
            cat.Items = cat.Items.OrderBy(i => i.DisplaySortOrder).ThenBy(i => i.Name).ToList();
        }
    }
}
