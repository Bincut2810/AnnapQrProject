using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public IReadOnlyList<MenuCategoryGroupVm> Groups { get; private set; } = Array.Empty<MenuCategoryGroupVm>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var categories = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var items = await db.MenuItems.AsNoTracking()
            .Include(i => i.Category)
            .OrderBy(i => i.DisplaySortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        Groups = categories
            .Select(c => new MenuCategoryGroupVm(
                c.Id,
                c.Name,
                items.Where(i => i.CategoryId == c.Id).Select(MenuItemProjections.ToAdminSummary).ToList()))
            .ToList();
    }
}
