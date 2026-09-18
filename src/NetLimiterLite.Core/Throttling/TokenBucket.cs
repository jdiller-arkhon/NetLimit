namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Classic token-bucket rate limiter. Tokens are bytes; capacity allows short bursts
/// (e.g. one full MTU-sized packet) without stalling small flows.
/// </summary>
public sealed class TokenBucket
{
    private readonly object _lock = new();
    private double _tokens;
    private long _lastRefillTicks;

    public double RatePerSecond { get; private set; }
    public double CapacityBytes { get; private set; }

    public TokenBucket(double ratePerSecond, double? capacityBytes = null)
    {
        SetRate(ratePerSecond, capacityBytes);
        _tokens = CapacityBytes;
        _lastRefillTicks = DateTime.UtcNow.Ticks;
    }

    public void SetRate(double ratePerSecond, double? capacityBytes = null)
    {
        lock (_lock)
        {
            RatePerSecond = Math.Max(0, ratePerSecond);
            // Default burst capacity: 100ms worth of traffic, floor of 1500 bytes (~1 MTU).
            CapacityBytes = capacityBytes ?? Math.Max(1500, RatePerSecond * 0.1);
            _tokens = Math.Min(_tokens, CapacityBytes);
        }
    }

    /// <summary>
    /// Attempts to consume <paramref name="bytes"/> tokens. Returns true if allowed immediately.
    /// If false, <paramref name="retryAfter"/> gives the estimated wait before enough tokens
    /// will be available (caller should queue/delay the packet, not drop it).
    /// </summary>
    public bool TryConsume(long bytes, out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            Refill();

            if (RatePerSecond <= 0)
            {
                // Rate of 0 means "fully paused" - never allow, retry check periodically.
                retryAfter = TimeSpan.FromMilliseconds(250);
                return false;
            }

            if (_tokens >= bytes)
            {
                _tokens -= bytes;
                retryAfter = TimeSpan.Zero;
                return true;
            }

            var deficit = bytes - _tokens;
            retryAfter = TimeSpan.FromSeconds(deficit / RatePerSecond);
            return false;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow.Ticks;
        var elapsedSeconds = (now - _lastRefillTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsedSeconds <= 0) return;

        _tokens = Math.Min(CapacityBytes, _tokens + elapsedSeconds * RatePerSecond);
        _lastRefillTicks = now;
    }
}
