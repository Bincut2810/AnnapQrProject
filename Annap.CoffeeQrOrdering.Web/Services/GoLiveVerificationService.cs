using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Infrastructure.Sommelier;
using Annap.CoffeeQrOrdering.Web.GuestExperience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Automated production go-live gates (Phase 8.6). Operational verification only.</summary>
public sealed class GoLiveVerificationService(
    AppDbContext db,
    IMenuInventoryGate inventoryGate,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IOptions<SommelierOpenAiOptions> sommelierOptions,
    HealthCheckService healthCheckService,
    IHttpClientFactory httpClientFactory,
    IAppUrlService appUrlService)
{
    private static readonly string[] CoffeeFamilyCategories =
        BeverageFamilyGrounding.AllowedCategoryNames(BeverageFamilyGrounding.Coffee).ToArray();

    public async Task<int> RunAsync(WebApplication app, CancellationToken cancellationToken)
    {
        var dbTarget = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
        Console.WriteLine("ANNAP Go-Live Verification");
        Console.WriteLine("============================");
        Console.WriteLine();

        var databaseGate = await SafeGateAsync(
            () => VerifyDatabaseAsync(dbTarget, cancellationToken),
            "Database").ConfigureAwait(false);
        PrintDatabaseSection(databaseGate, dbTarget, environment.EnvironmentName);

        var dbReady = databaseGate.Passed;
        var specialtyGate = dbReady
            ? await SafeGateAsync(() => VerifySpecialtyLotsAsync(cancellationToken), "Specialty Lots").ConfigureAwait(false)
            : Skipped("Specialty Lots", "Database unreachable.");
        var signatureGate = dbReady
            ? await SafeGateAsync(() => VerifySignatureIntegrityAsync(cancellationToken), "Signature Integrity").ConfigureAwait(false)
            : Skipped("Signature Integrity", "Database unreachable.");

        var poolCount = -1;
        GoLiveGateResult poolGate;
        if (dbReady)
        {
            try
            {
                poolCount = await CalculateSpecialtyRecommendationPoolCountAsync(cancellationToken).ConfigureAwait(false);
                poolGate = VerifyRecommendationPool(poolCount);
            }
            catch (Exception ex)
            {
                poolCount = -1;
                poolGate = Fail("Pool Size", Condense(ex.Message));
            }
        }
        else
        {
            poolGate = Skipped("Pool Size", "Database unreachable.");
        }

        PrintPoolSection(poolCount, poolGate);

        var editorialGate = dbReady
            ? await SafeGateAsync(() => VerifyEditorialContentAsync(cancellationToken), "Editorial Content").ConfigureAwait(false)
            : Skipped("Editorial Content", "Database unreachable.");
        var bootstrapGate = dbReady
            ? await SafeGateAsync(() => VerifyBootstrapAsync(cancellationToken), "Bootstrap").ConfigureAwait(false)
            : Skipped("Bootstrap", "Database unreachable.");
        PrintBootstrapLine(bootstrapGate);

        var healthGate = await SafeGateAsync(
            () => VerifyHealthAsync(app, dbReady, cancellationToken),
            "Health").ConfigureAwait(false);
        var openAiWarning = BuildOpenAiWarning();
        PrintHealthSection(healthGate, openAiWarning);

        var gates = new[]
        {
            databaseGate,
            specialtyGate,
            signatureGate,
            poolGate,
            editorialGate,
            bootstrapGate,
            healthGate
        };

        var ready = gates.All(g => g.Passed);
        var warnings = new List<string>();
        if (openAiWarning is not null)
            warnings.Add(openAiWarning);
        var renderAppUrlWarning = BuildQrPublicUrlWarnings();
        if (renderAppUrlWarning.Count > 0)
            warnings.AddRange(renderAppUrlWarning);
        PrintSummary(gates, ready, warnings);

        return ready ? 0 : 1;
    }

    private async Task<GoLiveGateResult> VerifyDatabaseAsync(
        DatabaseStartupHelper.DatabaseTarget dbTarget,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
        {
            return Fail(
                "Database",
                "ConnectionStrings:DefaultConnection is missing.");
        }

        try
        {
            var ok = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                return Fail(
                    "Database",
                    $"PostgreSQL not reachable at {dbTarget.Display}.");
            }

            return Pass("Database");
        }
        catch (Exception ex)
        {
            return Fail(
                "Database",
                $"PostgreSQL connection failed at {dbTarget.Display}: {Condense(ex.Message)}");
        }
    }

    private async Task<GoLiveGateResult> VerifySpecialtyLotsAsync(CancellationToken cancellationToken)
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.CatalogKey != null && AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys.Contains(m.CatalogKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byKey = items
            .Where(m => !string.IsNullOrWhiteSpace(m.CatalogKey))
            .ToDictionary(m => m.CatalogKey!.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys)
        {
            if (!byKey.TryGetValue(key, out var item))
            {
                return Fail(
                    "Specialty Lots",
                    $"Catalog key {key} is missing from menu_items.");
            }

            if (!item.IsAvailable)
            {
                return Fail(
                    "Specialty Lots",
                    $"Catalog key {key} ({item.Name}) has IsAvailable=false.");
            }

            if (item.IsArchived)
            {
                return Fail(
                    "Specialty Lots",
                    $"Catalog key {key} ({item.Name}) has IsArchived=true.");
            }

            if (!item.IsSignature)
            {
                return Fail(
                    "Specialty Lots",
                    $"Catalog key {key} ({item.Name}) has IsSignature=false.");
            }

            var categoryName = item.Category?.Name?.Trim() ?? "";
            if (!categoryName.Equals(AnnapSpecialtyCoffeeCatalog.CategoryName, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    "Specialty Lots",
                    $"Catalog key {key} ({item.Name}) category is '{categoryName}', expected '{AnnapSpecialtyCoffeeCatalog.CategoryName}'.");
            }
        }

        var missing = AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys
            .Where(key => !byKey.ContainsKey(key))
            .ToList();
        if (missing.Count > 0)
        {
            return Fail(
                "Specialty Lots",
                $"Missing catalog keys: {string.Join(", ", missing)}.");
        }

        return Pass("Specialty Lots");
    }

    private async Task<GoLiveGateResult> VerifySignatureIntegrityAsync(CancellationToken cancellationToken)
    {
        var contaminants = await db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m =>
                m.IsSignature
                && m.Category != null
                && CoffeeFamilyCategories.Contains(m.Category.Name))
            .Select(m => new { m.CatalogKey, m.Name, Category = m.Category!.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var unexpected = contaminants
            .Where(m => !AnnapSpecialtyCoffeeCatalog.IsProtectedCatalogKey(m.CatalogKey))
            .ToList();

        if (unexpected.Count == 0)
            return Pass("Signature Integrity");

        var sample = string.Join(
            ", ",
            unexpected.Take(6).Select(m =>
                $"{m.Name} ({m.Category}, key={m.CatalogKey ?? "null"})"));

        return Fail(
            "Signature Integrity",
            $"{unexpected.Count} unexpected IsSignature=true item(s) in coffee-family categories: {sample}.");
    }

    private async Task<int> CalculateSpecialtyRecommendationPoolCountAsync(CancellationToken cancellationToken)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);
        var raw = await db.MenuItems
            .AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived && !blocked.Contains(m.Id))
            .Select(m => new PoolMenuRow(
                m.Id,
                m.Name,
                m.ItemType,
                m.IngredientBreakdown,
                m.FlavorTags,
                m.Category.Name,
                m.IsSignature))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        const string familyKey = BeverageFamilyGrounding.Coffee;
        var filteredRaw = raw
            .Where(m => BeverageFamilyGrounding.Matches(
                familyKey,
                m.CategoryName,
                m.Name,
                m.ItemType,
                m.IngredientBreakdown,
                m.FlavorTags))
            .ToList();

        var signatureOnly = filteredRaw.Where(m => m.IsSignature).ToList();
        if (signatureOnly.Count > 0)
            filteredRaw = signatureOnly;

        return filteredRaw.Count;
    }

    private static GoLiveGateResult VerifyRecommendationPool(int poolCount)
    {
        const int expected = 4;

        if (poolCount == expected)
            return Pass("Pool Size");

        return Fail(
            "Pool Size",
            $"specialty_pool_count={poolCount}, expected {expected} (production specialty recommendation pool).");
    }

    private static void PrintPoolSection(int poolCount, GoLiveGateResult poolGate)
    {
        Console.WriteLine("4. Recommendation pool audit");
        if (poolCount >= 0)
            Console.WriteLine($"   specialty_pool_count = {poolCount}");
        else
            Console.WriteLine("   specialty_pool_count = (not calculated)");
        Console.WriteLine($"   Status: {(poolGate.Passed ? "PASS" : "FAIL")}");
        if (!poolGate.Passed && poolGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {poolGate.FailureReason}");
        Console.WriteLine();
    }

    private static async Task<GoLiveGateResult> SafeGateAsync(
        Func<Task<GoLiveGateResult>> verify,
        string gateName)
    {
        try
        {
            return await verify().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(gateName, Condense(ex.Message));
        }
    }

    private static GoLiveGateResult Skipped(string name, string reason) => Fail(name, reason);

    private async Task<GoLiveGateResult> VerifyRecommendationPoolAsync(CancellationToken cancellationToken)
    {
        var poolCount = await CalculateSpecialtyRecommendationPoolCountAsync(cancellationToken).ConfigureAwait(false);
        return VerifyRecommendationPool(poolCount);
    }

    private async Task<GoLiveGateResult> VerifyEditorialContentAsync(CancellationToken cancellationToken)
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .Where(m => m.CatalogKey != null && AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys.Contains(m.CatalogKey))
            .Select(m => new
            {
                m.CatalogKey,
                m.Name,
                m.Origin,
                m.ShortStory,
                m.ProducerStory
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var key in AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys)
        {
            var item = items.FirstOrDefault(m =>
                string.Equals(m.CatalogKey, key, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return Fail(
                    "Editorial Content",
                    $"Catalog key {key} not found for editorial audit.");
            }

            if (string.IsNullOrWhiteSpace(item.Origin))
            {
                return Fail(
                    "Editorial Content",
                    $"Catalog key {key} ({item.Name}) is missing Origin.");
            }

            if (string.IsNullOrWhiteSpace(item.ShortStory))
            {
                return Fail(
                    "Editorial Content",
                    $"Catalog key {key} ({item.Name}) is missing ShortStory.");
            }

            if (string.IsNullOrWhiteSpace(item.ProducerStory))
            {
                return Fail(
                    "Editorial Content",
                    $"Catalog key {key} ({item.Name}) is missing ProducerStory.");
            }
        }

        return Pass("Editorial Content");
    }

    private async Task<GoLiveGateResult> VerifyBootstrapAsync(CancellationToken cancellationToken)
    {
        var specialtyOk = (await VerifySpecialtyLotsAsync(cancellationToken).ConfigureAwait(false)).Passed;
        if (!specialtyOk)
        {
            return Fail(
                "Bootstrap",
                "Specialty coffee catalog rows are not in bootstrap-ready state.");
        }

        var setKey = GuidedSommelierCatalog.QuestionSetId;
        var specialtyQuestions = await db.ExperienceGuidedQuestions
            .AsNoTracking()
            .Where(q => q.SetKey == setKey && (q.ExternalKey == "q_sc_flavor" || q.ExternalKey == "q_sc_experience"))
            .Select(q => q.ExternalKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missingQuestions = new[] { "q_sc_flavor", "q_sc_experience" }
            .Where(key => !specialtyQuestions.Contains(key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingQuestions.Count > 0)
        {
            return Fail(
                "Bootstrap",
                $"Guided specialty discovery questions missing: {string.Join(", ", missingQuestions)}.");
        }

        var baseQuestionCount = await db.ExperienceGuidedQuestions
            .AsNoTracking()
            .CountAsync(q => q.SetKey == setKey, cancellationToken)
            .ConfigureAwait(false);
        if (baseQuestionCount < 4)
        {
            return Fail(
                "Bootstrap",
                $"Guided sommelier question set '{setKey}' has only {baseQuestionCount} question(s); bootstrap incomplete.");
        }

        return Pass("Bootstrap");
    }

    private async Task<GoLiveGateResult> VerifyHealthAsync(
        WebApplication app,
        bool databaseReady,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        try
        {
            var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            if (healthReport.Status != HealthStatus.Healthy)
            {
                var unhealthy = healthReport.Entries
                    .Where(e => e.Value.Status != HealthStatus.Healthy)
                    .Select(e => $"{e.Key}={e.Value.Status}")
                    .ToList();
                failures.Add(
                    unhealthy.Count > 0
                        ? $"Health checks unhealthy: {string.Join(", ", unhealthy)}."
                        : $"Health checks status={healthReport.Status}.");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Health check service failed: {Condense(ex.Message)}.");
        }

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
                failures.Add("Database connectivity check failed inside health verification.");
        }
        catch (Exception ex)
        {
            failures.Add($"Database connectivity check failed: {Condense(ex.Message)}.");
        }

        var endpointFailure = databaseReady
            ? await VerifyHealthEndpointAsync(app, cancellationToken).ConfigureAwait(false)
            : "Skipped /health probe because database is unreachable.";
        if (endpointFailure is not null)
            failures.Add(endpointFailure);

        if (failures.Count == 0)
            return Pass("Health");

        return Fail("Health", string.Join(" ", failures));
    }

    private async Task<string?> VerifyHealthEndpointAsync(
        WebApplication app,
        CancellationToken cancellationToken)
    {
        var configuredUrl = ResolveConfiguredHealthUrl();
        var startedLocally = false;

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (app.Urls.Count == 0)
                app.Urls.Add("http://127.0.0.1:0");

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            startedLocally = true;
            configuredUrl = ResolveListeningHealthUrl(app);
        }

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (startedLocally)
                await app.StopAsync(cancellationToken).ConfigureAwait(false);

            return "Application health endpoint URL could not be resolved.";
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(GoLiveVerificationService));
            client.Timeout = TimeSpan.FromSeconds(12);
            using var response = await client.GetAsync(configuredUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return $"GET {configuredUrl} returned HTTP {(int)response.StatusCode}.";
            }

            var body = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
            if (body.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
                return $"GET {configuredUrl} reported Unhealthy.";

            if (!body.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
                return $"GET {configuredUrl} did not report Healthy (body='{Condense(body)}').";
        }
        catch (Exception ex)
        {
            return $"GET {configuredUrl} failed: {Condense(ex.Message)}.";
        }
        finally
        {
            if (startedLocally)
                await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private string? ResolveConfiguredHealthUrl()
    {
        var explicitUrl = configuration["GoLive:HealthUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitUrl))
            return NormalizeHealthUrl(explicitUrl);

        var publicBase = configuration["AppUrl:PublicBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(publicBase))
            return NormalizeHealthUrl(publicBase);

        return null;
    }

    private static string? ResolveListeningHealthUrl(WebApplication app)
    {
        var address = app.Urls.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(address))
            return null;

        if (address.Contains(":0", StringComparison.Ordinal))
        {
            var server = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var feature = server?.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            address = feature?.Addresses.FirstOrDefault();
        }

        return string.IsNullOrWhiteSpace(address) ? null : NormalizeHealthUrl(address);
    }

    private static string NormalizeHealthUrl(string baseOrUrl)
    {
        var trimmed = baseOrUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/health", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/health";
    }

    private IReadOnlyList<string> BuildQrPublicUrlWarnings()
    {
        var messages = new List<string>();
        var resolution = appUrlService.DescribeResolution(null);

        foreach (var warning in resolution.Warnings)
            messages.Add($"QR public URL: WARN — {warning}");

        if (InfrastructureEnvironment.IsRenderDeployment
            && string.IsNullOrWhiteSpace(resolution.ConfiguredPublicBaseUrl)
            && string.IsNullOrWhiteSpace(resolution.DatabaseOverride)
            && resolution.Source == AppUrlResolutionSource.RequestHost)
        {
            messages.Add(
                "QR public URL: INFO — Using request-host fallback (override and AppUrl__PublicBaseUrl empty). "
                + $"Sample: {resolution.SampleTableQrUrl("T01")}. "
                + "Set AppUrl__PublicBaseUrl=https://<your-service>.onrender.com for explicit QR hostname.");
        }

        return messages;
    }

    private string? BuildOpenAiWarning()
    {
        if (HasOpenAiConfiguration())
            return null;

        return "OpenAI: WARN — Sommelier:ApiKey not configured (Sommelier:ApiKey or Sommelier__ApiKey). Specialty guided recommendations are unaffected; legacy POST /api/sommelier/suggest falls back to SimulatedSommelierService.";
    }

    private static void PrintHealthSection(GoLiveGateResult healthGate, string? openAiWarning)
    {
        Console.WriteLine("7. Health verification");
        Console.WriteLine($"   Application health: {(healthGate.Passed ? "PASS" : "FAIL")}");
        if (!healthGate.Passed && healthGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {healthGate.FailureReason}");
        Console.WriteLine($"   OpenAI configuration: {(openAiWarning is null ? "present" : "missing (non-blocking)")}");
        if (openAiWarning is not null)
            Console.WriteLine($"   Advisory: {openAiWarning}");
        Console.WriteLine();
    }

    private bool HasOpenAiConfiguration()
    {
        if (!string.IsNullOrWhiteSpace(sommelierOptions.Value.ApiKey))
            return true;
        if (!string.IsNullOrWhiteSpace(configuration["Sommelier:ApiKey"]))
            return true;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Sommelier__ApiKey")))
            return true;

        return false;
    }

    private static void PrintDatabaseSection(
        GoLiveGateResult databaseGate,
        DatabaseStartupHelper.DatabaseTarget dbTarget,
        string environmentName)
    {
        Console.WriteLine("1. Database connectivity");
        Console.WriteLine($"   Database: {dbTarget.Database}");
        Console.WriteLine($"   Environment: {environmentName}");
        Console.WriteLine($"   Target: {dbTarget.Display}");
        Console.WriteLine($"   Status: {(databaseGate.Passed ? "REACHABLE" : "UNREACHABLE")}");
        if (!databaseGate.Passed && databaseGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {databaseGate.FailureReason}");
        Console.WriteLine();
    }

    private static void PrintBootstrapLine(GoLiveGateResult bootstrapGate)
    {
        Console.WriteLine("6. Bootstrap verification");
        Console.WriteLine($"   Specialty bootstrap readiness: {(bootstrapGate.Passed ? "PASS" : "FAIL")}");
        if (!bootstrapGate.Passed && bootstrapGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {bootstrapGate.FailureReason}");
        Console.WriteLine();
    }

    private static void PrintSummary(IReadOnlyList<GoLiveGateResult> gates, bool ready, IReadOnlyList<string> warnings)
    {
        Console.WriteLine("====================================");
        Console.WriteLine("ANNAP GO-LIVE REPORT");
        Console.WriteLine("====================");
        Console.WriteLine();

        foreach (var gate in gates)
        {
            Console.WriteLine($"{FormatGateLabel(gate.Name)} {(gate.Passed ? "PASS" : "FAIL")}");
            if (!gate.Passed && !string.IsNullOrWhiteSpace(gate.FailureReason))
                Console.WriteLine($"  -> {gate.FailureReason}");
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("WARNINGS (non-blocking):");
            foreach (var warning in warnings)
                Console.WriteLine($"  ! {warning}");
        }

        Console.WriteLine();
        Console.WriteLine("FINAL STATUS:");
        Console.WriteLine();
        if (ready)
        {
            Console.WriteLine("READY FOR PRODUCTION TRAFFIC");
        }
        else
        {
            Console.WriteLine("NOT READY FOR PRODUCTION TRAFFIC");
            var reasons = gates
                .Where(g => !g.Passed && !string.IsNullOrWhiteSpace(g.FailureReason))
                .Select(g => $"{g.Name}: {g.FailureReason}")
                .ToList();
            if (reasons.Count > 0)
                Console.WriteLine(string.Join(Environment.NewLine, reasons));
        }
    }

    private static string FormatGateLabel(string name)
    {
        const int width = 22;
        var dots = Math.Max(1, width - name.Length - 1);
        return $"{name} {new string('.', dots)}";
    }

    private static GoLiveGateResult Pass(string name) => new(name, true, null);

    private static GoLiveGateResult Fail(string name, string reason) => new(name, false, reason);

    private static string Condense(string? message)
    {
        var text = (message ?? "").Replace(Environment.NewLine, " ").Trim();
        return text.Length <= 220 ? text : text[..217] + "...";
    }

    private sealed record GoLiveGateResult(string Name, bool Passed, string? FailureReason);

    private sealed record PoolMenuRow(
        Guid Id,
        string Name,
        string? ItemType,
        string? IngredientBreakdown,
        string? FlavorTags,
        string CategoryName,
        bool IsSignature);
}
