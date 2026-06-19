using Annap.CoffeeQrOrdering.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

public sealed class SommelierSessionOptions
{
    public int SlidingExpirationMinutes { get; set; } = 240;
}

/// <summary>Lightweight in-memory sommelier trajectory for one browser/table sitting.</summary>
public sealed class SommelierSessionState
{
    public Guid SessionId { get; init; }
    public DateTimeOffset LastTouchedUtc { get; set; }

    public Guid? PreviousLeadMenuItemId { get; set; }
    public string? PreviousLeadName { get; set; }

    /// <summary>Stable mood keys in visit order (e.g. bright, focus).</summary>
    public List<string> MoodKeys { get; } = [];

    /// <summary>Human mood labels for copy.</summary>
    public List<string> MoodLabels { get; } = [];

    /// <summary>Refinement chip ids in order (softer, brighter, …).</summary>
    public List<string> RefinementKeys { get; } = [];

    /// <summary>Short heuristic line describing cumulative drift.</summary>
    public string? FlavorDirection { get; set; }

    /// <summary>Last guest UI language (en/vi) for continuity and sommelier output.</summary>
    public string PreferredLanguage { get; set; } = GuestOutputLanguage.English;
}

public interface ISommelierSessionMemory
{
    SommelierSessionState GetOrCreate(Guid sessionId);
    void Save(SommelierSessionState state);
}

public sealed class SommelierSessionMemoryService(IMemoryCache cache, IOptions<SommelierSessionOptions> options)
    : ISommelierSessionMemory
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(Math.Clamp(options.Value.SlidingExpirationMinutes, 30, 720));

    private static string Key(Guid id) => "sommelier_session:" + id;

    public SommelierSessionState GetOrCreate(Guid sessionId)
    {
        if (cache.TryGetValue(Key(sessionId), out SommelierSessionState? hit) && hit is not null)
        {
            hit.LastTouchedUtc = DateTimeOffset.UtcNow;
            return hit;
        }

        var created = new SommelierSessionState { SessionId = sessionId, LastTouchedUtc = DateTimeOffset.UtcNow };
        Save(created);
        return created;
    }

    public void Save(SommelierSessionState state)
    {
        state.LastTouchedUtc = DateTimeOffset.UtcNow;
        cache.Set(
            Key(state.SessionId),
            state,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _ttl
            });
    }
}
