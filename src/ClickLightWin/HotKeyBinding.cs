using System.Text.Json.Serialization;
using System.Windows.Input;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// A user-configurable global hotkey: a set of modifiers plus a key. Stored in
/// WPF terms (so the recorder and display are simple) and converted to Win32
/// modifiers/virtual-key for RegisterHotKey. Maps to HotKeyBinding.swift.
/// </summary>
public sealed record HotKeyBinding(ModifierKeys Modifiers, Key Key)
{
    /// <summary>A hotkey needs a key and at least one of Ctrl/Alt/Win to be a sane global shortcut.</summary>
    [JsonIgnore]
    public bool IsValid => Key != Key.None
        && (Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;

    [JsonIgnore]
    public uint Win32Modifiers
    {
        get
        {
            var m = NativeMethods.MOD_NOREPEAT;
            if (Modifiers.HasFlag(ModifierKeys.Control)) m |= NativeMethods.MOD_CONTROL;
            if (Modifiers.HasFlag(ModifierKeys.Alt)) m |= NativeMethods.MOD_ALT;
            if (Modifiers.HasFlag(ModifierKeys.Shift)) m |= NativeMethods.MOD_SHIFT;
            if (Modifiers.HasFlag(ModifierKeys.Windows)) m |= NativeMethods.MOD_WIN;
            return m;
        }
    }

    [JsonIgnore] public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>Human-readable form, e.g. "Ctrl+Shift+L".</summary>
    [JsonIgnore]
    public string Display
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyName(Key));
            return string.Join("+", parts);
        }
    }

    /// <summary>The out-of-the-box binding for each action (also used by the reset buttons).</summary>
    public static readonly HotKeyBinding DefaultToggle = new(ModifierKeys.Control | ModifierKeys.Shift, Key.L);
    public static readonly HotKeyBinding DefaultClear = new(ModifierKeys.Control | ModifierKeys.Shift, Key.C);
    public static readonly HotKeyBinding DefaultDrawMode = new(ModifierKeys.Control | ModifierKeys.Shift, Key.D);

    private static string KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => key.ToString()[1..],       // "D5" -> "5"
        >= Key.NumPad0 and <= Key.NumPad9 => "Num" + key.ToString()[6..],
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.None => "?",
        _ => key.ToString()
    };
}
