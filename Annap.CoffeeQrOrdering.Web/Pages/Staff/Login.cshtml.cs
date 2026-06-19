using System.Security.Claims;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff;

[AllowAnonymous]
public sealed class LoginModel(IOptions<StaffAuthOptions> options) : PageModel
{
    [BindProperty]
    public string UserName { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Staff/Orders");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var o = options.Value;
        if (SlowEquals(UserName?.Trim(), o.UserName) && SlowEquals(Password ?? "", o.Password))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, o.UserName),
                new(ClaimTypes.Role, "Staff")
            };
            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(id),
                new AuthenticationProperties { IsPersistent = true });

            return RedirectToPage("/Staff/Orders");
        }

        ErrorMessage = "Please check the sign-in you entered.";
        return Page();
    }

    private static bool SlowEquals(string? a, string? b)
    {
        var ab = Encoding.UTF8.GetBytes(a ?? "");
        var bb = Encoding.UTF8.GetBytes(b ?? "");
        if (ab.Length != bb.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
