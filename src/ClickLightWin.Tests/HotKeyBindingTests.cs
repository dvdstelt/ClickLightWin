using System.Text.Json;
using System.Windows.Input;
using ClickLightWin.Interop;

namespace ClickLightWin.Tests;

public class HotKeyBindingTests
{
    [Fact]
    public void Display_formats_modifiers_and_key()
    {
        var b = new HotKeyBinding(ModifierKeys.Control | ModifierKeys.Shift, Key.L);
        Assert.Equal("Ctrl+Shift+L", b.Display);
    }

    [Fact]
    public void Display_orders_ctrl_alt_shift_win_and_names_digits_and_fkeys()
    {
        Assert.Equal("Ctrl+Alt+5", new HotKeyBinding(ModifierKeys.Control | ModifierKeys.Alt, Key.D5).Display);
        Assert.Equal("Ctrl+F5", new HotKeyBinding(ModifierKeys.Control, Key.F5).Display);
        Assert.Equal("Win+D", new HotKeyBinding(ModifierKeys.Windows, Key.D).Display);
    }

    [Fact]
    public void Win32_modifiers_map_and_always_include_no_repeat()
    {
        var b = new HotKeyBinding(ModifierKeys.Control | ModifierKeys.Shift, Key.L);
        var expected = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        Assert.Equal(expected, b.Win32Modifiers);
    }

    [Fact]
    public void VirtualKey_matches_the_win32_code()
    {
        Assert.Equal(0x4Cu, new HotKeyBinding(ModifierKeys.Control, Key.L).VirtualKey); // 'L'
    }

    [Fact]
    public void Requires_a_key_and_a_non_shift_modifier_to_be_valid()
    {
        Assert.True(new HotKeyBinding(ModifierKeys.Control, Key.L).IsValid);
        Assert.False(new HotKeyBinding(ModifierKeys.Shift, Key.L).IsValid);   // Shift alone is typing
        Assert.False(new HotKeyBinding(ModifierKeys.Control, Key.None).IsValid);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var b = new HotKeyBinding(ModifierKeys.Control | ModifierKeys.Alt, Key.K);
        var restored = JsonSerializer.Deserialize<HotKeyBinding>(JsonSerializer.Serialize(b));
        Assert.Equal(b, restored);
    }
}
