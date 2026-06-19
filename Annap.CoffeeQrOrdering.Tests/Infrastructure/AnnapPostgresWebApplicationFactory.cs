using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

public sealed class AnnapPostgresWebApplicationFactory : WebApplicationFactory<WebAppEntryPoint>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("annap_order_tests")
        .WithUsername("postgres")
        .WithPassword("postgres-test-secret-16")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var _ = CreateClient();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Cookie auth uses SecurePolicy="Always" for non-dev environments.
        // For HTTP-based test server we want Development so the auth cookie is sendable.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["StaffAuth:UserName"] = "test-host",
                ["StaffAuth:Password"] = "test-staff-secret-16",
                ["KiotViet:IsEnabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddLogging();
        });
    }
}
