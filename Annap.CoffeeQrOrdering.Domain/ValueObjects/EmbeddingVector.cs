namespace Annap.CoffeeQrOrdering.Domain.ValueObjects;

public sealed record EmbeddingVector
{
    public EmbeddingVector(float[] values)
    {
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public float[] Values { get; }
    public int Dimensions => Values.Length;
}

