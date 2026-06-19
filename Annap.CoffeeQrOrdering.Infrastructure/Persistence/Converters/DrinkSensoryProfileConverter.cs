using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Converters;

public sealed class DrinkSensoryProfileConverter : ValueConverter<DrinkSensoryProfile, string>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public DrinkSensoryProfileConverter() : base(
        v => JsonSerializer.Serialize(v, Json),
        v => string.IsNullOrWhiteSpace(v) || v == "{}"
            ? new DrinkSensoryProfile()
            : JsonSerializer.Deserialize<DrinkSensoryProfile>(v, Json) ?? new DrinkSensoryProfile())
    {
    }
}
