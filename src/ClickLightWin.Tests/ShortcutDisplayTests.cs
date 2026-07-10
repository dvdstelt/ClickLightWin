namespace ClickLightWin.Tests;

public class ShortcutDisplayTests
{
    private const int VkC = 0x43;
    private const int VkP = 0x50;
    private const int VkF5 = 0x74;
    private const int VkLeftArrow = 0x25;

    [Fact]
    public void Ctrl_combo_is_shown()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(VkC, ctrl: true, alt: false, shift: false, win: false, rightAlt: false);
        Assert.Equal(new[] { "Ctrl", "C" }, keys);
    }

    [Fact]
    public void Modifiers_are_ordered_ctrl_alt_shift_win()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(VkP, ctrl: true, alt: true, shift: true, win: true, rightAlt: false);
        Assert.Equal(new[] { "Ctrl", "Alt", "Shift", "Win", "P" }, keys);
    }

    [Fact]
    public void Plain_typing_is_never_shown()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(VkC, ctrl: false, alt: false, shift: false, win: false, rightAlt: false);
        Assert.Null(keys);
    }

    [Fact]
    public void Shift_only_is_plain_typing_and_never_shown()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(VkC, ctrl: false, alt: false, shift: true, win: false, rightAlt: false);
        Assert.Null(keys);
    }

    [Fact]
    public void AltGr_typing_is_never_shown()
    {
        // AltGr (right Alt) arrives as Ctrl+Alt on international layouts; typing
        // accents (e.g. AltGr+A on US International) must not produce a pill.
        var keys = LowLevelKeyboardHook.BuildShortcut(0x41, ctrl: true, alt: true, shift: false, win: false, rightAlt: true);
        Assert.Null(keys);
    }

    [Fact]
    public void Win_combo_is_shown()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(0x44, ctrl: false, alt: false, shift: false, win: true, rightAlt: false);
        Assert.Equal(new[] { "Win", "D" }, keys);
    }

    [Fact]
    public void Unmapped_keys_are_not_shown_even_with_modifiers()
    {
        var keys = LowLevelKeyboardHook.BuildShortcut(0xB3, ctrl: true, alt: false, shift: false, win: false, rightAlt: false); // media key
        Assert.Null(keys);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x35, "5")]
    [InlineData(VkF5, "F5")]
    [InlineData(VkLeftArrow, "←")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x2E, "Del")]
    [InlineData(0x60, "Num0")]
    public void Key_names_are_readable(int vk, string expected) =>
        Assert.Equal(expected, LowLevelKeyboardHook.KeyName(vk));
}
