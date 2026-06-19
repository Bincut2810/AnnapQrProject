using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience.Homepage;

public sealed class IndexModel(IApplicationDbContext db) : PageModel, IHomepageCompositionHost
{
    [BindProperty]
    public HomepageExperienceSettingsAdmin.FormModel HomepageForm { get; set; } = new();

    public string? HomepageCompositionStatus { get; set; }

    public bool HomepageCompositionShowDedicatedLink => false;

    public string HomepageCompositionFormId => "exp-homepage-form";

    public string HomepageCompositionPostHandler => "SaveHomepageComposition";

    public int VisibleModeCount => HomepageExperienceSettingsAdmin.VisibleCount(HomepageForm);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        HomepageForm = await HomepageExperienceSettingsAdmin.LoadAsync(db, cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveHomepageCompositionAsync(CancellationToken cancellationToken)
    {
        await HomepageExperienceSettingsAdmin.SaveAsync(db, HomepageForm, cancellationToken);
        HomepageCompositionStatus = "Lobby composition saved.";
        await OnGetAsync(cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) =>
        OnPostSaveHomepageCompositionAsync(cancellationToken);
}
