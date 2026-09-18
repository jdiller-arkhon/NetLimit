namespace NetLimiterLite.Core.Model;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Actions a global hotkey can trigger. Kept here (rather than in the App project) so the
/// binding data model and its persistence stay platform-agnostic and testable; only the actual
/// OS-level key registration (RegisterHotKey) is Windows-specific and lives in
/// NetLimiterLite.App/Hotkeys.
/// </summary>
public enum HotkeyAction
{
    ToggleDestiny2PriorityMode,
    ToggleThrottlingPause,
    ApplyLimits,
}

/// <summary>
/// One global hotkey: a modifier combo + a virtual-key code (Windows VK_* value) mapped to an
/// action. VirtualKeyCode is stored as a plain int rather than System.Windows.Input.Key so this
/// type has no WPF/Windows dependency.
/// </summary>
public sealed class HotkeyBinding
{
    public required HotkeyAction Action { get; init; }
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    public required int VirtualKeyCode { get; set; }
    public bool Enabled { get; set; } = true;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(VirtualKeyDisplayName(VirtualKeyCode));
        return string.Join("+", parts);
    }

    private static string VirtualKeyDisplayName(int vk) =>
        vk switch
        {
            >= 0x30 and <= 0x39 => ((char)vk).ToString(), // '0'-'9'
            >= 0x41 and <= 0x5A => ((char)vk).ToString(), // 'A'-'Z'
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",        // F1-F24
            _ => $"VK_{vk:X2}",
        };
}
