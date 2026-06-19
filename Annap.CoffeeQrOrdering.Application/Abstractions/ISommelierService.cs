using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Application.Abstractions;

public interface ISommelierService
{
    /// <summary>Curated pairing note: OpenAI RAG when configured, otherwise deterministic house pairings.</summary>
    Task<SommelierSuggestion> SuggestAsync(SommelierGuideRequest request, CancellationToken cancellationToken = default);
}
