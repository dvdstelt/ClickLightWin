using System.Runtime.InteropServices;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// System-wide low-level mouse hook. Raises <see cref="ClickDetected"/> on the
/// UI thread for every button press, release, and drag, and <see cref="AnnotationDetected"/>
/// for Ctrl+Shift annotation gestures (which it swallows so they never reach the
/// app underneath). The Windows analogue of the macOS Quartz event tap.
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    // Keep the delegate alive for the lifetime of the hook; if it is collected
    // the callback becomes a dangling pointer and the app crashes.
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private nint _hookHandle;
    private bool _leftDown, _rightDown, _middleDown;

    // In-progress Ctrl+Shift annotation gesture.
    private bool _annotating;
    private AnnotationTool _annotatingTool;
    private int _annotatingUpMsg;

    public event Action<ClickEvent>? ClickDetected;
    public event Action<AnnotationEvent>? AnnotationDetected;

    /// <summary>
    /// When true, plain mouse moves (no button held) are also raised as
    /// <see cref="ClickPhase.Move"/>. Off by default so the high-frequency move
    /// stream is only processed when a feature (the laser pointer) needs it.
    /// </summary>
    public bool EmitMoves { get; set; }

    /// <summary>When true, Ctrl+Shift + drag draws annotations instead of clicking through.</summary>
    public bool AnnotationsEnabled { get; set; }

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

            // Annotation gestures are swallowed so the drag never reaches the app.
            if (HandleAnnotation(msg, data.pt.X, data.pt.Y))
                return 1;

            if (TryMap(msg, data, out var click))
                ClickDetected?.Invoke(click); // handler marshals to Dispatcher if needed
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Returns true if the event should be swallowed (kept from the app). We swallow
    /// only the button down and up of an annotation gesture, never the moves: eating
    /// WM_MOUSEMOVE in a low-level hook freezes the cursor. Because the app never sees
    /// the button press, the passed-through moves are just hover and don't drag it.
    /// </summary>
    private bool HandleAnnotation(int msg, int x, int y)
    {
        if (_annotating)
        {
            if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                AnnotationDetected?.Invoke(new(_annotatingTool, AnnotationPhase.Update, x, y));
                return false; // let the move through so the cursor keeps moving
            }
            if (msg == _annotatingUpMsg)
            {
                AnnotationDetected?.Invoke(new(_annotatingTool, AnnotationPhase.Commit, x, y));
                _annotating = false;
                return true; // swallow the release
            }
            // Swallow any other button events mid-gesture; leave moves/wheel alone.
            return msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN
                or NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_LBUTTONUP
                or NativeMethods.WM_RBUTTONUP or NativeMethods.WM_MBUTTONUP;
        }

        if (!AnnotationsEnabled) return false;
        var isDown = msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN;
        if (!isDown || !NativeMethods.IsDown(NativeMethods.VK_CONTROL) || !NativeMethods.IsDown(NativeMethods.VK_SHIFT))
            return false;

        _annotating = true;
        _annotatingTool = msg == NativeMethods.WM_LBUTTONDOWN ? AnnotationTool.Arrow : AnnotationTool.Box;
        _annotatingUpMsg = msg == NativeMethods.WM_LBUTTONDOWN ? NativeMethods.WM_LBUTTONUP : NativeMethods.WM_RBUTTONUP;
        AnnotationDetected?.Invoke(new(_annotatingTool, AnnotationPhase.Begin, x, y));
        return true; // swallow the press so the app never starts a selection/drag
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
            case NativeMethods.WM_MOUSEMOVE when EmitMoves:   click = new(ClickButton.Left, ClickPhase.Move, x, y);   return true;
        }
        click = default;
        return false;
    }

    public void Dispose() => Uninstall();
}
