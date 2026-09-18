using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetLimiterLite.App;

/// <summary>
/// One row in the main window's process list: live throughput plus editable rule fields.
/// KB/s in the UI, converted to bytes/sec when written into a BandwidthRule.
/// </summary>
public sealed class ProcessRowViewModel : INotifyPropertyChanged
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; }

    private double _downKbps;
    public double DownloadKBps
    {
        get => _downKbps;
        set => SetField(ref _downKbps, value);
    }

    private double _upKbps;
    public double UploadKBps
    {
        get => _upKbps;
        set => SetField(ref _upKbps, value);
    }

    private double? _downLimitKBps;
    public double? DownloadLimitKBps
    {
        get => _downLimitKBps;
        set => SetField(ref _downLimitKBps, value);
    }

    private double? _upLimitKBps;
    public double? UploadLimitKBps
    {
        get => _upLimitKBps;
        set => SetField(ref _upLimitKBps, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
