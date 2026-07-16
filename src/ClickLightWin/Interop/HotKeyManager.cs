using System.Windows.Interop;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// Registers the configurable global hotkeys (toggle, clear annotations, drawing
/// mode) on a message-only window and raises an event when one fires. Bindings can
/// be reconfigured at runtime, and suspended while the settings window is open so
/// they neither fire nor collide with the shortcut recorder. Maps to HotKeyManager.swift.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int ToggleHotKeyId = 1;
    private const int ClearHotKeyId = 2;
    private const int DrawModeHotKeyId = 3;
    private const int ShortcutsHotKeyId = 4;

    private HwndSource? _source;
    private HotKeyBinding? _toggle, _clear, _drawMode, _shortcuts;

    public event Action? TogglePressed;
    public event Action? ClearPressed;
    public event Action? DrawModePressed;
    public event Action? ShortcutsPressed;

    /// <summary>False when the binding is invalid or another application already owns it.</summary>
    public bool ToggleRegistered { get; private set; }
    public bool ClearRegistered { get; private set; }
    public bool DrawModeRegistered { get; private set; }
    public bool ShortcutsRegistered { get; private set; }

    /// <summary>Create the message-only window that receives WM_HOTKEY.</summary>
    public void Start()
    {
        if (_source is not null) return;
        var parameters = new HwndSourceParameters("ClickLightWinHotKeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Apply a new set of bindings and (re)register them.</summary>
    public void Configure(HotKeyBinding toggle, HotKeyBinding clear, HotKeyBinding drawMode, HotKeyBinding shortcuts)
    {
        _toggle = toggle;
        _clear = clear;
        _drawMode = drawMode;
        _shortcuts = shortcuts;
        Reregister();
    }

    /// <summary>Unregister everything (e.g. while the settings window records a new combo).</summary>
    public void Suspend() => UnregisterAll();

    private void Reregister()
    {
        UnregisterAll();
        if (_source is null) return;
        ToggleRegistered = TryRegister(ToggleHotKeyId, _toggle);
        ClearRegistered = TryRegister(ClearHotKeyId, _clear);
        DrawModeRegistered = TryRegister(DrawModeHotKeyId, _drawMode);
        ShortcutsRegistered = TryRegister(ShortcutsHotKeyId, _shortcuts);
    }

    private bool TryRegister(int id, HotKeyBinding? binding) =>
        binding is { IsValid: true }
        && NativeMethods.RegisterHotKey(_source!.Handle, id, binding.Win32Modifiers, binding.VirtualKey);

    private void UnregisterAll()
    {
        if (_source is null) return;
        NativeMethods.UnregisterHotKey(_source.Handle, ToggleHotKeyId);
        NativeMethods.UnregisterHotKey(_source.Handle, ClearHotKeyId);
        NativeMethods.UnregisterHotKey(_source.Handle, DrawModeHotKeyId);
        NativeMethods.UnregisterHotKey(_source.Handle, ShortcutsHotKeyId);
        ToggleRegistered = ClearRegistered = DrawModeRegistered = ShortcutsRegistered = false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY) return 0;
        switch ((int)wParam)
        {
            case ToggleHotKeyId: TogglePressed?.Invoke(); handled = true; break;
            case ClearHotKeyId: ClearPressed?.Invoke(); handled = true; break;
            case DrawModeHotKeyId: DrawModePressed?.Invoke(); handled = true; break;
            case ShortcutsHotKeyId: ShortcutsPressed?.Invoke(); handled = true; break;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_source is null) return;
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }
}
