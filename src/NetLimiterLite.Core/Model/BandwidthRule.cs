namespace NetLimiterLite.Core.Model;

/// <summary>
/// A user-configured cap for a process, matched by executable name (case-insensitive) or full
/// path. Rates are in bytes/sec; null means "unlimited" for that direction.
/// </summary>
public sealed class BandwidthRule
{
    public required string ProcessMatch { get; init; }
    public bool MatchFullPath { get; init; }

    public long? MaxBytesPerSecondDown { get; set; }
    public long? MaxBytesPerSecondUp { get; set; }

    public bool Enabled { get; set; } = true;

    public bool Matches(string processName, string? executablePath)
    {
        if (!Enabled) return false;

        var candidate = MatchFullPath ? executablePath : processName;
        if (candidate is null) return false;

        return string.Equals(candidate, ProcessMatch, StringComparison.OrdinalIgnoreCase);
    }
}
