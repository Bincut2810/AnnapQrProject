using Annap.CoffeeQrOrdering.Web.Services;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Experience;

public interface IHomepageCompositionHost
{
    HomepageExperienceSettingsAdmin.FormModel HomepageForm { get; }

    string? HomepageCompositionStatus { get; }

    bool HomepageCompositionShowDedicatedLink { get; }

    string HomepageCompositionFormId { get; }

    string HomepageCompositionPostHandler { get; }
}
