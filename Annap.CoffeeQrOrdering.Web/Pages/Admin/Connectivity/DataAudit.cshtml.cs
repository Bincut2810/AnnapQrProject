using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Connectivity;

[Authorize(Policy = "Staff")]
public sealed class DataAuditModel(ProductionDataAuditService auditService) : PageModel
{
    public ProductionDataAuditReport Report { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Report = await auditService.BuildAsync(cancellationToken);
    }
}
