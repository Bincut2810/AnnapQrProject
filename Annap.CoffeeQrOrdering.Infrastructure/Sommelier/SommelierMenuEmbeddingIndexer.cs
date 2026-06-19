using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Computes and stores OpenAI embeddings for menu items (pgvector RAG).</summary>
public sealed class SommelierMenuEmbeddingIndexer(
    AppDbContext db,
    IOptions<SommelierOpenAiOptions> options,
    ILogger<SommelierMenuEmbeddingIndexer> logger)
{
    private readonly SommelierOpenAiOptions _opts = options.Value;

    public async Task EnsureEmbeddingsCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            return;

        var model = _opts.EmbeddingModel.Trim();
        if (model.Length == 0)
            return;

        var staleIds = await db.MenuItems
            .AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived && (m.Embedding == null || m.EmbeddingModel != model))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (staleIds.Count == 0)
            return;

        logger.LogInformation("Sommelier embedding indexer: updating {Count} menu rows for model {Model}.", staleIds.Count, model);

        var batchSize = Math.Clamp(_opts.EmbeddingBatchSize, 1, 64);
        var credential = new ApiKeyCredential(_opts.ApiKey);
        var openAiOptions = new OpenAIClientOptions();
        var embeddingClient = new EmbeddingClient(model, credential, openAiOptions);

        for (var offset = 0; offset < staleIds.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = staleIds.Skip(offset).Take(batchSize).ToList();
            var items = await db.MenuItems
                .Include(m => m.Category)
                .Where(m => slice.Contains(m.Id))
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
                continue;

            var order = slice.Select((id, idx) => (id, idx)).ToDictionary(t => t.id, t => t.idx);
            items.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));

            var texts = items.Select(BuildEmbeddingDocument).ToList();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(20, _opts.RequestTimeoutSeconds * 2)));

            ClientResult<OpenAIEmbeddingCollection> response;
            try
            {
                response = await embeddingClient.GenerateEmbeddingsAsync(texts, cancellationToken: timeoutCts.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OpenAI embeddings batch failed at offset {Offset}.", offset);
                continue;
            }

            var embeddings = response.Value.ToArray();
            if (embeddings.Length != items.Count)
            {
                logger.LogWarning(
                    "Embedding batch size mismatch (expected {Expected}, got {Actual}).",
                    items.Count,
                    embeddings.Length);
                continue;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var floats = embeddings[i].ToFloats().ToArray();
                items[i].Embedding = new EmbeddingVector(floats);
                items[i].EmbeddingModel = model;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    internal static string BuildEmbeddingDocument(MenuItem m)
    {
        var category = m.Category?.Name ?? "";
        var parts = new List<string> { m.Name.Trim(), category };
        if (!string.IsNullOrWhiteSpace(m.MoodProfile))
            parts.Add(m.MoodProfile.Trim());
        if (!string.IsNullOrWhiteSpace(m.TastingNotes))
            parts.Add(Truncate(m.TastingNotes, 420));
        else if (!string.IsNullOrWhiteSpace(m.Description))
            parts.Add(Truncate(m.Description, 420));
        if (m.CaffeineLevel is int c)
            parts.Add($"caffeine {c}/5");
        if (m.SweetnessLevel is int s)
            parts.Add($"sweetness {s}/5");
        if (m.AcidityLevel is int a)
            parts.Add($"acidity {a}/5");
        var merged = m.SensoryProfile.MergeWithLegacyLevels(m.CaffeineLevel, m.SweetnessLevel, m.AcidityLevel);
        var sensoryNarrative = merged.ToEmbeddingNarrative();
        if (sensoryNarrative.Length > 28)
            parts.Add(sensoryNarrative);
        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }

    private static string Truncate(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }
}
