using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Backfills menu embeddings after startup so pgvector RAG is warm (non-blocking).</summary>
public sealed class MenuSommelierEmbeddingBootstrapHostedService(
    ILogger<MenuSommelierEmbeddingBootstrapHostedService> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<SommelierOpenAiOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            using var scope = scopeFactory.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<SommelierMenuEmbeddingIndexer>();
            await indexer.EnsureEmbeddingsCurrentAsync(stoppingToken).ConfigureAwait(false);
            logger.LogInformation("Sommelier menu embedding bootstrap finished.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Sommelier menu embedding bootstrap stopped during shutdown.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sommelier menu embedding bootstrap did not complete.");
        }
    }
}
