namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>
/// Shared staff/admin sign-in. Development may use the default password;
/// production must set <c>STAFF_PASSWORD</c> (≥12 characters, not a known weak value).
/// <see cref="ProductionStartupGuard"/> fails fast when requirements are not met.
/// </summary>
public sealed class StaffAuthOptions
{
    public const string SectionName = "StaffAuth";

    /// <summary>Sign-in name for the staff back room.</summary>
    public string UserName { get; set; } = "host";

    /// <summary>Local-dev default only — override via <c>STAFF_PASSWORD</c> in production.</summary>
    public string Password { get; set; } = "ChangeMe";
}
