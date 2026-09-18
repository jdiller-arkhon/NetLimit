using System.Collections.ObjectModel;
using System.Windows;
using NetLimiterLite.Core.Model;
using NetLimiterLite.Core.Throttling;

namespace NetLimiterLite.App;

/// <summary>
/// Wires the UI to RuleEngine + RuleStore. Live throughput population and the actual
/// diverter/pipeline startup are left as TODOs here: they depend on WinDivertPacketDiverter,
/// which can only be finished/run on a Windows machine (see docs/WINDOWS_SETUP.md).
/// </summary>
public partial class MainWindow : Window
{
    private readonly RuleEngine _ruleEngine = new();
    private readonly RuleStore _ruleStore = new();
    private readonly GamingPriorityProfile _destiny2Profile = GamingPriorityProfile.Destiny2();
    private PriorityModeController _priorityController;

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _priorityController = new PriorityModeController(_destiny2Profile);
        LoadPersistedRulesIntoEngine();

        // TODO(windows-machine): once WinDivertPacketDiverter is implemented, start a
        // ThrottlePipeline here and populate `Rows` from a periodic snapshot of ProcessFlow
        // aggregates (see NetLimiterLite.Core.Model.ProcessFlow). Each refresh tick should also
        // call RecomputeRules() so Destiny 2 Priority Mode reacts to the game starting/stopping
        // without the user having to click Apply again.
    }

    private void LoadPersistedRulesIntoEngine()
    {
        var rules = _ruleStore.Load();
        _ruleEngine.SetRules(rules);
    }

    /// <summary>
    /// Manual per-row limits always win; Destiny 2 Priority Mode fills in a background cap for
    /// every other currently-active process that doesn't already have a manual limit set.
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        RecomputeRules(persist: true);
    }

    private void RecomputeRules(bool persist)
    {
        var manualRules = Rows
            .Where(r => r.DownloadLimitKBps is not null || r.UploadLimitKBps is not null)
            .Select(r => new BandwidthRule
            {
                ProcessMatch = r.ProcessName,
                MaxBytesPerSecondDown = r.DownloadLimitKBps is { } d ? (long)(d * 1024) : null,
                MaxBytesPerSecondUp = r.UploadLimitKBps is { } u ? (long)(u * 1024) : null,
            })
            .ToList();

        var manuallyRuledNames = manualRules.Select(r => r.ProcessMatch).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var priorityRules = _priorityController
            .BuildRules(Rows.Select(r => r.ProcessName))
            .Where(r => !manuallyRuledNames.Contains(r.ProcessMatch))
            .ToList();

        var allRules = manualRules.Concat(priorityRules).ToList();

        _ruleEngine.SetRules(allRules);
        if (persist) _ruleStore.Save(manualRules); // priority-mode rules are derived, not persisted

        var destinyActive = _priorityController.IsProfileActive(Rows.Select(r => r.ProcessName));
        var suffix = _destiny2Profile.Enabled
            ? destinyActive ? " Destiny 2 detected - background cap active." : " Destiny 2 not running - no background cap applied."
            : string.Empty;
        StatusText.Text = $"Applied {allRules.Count} rule(s) ({manualRules.Count} manual, {priorityRules.Count} from Destiny 2 Priority Mode).{suffix}";
    }

    private void Destiny2PriorityCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _destiny2Profile.Enabled = Destiny2PriorityCheckBox.IsChecked == true;

        if (double.TryParse(Destiny2DownCapTextBox.Text, out var downKBps))
            _destiny2Profile.BackgroundCapBytesPerSecondDown = (long)(downKBps * 1024);
        if (double.TryParse(Destiny2UpCapTextBox.Text, out var upKBps))
            _destiny2Profile.BackgroundCapBytesPerSecondUp = (long)(upKBps * 1024);

        RecomputeRules(persist: false);
    }

    private bool _paused;

    private void PauseAllButton_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseAllButton.Content = _paused ? "Resume Throttling" : "Pause All Throttling";
        // TODO(windows-machine): wire this into ThrottlePipeline once it exists - e.g. a
        // pipeline.SetPaused(bool) that makes HandlePacket always reinject immediately.
        StatusText.Text = _paused ? "Throttling paused - all traffic passes through." : "Throttling active.";
    }
}
