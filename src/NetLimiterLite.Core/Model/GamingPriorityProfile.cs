namespace NetLimiterLite.Core.Model;

/// <summary>
/// A "give this game priority" profile: while any of <see cref="PriorityProcessNames"/> is
/// running, every *other* process gets capped at <see cref="BackgroundCapBytesPerSecondDown"/>/
/// Up, so the game's traffic effectively gets the rest of the pipe. The priority process itself
/// is never throttled by this profile.
/// </summary>
public sealed class GamingPriorityProfile
{
    public required string Name { get; init; }
    public bool Enabled { get; set; }

    public required IReadOnlyList<string> PriorityProcessNames { get; init; }

    /// <summary>Cap applied to every non-priority process while a priority process is active. Null = unlimited (profile has no effect beyond identifying the priority process).</summary>
    public long? BackgroundCapBytesPerSecondDown { get; set; }
    public long? BackgroundCapBytesPerSecondUp { get; set; }

    /// <summary>
    /// Destiny 2's PC processes: the game itself and the Bungie launcher that stays running and
    /// can otherwise compete for bandwidth (patch checks, friends list, etc.) while you play.
    /// </summary>
    public static GamingPriorityProfile Destiny2(long backgroundCapDownKBps = 200, long backgroundCapUpKBps = 50) => new()
    {
        Name = "Destiny 2",
        PriorityProcessNames = new[] { "destiny2.exe", "bungielauncher.exe" },
        BackgroundCapBytesPerSecondDown = backgroundCapDownKBps * 1024,
        BackgroundCapBytesPerSecondUp = backgroundCapUpKBps * 1024,
    };

    public bool IsPriorityProcess(string processName)
        => PriorityProcessNames.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
}
