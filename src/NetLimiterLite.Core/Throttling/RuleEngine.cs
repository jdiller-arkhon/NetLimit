using NetLimiterLite.Core.Model;

namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Holds the active rule set and hands out per-(PID, direction) token buckets, creating or
/// re-configuring them as rules change or as new processes are observed.
/// </summary>
public sealed class RuleEngine
{
    private readonly object _lock = new();
    private readonly List<BandwidthRule> _rules = new();
    private readonly Dictionary<(int pid, bool isDownload), TokenBucket> _buckets = new();

    public IReadOnlyList<BandwidthRule> Rules
    {
        get { lock (_lock) return _rules.ToList(); }
    }

    public void SetRules(IEnumerable<BandwidthRule> rules)
    {
        lock (_lock)
        {
            _rules.Clear();
            _rules.AddRange(rules);
            // Existing buckets will pick up new rates lazily via GetBucket's re-sync below.
        }
    }

    public void AddOrUpdateRule(BandwidthRule rule)
    {
        lock (_lock)
        {
            var existing = _rules.FirstOrDefault(r =>
                string.Equals(r.ProcessMatch, rule.ProcessMatch, StringComparison.OrdinalIgnoreCase)
                && r.MatchFullPath == rule.MatchFullPath);
            if (existing is not null) _rules.Remove(existing);
            _rules.Add(rule);
        }
    }

    private BandwidthRule? FindRule(string processName, string? executablePath)
    {
        lock (_lock)
        {
            return _rules.FirstOrDefault(r => r.Matches(processName, executablePath));
        }
    }

    /// <summary>
    /// Returns the token bucket that should gate a packet for the given process/direction, or
    /// null if no rule applies (i.e. unlimited / not throttled).
    /// </summary>
    public TokenBucket? GetBucket(int pid, string processName, string? executablePath, bool isDownload)
    {
        var rule = FindRule(processName, executablePath);
        var capBytesPerSec = rule is null
            ? (long?)null
            : (isDownload ? rule.MaxBytesPerSecondDown : rule.MaxBytesPerSecondUp);

        var key = (pid, isDownload);

        lock (_lock)
        {
            if (capBytesPerSec is null)
            {
                _buckets.Remove(key);
                return null;
            }

            if (_buckets.TryGetValue(key, out var bucket))
            {
                bucket.SetRate(capBytesPerSec.Value);
                return bucket;
            }

            var newBucket = new TokenBucket(capBytesPerSec.Value);
            _buckets[key] = newBucket;
            return newBucket;
        }
    }

    public void ForgetProcess(int pid)
    {
        lock (_lock)
        {
            _buckets.Remove((pid, true));
            _buckets.Remove((pid, false));
        }
    }
}
