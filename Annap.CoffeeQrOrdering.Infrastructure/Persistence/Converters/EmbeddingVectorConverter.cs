using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Converters;

public sealed class EmbeddingVectorConverter : ValueConverter<EmbeddingVector?, Vector?>
{
    public EmbeddingVectorConverter()
        : base(
            v => v == null ? null : new Vector(v.Values),
            v => v == null ? null : new EmbeddingVector(v.ToArray()))
    {
    }
}

