using NetLimiterLite.Core.Model;

namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Turns a GamingPriorityProfile plus a snapshot of currently-active processes into a concrete
/// rule set: cap everything except the priority process(es), and only while at least one of them
/// is actually running (so the cap isn't left on in the background when you're not playing).
/// </summary>
public sealed class PriorityModeController
{
    private readonly GamingPriorityProfile _profile;

    public PriorityModeController(GamingPriorityProfile profile)
    {
        _profile = profile;
    }

    public bool IsProfileActive(IEnumerable<string> activeProcessNames)
        => _profile.Enabled && activeProcessNames.Any(_profile.IsPriorityProcess);

    /// <summary>
    /// Computes the rules this profile wants applied, given the set of process names currently
    /// observed with network activity. Returns an empty list if the profile is disabled or none
    /// of its priority processes are currently running (no reason to cap anything).
    /// </summary>
    public IReadOnlyList<BandwidthRule> BuildRules(IEnumerable<string> activeProcessNames)
    {
        var active = activeProcessNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (!IsProfileActive(active)) return Array.Empty<BandwidthRule>();

        return active
            .Where(name => !_profile.IsPriorityProcess(name))
            .Select(name => new BandwidthRule
            {
                ProcessMatch = name,
                MaxBytesPerSecondDown = _profile.BackgroundCapBytesPerSecondDown,
                MaxBytesPerSecondUp = _profile.BackgroundCapBytesPerSecondUp,
            })
            .ToList();
    }
}
