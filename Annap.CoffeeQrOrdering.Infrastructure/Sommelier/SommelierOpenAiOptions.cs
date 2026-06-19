namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Configuration for OpenAI-backed RAG sommelier (concise, grounded on menu only).</summary>
public sealed class SommelierOpenAiOptions
{
    /// <summary>OpenAI API key. If empty, the simulated sommelier is used.</summary>
    public string ApiKey { get; set; } = "";

    public string ChatModel { get; set; } = "gpt-4o-mini";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>How many menu rows to retrieve via pgvector for grounding.</summary>
    public int MaxRetrievedMenuItems { get; set; } = 6;

    public int MaxOutputTokens { get; set; } = 360;

    /// <summary>Per-request timeout for OpenAI calls (embedding + chat).</summary>
    public int RequestTimeoutSeconds { get; set; } = 16;

    public int MaxRetries { get; set; } = 2;

    public int RetryBaseDelayMilliseconds { get; set; } = 450;

    public int CacheDurationMinutes { get; set; } = 25;

    /// <summary>Items per OpenAI embeddings API call.</summary>
    public int EmbeddingBatchSize { get; set; } = 16;
}
