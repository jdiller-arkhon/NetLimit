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

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        LoadPersistedRulesIntoEngine();

        // TODO(windows-machine): once WinDivertPacketDiverter is implemented, start a
        // ThrottlePipeline here and populate `Rows` from a periodic snapshot of ProcessFlow
        // aggregates (see NetLimiterLite.Core.Model.ProcessFlow). For now the grid starts empty;
        // this window is a working shell for the rule-editing UX.
    }

    private void LoadPersistedRulesIntoEngine()
    {
        var rules = _ruleStore.Load();
        _ruleEngine.SetRules(rules);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var rules = Rows
            .Where(r => r.DownloadLimitKBps is not null || r.UploadLimitKBps is not null)
            .Select(r => new BandwidthRule
            {
                ProcessMatch = r.ProcessName,
                MaxBytesPerSecondDown = r.DownloadLimitKBps is { } d ? (long)(d * 1024) : null,
                MaxBytesPerSecondUp = r.UploadLimitKBps is { } u ? (long)(u * 1024) : null,
            })
            .ToList();

        _ruleEngine.SetRules(rules);
        _ruleStore.Save(rules);
        StatusText.Text = $"Applied {rules.Count} rule(s) and saved to {_ruleStore.FilePath}";
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
