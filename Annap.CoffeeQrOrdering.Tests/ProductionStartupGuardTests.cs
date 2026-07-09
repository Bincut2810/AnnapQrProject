using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ProductionStartupGuardTests
{
    private static void Validate(
        string environment,
        string staffPassword,
        string? checkoutPassword = null,
        string? baristaPassword = null,
        string? connectionString = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaffAuth:UserName"] = "host",
                ["StaffAuth:Password"] = staffPassword,
                ["StaffAuth:CheckoutPassword"] = checkoutPassword ?? "CheckoutFloor-2026-Secure",
                ["StaffAuth:BaristaPassword"] = baristaPassword ?? "BaristaFloor-2026-Secure",
                ["ConnectionStrings:DefaultConnection"] = connectionString
                    ?? "Host=localhost;Database=test;Username=postgres;Password=deployment-secret-32"
            })
            .Build();

        var env = new HostEnvironment { EnvironmentName = environment };
        ProductionStartupGuard.Validate(env, config);
    }

    [Fact]
    public void Development_allows_default_ChangeMe_password()
    {
        var ex = Record.Exception(() => Validate(Environments.Development, "ChangeMe", "", ""));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("ChangeMe")]
    [InlineData("changeme")]
    [InlineData("password")]
    [InlineData("admin")]
    [InlineData("short")]
    public void Production_rejects_weak_or_short_staff_password(string password)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Validate(Environments.Production, password));
        Assert.Contains("StaffAuth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_accepts_strong_unique_staff_passwords()
    {
        var ex = Record.Exception(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                "CheckoutFloor-2026-Secure",
                "BaristaFloor-2026-Secure"));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("checkout-dev", "BaristaFloor-2026-Secure")]
    [InlineData("BaristaFloor-2026-Secure", "barista-dev")]
    public void Production_rejects_dev_default_role_passwords(string checkoutPassword, string baristaPassword)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                checkoutPassword,
                baristaPassword));

        Assert.Contains("StaffAuth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_empty_checkout_password()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                "",
                "BaristaFloor-2026-Secure"));

        Assert.Contains("CheckoutPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_local_postgres_password_in_connection_string()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                connectionString: "Host=localhost;Database=test;Username=postgres;Password=annap_local_dev"));

        Assert.Contains("PostgreSQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("checkout-dev")]
    [InlineData("barista-dev")]
    public void StaffAuthPasswordValidator_rejects_known_dev_defaults(string password)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StaffAuthPasswordValidator.ValidateProductionPassword("TestPassword", password));
        Assert.True(
            ex.Message.Contains("weak", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("at least", StringComparison.OrdinalIgnoreCase),
            ex.Message);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Annap.CoffeeQrOrdering.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
