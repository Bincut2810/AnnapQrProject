using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff;

[Authorize(Roles = "Staff")]
public sealed class OrdersModel : PageModel
{
    public void OnGet()
    {
        ViewData["Title"] = "Order floor";
    }
}
