using System.Diagnostics;
using Annap.CoffeeQrOrdering.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Annap.CoffeeQrOrdering.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel(IStringLocalizer<SharedResources> localizer) : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCodeValue { get; set; } = 500;

    public string Title { get; set; } = string.Empty;

    public string Lede { get; set; } = string.Empty;

    public void OnGet(int? statusCode = null)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        StatusCodeValue = statusCode is > 0 ? statusCode.Value : Response.StatusCode;
        if (StatusCodeValue is < 400 or > 599)
            StatusCodeValue = 500;

        Response.StatusCode = StatusCodeValue;

        (Title, Lede) = StatusCodeValue switch
        {
            404 => (localizer["ops.error.404Title"], localizer["ops.error.404Lede"]),
            403 => (localizer["ops.error.403Title"], localizer["ops.error.403Lede"]),
            503 => (localizer["ops.error.503Title"], localizer["ops.error.503Lede"]),
            _ => (localizer["ops.error.genericTitle"], localizer["ops.error.genericLede"])
        };
    }
}
