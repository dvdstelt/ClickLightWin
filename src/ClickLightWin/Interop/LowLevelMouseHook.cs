using System.Runtime.InteropServices;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// System-wide low-level mouse hook. Raises <see cref="ClickDetected"/> on the
/// UI thread for every button press, release, and drag. The Windows analogue of
/// the macOS Quartz event tap in ClickEventTap.swift.
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    // Keep the delegate alive for the lifetime of the hook; if it is collected
    // the callback becomes a dangling pointer and the app crashes.
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private nint _hookHandle;
    private bool _leftDown, _rightDown, _middleDown;

    public event Action<ClickEvent>? ClickDetected;

    public LowLevelMouseHook() => _proc = HookCallback;

    public void Install()
    {
        if (_hookHandle != 0) return;
        var hMod = NativeMethods.GetModuleHandleW(null);
        _hookHandle = NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
        if (_hookHandle == 0)
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public void Uninstall()
    {
        if (_hookHandle == 0) return;
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = 0;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;
            if (TryMap(msg, data, out var click))
                ClickDetected?.Invoke(click); // handler marshals to Dispatcher if needed
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool TryMap(int msg, NativeMethods.MSLLHOOKSTRUCT data, out ClickEvent click)
    {
        int x = data.pt.X, y = data.pt.Y;
        switch (msg)
        {
            case NativeMethods.WM_LBUTTONDOWN: _leftDown = true;   click = new(ClickButton.Left, ClickPhase.Down, x, y);   return true;
            case NativeMethods.WM_LBUTTONUP:   _leftDown = false;  click = new(ClickButton.Left, ClickPhase.Up, x, y);     return true;
            case NativeMethods.WM_RBUTTONDOWN: _rightDown = true;  click = new(ClickButton.Right, ClickPhase.Down, x, y);  return true;
            case NativeMethods.WM_RBUTTONUP:   _rightDown = false; click = new(ClickButton.Right, ClickPhase.Up, x, y);    return true;
            case NativeMethods.WM_MBUTTONDOWN: _middleDown = true; click = new(ClickButton.Middle, ClickPhase.Down, x, y); return true;
            case NativeMethods.WM_MBUTTONUP:   _middleDown = false;click = new(ClickButton.Middle, ClickPhase.Up, x, y);   return true;
            case NativeMethods.WM_MOUSEMOVE when _leftDown:   click = new(ClickButton.Left, ClickPhase.Drag, x, y);   return true;
            case NativeMethods.WM_MOUSEMOVE when _rightDown:  click = new(ClickButton.Right, ClickPhase.Drag, x, y);  return true;
            case NativeMethods.WM_MOUSEMOVE when _middleDown: click = new(ClickButton.Middle, ClickPhase.Drag, x, y); return true;
        }
        click = default;
        return false;
    }

    public void Dispose() => Uninstall();
}
