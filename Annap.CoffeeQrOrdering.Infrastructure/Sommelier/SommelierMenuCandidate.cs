using Annap.CoffeeQrOrdering.Domain.ValueObjects;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Grounding row retrieved from PostgreSQL + pgvector.</summary>
public sealed record SommelierMenuCandidate(
    Guid Id,
    string Name,
    string? TastingNotes,
    string? MoodProfile,
    decimal Price,
    string CategoryName,
    DrinkSensoryProfile? SensoryProfile,
    int? CaffeineLevel,
    int? SweetnessLevel,
    int? AcidityLevel)
{
    public DrinkSensoryProfile EffectiveSensory =>
        (SensoryProfile ?? new DrinkSensoryProfile()).MergeWithLegacyLevels(CaffeineLevel, SweetnessLevel, AcidityLevel);

    public string SensoryLineForModel() => EffectiveSensory.ToSommelierLine();
}
