using System.Text.Json;
using System.Text.Json.Serialization;
using NetLimiterLite.Core.Model;

namespace NetLimiterLite.Core.Throttling;

/// <summary>
/// Loads/saves global hotkey bindings as JSON, defaulting to
/// %APPDATA%/NetLimiterLite/hotkeys.json on Windows.
/// </summary>
public sealed class HotkeyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; }

    public HotkeyStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    private static string DefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "NetLimiterLite", "hotkeys.json");
    }

    /// <summary>
    /// Default bindings shipped out of the box: Ctrl+Alt+D toggles Destiny 2 Priority Mode,
    /// Ctrl+Alt+P pauses/resumes all throttling, Ctrl+Alt+A re-applies the current limits.
    /// Virtual-key codes are plain Windows VK_* values so this file has no WPF dependency.
    /// </summary>
    public static List<HotkeyBinding> Defaults() => new()
    {
        new HotkeyBinding { Action = HotkeyAction.ToggleDestiny2PriorityMode, VirtualKeyCode = 0x44 }, // 'D'
        new HotkeyBinding { Action = HotkeyAction.ToggleThrottlingPause, VirtualKeyCode = 0x50 },      // 'P'
        new HotkeyBinding { Action = HotkeyAction.ApplyLimits, VirtualKeyCode = 0x41 },                // 'A'
    };

    public List<HotkeyBinding> Load()
    {
        if (!File.Exists(FilePath)) return Defaults();

        var json = File.ReadAllText(FilePath);
        var loaded = JsonSerializer.Deserialize<List<HotkeyBinding>>(json, JsonOptions);
        return loaded is { Count: > 0 } ? loaded : Defaults();
    }

    public void Save(IEnumerable<HotkeyBinding> bindings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(bindings.ToList(), JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
