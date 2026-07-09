using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Fail-fast checks before accepting production traffic (secrets, unsafe defaults).
/// </summary>
public static class ProductionStartupGuard
{
    public static void Validate(IHostEnvironment env, IConfiguration configuration)
    {
        if (!env.IsProduction()) return;

        var staff = configuration.GetSection(StaffAuthOptions.SectionName).Get<StaffAuthOptions>()
            ?? new StaffAuthOptions();

        StaffAuthPasswordValidator.ValidateProductionPassword(nameof(StaffAuthOptions.Password), staff.Password);
        StaffAuthPasswordValidator.ValidateProductionPassword(
            nameof(StaffAuthOptions.CheckoutPassword),
            staff.CheckoutPassword);
        StaffAuthPasswordValidator.ValidateProductionPassword(
            nameof(StaffAuthOptions.BaristaPassword),
            staff.BaristaPassword);

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
