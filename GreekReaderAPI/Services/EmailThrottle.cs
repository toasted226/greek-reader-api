using Microsoft.Extensions.Caching.Memory;

namespace GreekReaderAPI.Services;

public sealed class EmailThrottle(IMemoryCache cache) : IEmailThrottle
{
    private const int MaxEmailsPerWindow = 5;
    private const string KeyPrefix = "auth:email-throttle:";

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Lock _gate = new();

    private sealed class State
    {
        public DateTimeOffset LastSent { get; set; } = DateTimeOffset.MinValue;
        public int Count { get; set; }
    }

    public bool TryAcquire(string normalisedAddress)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            var state = cache.GetOrCreate(
                KeyPrefix + normalisedAddress,
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = Window;
                    return new State();
                })!;

            if (now - state.LastSent < Cooldown)
                return false;

            if (state.Count >= MaxEmailsPerWindow)
                return false;

            state.Count++;
            state.LastSent = now;
            return true;
        }
    }
}
