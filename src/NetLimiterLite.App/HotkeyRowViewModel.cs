using System.ComponentModel;
using System.Runtime.CompilerServices;
using NetLimiterLite.Core.Model;

namespace NetLimiterLite.App;

public sealed class HotkeyRowViewModel : INotifyPropertyChanged
{
    public required HotkeyBinding Binding { get; init; }

    public string ActionLabel => Binding.Action switch
    {
        HotkeyAction.ToggleDestiny2PriorityMode => "Toggle Destiny 2 Priority Mode",
        HotkeyAction.ToggleThrottlingPause => "Pause / Resume All Throttling",
        HotkeyAction.ApplyLimits => "Re-apply Current Limits",
        _ => Binding.Action.ToString(),
    };

    private string _currentKeys = string.Empty;
    public string CurrentKeys
    {
        get => _currentKeys;
        set => SetField(ref _currentKeys, value);
    }

    public void RefreshFromBinding() => CurrentKeys = Binding.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
