using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NetLimiterLite.Core.Model;

namespace NetLimiterLite.App.Hotkeys;

/// <summary>
/// Registers Windows global hotkeys (RegisterHotKey) against the main window's message loop, so
/// bindings work even while a game has focus. Raises <see cref="ActionTriggered"/> with the
/// HotkeyAction to run - MainWindow decides what each action actually does.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    private readonly Window _window;
    private HwndSource? _source;
    private readonly Dictionary<int, HotkeyAction> _idToAction = new();
    private int _nextId = 0xA000; // arbitrary range unlikely to collide with other app hotkeys

    public event Action<HotkeyAction>? ActionTriggered;

    public GlobalHotkeyManager(Window window)
    {
        _window = window;
    }

    public void RegisterAll(IEnumerable<HotkeyBinding> bindings)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);

        UnregisterAll();

        foreach (var binding in bindings.Where(b => b.Enabled))
        {
            var id = _nextId++;
            var modFlags = ToNativeModifiers(binding.Modifiers);

            if (RegisterHotKey(handle, id, modFlags, (uint)binding.VirtualKeyCode))
            {
                _idToAction[id] = binding.Action;
            }
            else
            {
                // Another app already owns this combo. Surface via MainWindow's status text
                // rather than throwing - one bad binding shouldn't break the rest.
                RegistrationFailed?.Invoke(binding);
            }
        }
    }

    public event Action<HotkeyBinding>? RegistrationFailed;

    public void UnregisterAll()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        foreach (var id in _idToAction.Keys.ToList())
        {
            UnregisterHotKey(handle, id);
        }
        _idToAction.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _idToAction.TryGetValue(wParam.ToInt32(), out var action))
        {
            ActionTriggered?.Invoke(action);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

        uint flags = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) flags |= MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) flags |= MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) flags |= MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) flags |= MOD_WIN;
        return flags;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
