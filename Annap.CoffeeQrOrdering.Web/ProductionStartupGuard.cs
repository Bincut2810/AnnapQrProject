using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Fail-fast checks before accepting production traffic (secrets, unsafe defaults).
/// </summary>
public static class ProductionStartupGuard
{
    private static readonly string[] WeakStaffPasswords =
    [
        "changeme",
        "change-this-staff-password",
        "password",
        "password123",
        "admin",
        "admin123",
        "123456",
        "123456789012",
        "staff",
        "staffpassword",
        "annap",
        "annapcoffee",
        "host",
        "welcome",
        "qwertyuiop12"
    ];

    public static void Validate(IHostEnvironment env, IConfiguration configuration)
    {
        if (!env.IsProduction()) return;

        var staff = configuration.GetSection(StaffAuthOptions.SectionName).Get<StaffAuthOptions>();
        var pwd = staff?.Password?.Trim() ?? "";
        if (pwd.Length < 12)
            throw new InvalidOperationException(
                "Production: StaffAuth:Password must be at least 12 characters. Set STAFF_PASSWORD or StaffAuth__Password via environment.");

        foreach (var w in WeakStaffPasswords)
        {
            if (pwd.Equals(w, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Production: StaffAuth:Password is a known weak value. Choose a unique café password.");
        }

        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Production: ConnectionStrings:DefaultConnection is required.");

        if (conn.Contains("Password=annap_local_dev", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: local PostgreSQL password detected. Set POSTGRES_PASSWORD to a deployment-specific value.");
        }

        if (conn.Contains("Password=change-this-local-or-production-password", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: placeholder PostgreSQL password detected. Set POSTGRES_PASSWORD before starting production.");
        }

        if (conn.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: default PostgreSQL password detected. Set POSTGRES_PASSWORD to a deployment-specific value.");
        }
    }
}
