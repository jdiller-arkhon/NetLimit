using NetLimiterLite.Core.Model;
using NetLimiterLite.Core.Throttling;
using Xunit;

namespace NetLimiterLite.Tests;

public class HotkeyStoreTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"netlimiterlite-hotkeys-{Guid.NewGuid():N}.json");

    [Fact]
    public void LoadReturnsDefaultsWhenFileMissing()
    {
        var store = new HotkeyStore(_tempFile);

        var bindings = store.Load();

        Assert.Equal(3, bindings.Count);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.ToggleDestiny2PriorityMode);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.ToggleThrottlingPause);
        Assert.Contains(bindings, b => b.Action == HotkeyAction.ApplyLimits);
    }

    [Fact]
    public void SaveThenLoadRoundTripsCustomBinding()
    {
        var store = new HotkeyStore(_tempFile);
        var custom = new List<HotkeyBinding>
        {
            new() { Action = HotkeyAction.ToggleDestiny2PriorityMode, Modifiers = HotkeyModifiers.Shift, VirtualKeyCode = 0x46 }, // Shift+F
        };

        store.Save(custom);
        var loaded = store.Load();

        var binding = Assert.Single(loaded);
        Assert.Equal(HotkeyAction.ToggleDestiny2PriorityMode, binding.Action);
        Assert.Equal(HotkeyModifiers.Shift, binding.Modifiers);
        Assert.Equal(0x46, binding.VirtualKeyCode);
    }

    [Fact]
    public void ToStringFormatsModifiersAndKeyReadably()
    {
        var binding = new HotkeyBinding
        {
            Action = HotkeyAction.ApplyLimits,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
            VirtualKeyCode = 0x41, // 'A'
        };

        Assert.Equal("Ctrl+Alt+A", binding.ToString());
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
