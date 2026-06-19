using Annap.CoffeeQrOrdering.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ProductionStartupGuardTests
{
    private static void Validate(string environment, string staffPassword, string? connectionString = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaffAuth:UserName"] = "host",
                ["StaffAuth:Password"] = staffPassword,
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
        var ex = Record.Exception(() => Validate(Environments.Development, "ChangeMe"));
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
    public void Production_accepts_strong_unique_staff_password()
    {
        var ex = Record.Exception(() => Validate(Environments.Production, "CafeFloor-2026-Secure"));
        Assert.Null(ex);
    }

    [Fact]
    public void Production_rejects_local_postgres_password_in_connection_string()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                "Host=localhost;Database=test;Username=postgres;Password=annap_local_dev"));

        Assert.Contains("PostgreSQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Annap.CoffeeQrOrdering.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
