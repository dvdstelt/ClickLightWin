# Implementation Guide

Reference code for the parts that are non-obvious or Windows-specific. These are starting points, correct in shape and intent; refine them against the reference macOS behavior as you go. Copy them into the paths from [02-project-setup.md](02-project-setup.md).

The four hard parts, in order of difficulty:

1. [The click-through overlay window](#1-the-click-through-overlay-window) (trickiest)
2. [DPI and coordinate mapping](#2-dpi-and-coordinate-mapping)
3. [The low-level mouse hook](#3-the-low-level-mouse-hook)
4. [Multi-monitor routing and lifetime](#4-multi-monitor-routing)

Then the easy parts: [pulse rendering](#5-pulse-rendering), [tray icon](#6-tray-icon), [app controller](#7-app-controller), and [later milestones](#later-milestones).

---

## P/Invoke surface

`src/ClickLightWin/Interop/NativeMethods.cs` : the only unmanaged calls needed for v0.1.

```csharp
using System.Runtime.InteropServices;

namespace ClickLightWin.Interop;

/// <summary>
/// Thin P/Invoke surface for the Win32 primitives that have no managed wrapper:
/// the low-level mouse hook, extended-window-style flags for a click-through
/// overlay, and precise window placement in physical pixels.
/// </summary>
internal static partial class NativeMethods
{
    // ---- Low-level mouse hook -------------------------------------------------

    public const int WH_MOUSE_LL = 14;

    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_MOUSEMOVE = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    public delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint SetWindowsHookExW(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? lpModuleName);

    // ---- Click-through overlay window styles ---------------------------------

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    // ---- Precise placement in physical pixels --------------------------------

    public static readonly nint HWND_TOPMOST = -1;

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
```

---

## 1. The click-through overlay window

The overlay must be: fully transparent, always on top, invisible to alt-tab and the taskbar, and **click-through** so the app underneath still receives the clicks. WPF gives you transparency and topmost declaratively; click-through and the tool-window flags need extended styles applied after the HWND exists.

`src/ClickLightWin/Overlay/OverlayWindow.xaml`:

```xml
<Window x:Class="ClickLightWin.Overlay.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ShowActivated="False"
        ResizeMode="NoResize"
        IsHitTestVisible="False"
        WindowStartupLocation="Manual">
  <Canvas x:Name="PulseCanvas" IsHitTestVisible="False" />
</Window>
```

`src/ClickLightWin/Overlay/OverlayWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Forms; // Screen
using System.Windows.Interop;
using ClickLightWin.Interop;
using ClickLightWin.Rendering;

namespace ClickLightWin.Overlay;

/// <summary>
/// A transparent, click-through, topmost overlay covering exactly one monitor.
/// Placement is done in physical pixels via SetWindowPos so WPF's DIP-based
/// Left/Top do not fight per-monitor DPI.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly Screen _screen;
    private readonly PulseRenderer _renderer;

    public OverlayWindow(Screen screen)
    {
        InitializeComponent();
        _screen = screen;
        _renderer = new PulseRenderer(PulseCanvas);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // Make the window click-through and hidden from alt-tab / taskbar.
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        var newEx = (nint)((long)ex
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, newEx);

        // Place the window on its monitor in PHYSICAL pixels. Screen.Bounds is
        // already physical. This sidesteps WPF interpreting Left/Top in DIPs.
        var b = _screen.Bounds;
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            b.X, b.Y, b.Width, b.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>Spawn a pulse. Point is in physical virtual-screen pixels.</summary>
    public void ShowPulse(ClickEvent click, Settings settings)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var local = CoordinateMapper.PhysicalToLocalDips(click.ScreenX, click.ScreenY, _screen, dpi);
        _renderer.Spawn(local, click, settings);
    }
}
```

Gotchas learned the hard way:

- `AllowsTransparency=true` requires `WindowStyle=None`. They are a package.
- Apply `WS_EX_TRANSPARENT` in `OnSourceInitialized`, not the constructor; the HWND does not exist earlier.
- `ShowActivated=false` plus `WS_EX_NOACTIVATE` stops the overlay from stealing focus when it appears, which would interrupt the user mid-demo.
- Topmost is not absolute: another topmost window (or a fullscreen exclusive game/app) can still cover you. For a demo tool this is acceptable. If it becomes a problem, re-assert topmost on a timer or on `DisplaySettingsChanged`.

---

## 2. DPI and coordinate mapping

Mouse-hook points are physical virtual-screen pixels. WPF draws in DIPs local to each window. This helper is the single conversion seam. Getting it right here means every pulse lands exactly under the cursor on every monitor.

`src/ClickLightWin/Rendering/CoordinateMapper.cs`:

```csharp
using System.Windows;
using System.Windows.Forms; // Screen

namespace ClickLightWin.Rendering;

/// <summary>
/// Converts physical virtual-screen pixel coordinates (as delivered by the
/// low-level mouse hook) into device-independent units local to a specific
/// monitor's overlay window. This is the Windows analogue of the macOS
/// CoordinateMapper, which reconciles Quartz and AppKit coordinate spaces.
/// </summary>
public static class CoordinateMapper
{
    public static Point PhysicalToLocalDips(int physX, int physY, Screen screen, DpiScale dpi)
    {
        // Offset into the monitor, in physical pixels.
        var offsetX = physX - screen.Bounds.X;
        var offsetY = physY - screen.Bounds.Y;

        // Physical pixels -> DIPs. dpi.DpiScaleX is 1.0 at 96 DPI, 1.5 at 144, etc.
        return new Point(offsetX / dpi.DpiScaleX, offsetY / dpi.DpiScaleY);
    }
}
```

Notes:

- `VisualTreeHelper.GetDpi(window)` returns the window's *current* monitor DPI under Per-Monitor v2, and it updates when the window moves to a monitor with a different scale. Read it at draw time (as `OverlayWindow.ShowPulse` does) rather than caching it.
- Because each overlay is pinned to one monitor and placed in physical pixels, `screen.Bounds.X/Y` is the correct origin to subtract.
- Test explicitly on a mixed-DPI setup (for example a 150% laptop panel plus a 100% external monitor). This is where coordinate bugs hide.

---

## 3. The low-level mouse hook

Installs `WH_MOUSE_LL` and raises a clean `ClickEvent` for each press, release, and drag. **Do the minimum in the callback** and never draw or block there; a slow hook makes the whole system's cursor lag and Windows will drop you from the chain.

`src/ClickLightWin/Interop/LowLevelMouseHook.cs`:

```csharp
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
                ClickDetected?.Invoke(click); // handler marshals to Dispatcher
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool TryMap(int msg, NativeMethods.MSLLHOOKSTRUCT data, out ClickEvent click)
    {
        int x = data.pt.X, y = data.pt.Y;
        switch (msg)
        {
            case NativeMethods.WM_LBUTTONDOWN: _leftDown = true;  click = new(ClickButton.Left, ClickPhase.Down, x, y); return true;
            case NativeMethods.WM_LBUTTONUP:   _leftDown = false; click = new(ClickButton.Left, ClickPhase.Up, x, y);   return true;
            case NativeMethods.WM_RBUTTONDOWN: _rightDown = true; click = new(ClickButton.Right, ClickPhase.Down, x, y); return true;
            case NativeMethods.WM_RBUTTONUP:   _rightDown = false;click = new(ClickButton.Right, ClickPhase.Up, x, y);   return true;
            case NativeMethods.WM_MBUTTONDOWN: _middleDown = true;click = new(ClickButton.Middle, ClickPhase.Down, x, y); return true;
            case NativeMethods.WM_MBUTTONUP:   _middleDown = false;click = new(ClickButton.Middle, ClickPhase.Up, x, y);  return true;
            case NativeMethods.WM_MOUSEMOVE when _leftDown:  click = new(ClickButton.Left, ClickPhase.Drag, x, y);  return true;
            case NativeMethods.WM_MOUSEMOVE when _rightDown: click = new(ClickButton.Right, ClickPhase.Drag, x, y); return true;
            case NativeMethods.WM_MOUSEMOVE when _middleDown:click = new(ClickButton.Middle, ClickPhase.Drag, x, y);return true;
        }
        click = default;
        return false;
    }

    public void Dispose() => Uninstall();
}
```

Notes:

- The hook is installed on the thread that calls `Install()`. Call it from the WPF UI thread so `ClickDetected` fires there and no cross-thread marshaling is needed before touching the overlay. If you ever move the hook to its own thread, that thread must run a message loop or the hook never fires.
- For v0.1 you may want to ignore `Drag` entirely (only draw on `Down`/`Up`) to keep it simple; the drag phase is here for the laser-pointer feature later.
- The reference `ClickEventTap.swift` also keeps a fallback global monitor. On Windows the single low-level hook is reliable enough that you do not need a fallback.

---

## 4. Multi-monitor routing

One overlay per monitor; route each click to the monitor whose physical bounds contain it. Recreate overlays when the display arrangement changes.

`src/ClickLightWin/Overlay/OverlayManager.cs`:

```csharp
using System.Windows;
using System.Windows.Forms; // Screen
using Microsoft.Win32;       // SystemEvents

namespace ClickLightWin.Overlay;

public sealed class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlays = new();
    private readonly Settings _settings;

    public OverlayManager(Settings settings)
    {
        _settings = settings;
        Rebuild();
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
    }

    public void Dispatch(ClickEvent click)
    {
        // Route to the monitor that physically contains the point.
        foreach (var overlay in _overlays)
            if (overlay.ScreenBounds.Contains(click.ScreenX, click.ScreenY))
            {
                overlay.ShowPulse(click, _settings);
                return;
            }
    }

    private void OnDisplaysChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        foreach (var o in _overlays) o.Close();
        _overlays.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new OverlayWindow(screen);
            overlay.Show(); // shown but transparent + click-through; never activated
            _overlays.Add(overlay);
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        foreach (var o in _overlays) o.Close();
        _overlays.Clear();
    }
}
```

Add a `ScreenBounds` convenience to `OverlayWindow` exposing `_screen.Bounds` so the manager can hit-test without touching WinForms types elsewhere.

---

## 5. Pulse rendering

Retained-mode WPF makes the fading pulse almost free: add an `Ellipse` to the canvas, animate scale and opacity with a `Storyboard`, remove it on completion. Match the visual parameters (size, duration, per-button color) to the reference `ClickOverlayView.swift`.

`src/ClickLightWin/Rendering/PulseRenderer.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ClickLightWin.Rendering;

/// <summary>
/// Spawns and animates click pulses on an overlay canvas. Each pulse is an
/// expanding, fading ring, colored per button. Mirrors ClickOverlayView.swift.
/// </summary>
public sealed class PulseRenderer(Canvas canvas)
{
    public void Spawn(Point center, ClickEvent click, Settings settings)
    {
        var color = settings.ColorFor(click.Button);
        var baseDiameter = settings.BaseDiameterDips;   // e.g. 28
        var maxScale = settings.MaxScale;               // e.g. 2.2
        var duration = settings.PulseDuration;          // e.g. 450ms

        var ring = new Ellipse
        {
            Width = baseDiameter,
            Height = baseDiameter,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = settings.StrokeThickness, // e.g. 3
            Fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        // Position so the ellipse is centered on the click point.
        Canvas.SetLeft(ring, center.X - baseDiameter / 2);
        Canvas.SetTop(ring, center.Y - baseDiameter / 2);

        var scale = new ScaleTransform(1, 1);
        ring.RenderTransform = scale;
        canvas.Children.Add(ring);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(1, maxScale, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => canvas.Children.Remove(ring);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ring.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
```

Extending this later: press vs. release visuals, right-click and drag variants, and the laser-pointer freehand stroke all live here. Read `ClickOverlayView.swift` for the exact shapes and timings the original uses, then translate.

---

## 6. Tray icon

The system-tray presence and menu. WinForms `NotifyIcon` is the least-ceremony option and needs no extra package.

`src/ClickLightWin/Tray/TrayIcon.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms; // NotifyIcon, ContextMenuStrip

namespace ClickLightWin.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public event Action? ToggleRequested;
    public event Action? QuitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        var toggle = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
        toggle.Click += (_, _) => ToggleRequested?.Invoke();
        menu.Items.Add(toggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // replace with a real .ico asset
            Text = "ClickLight",
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
```

Ship a real `.ico` (embed as a resource) instead of `SystemIcons.Application` before release.

---

## 7. App controller

Wires the three pieces together and owns their lifetime. The click handler marshals onto the WPF dispatcher before touching any overlay.

`src/ClickLightWin/AppController.cs`:

```csharp
using System.Windows;
using ClickLightWin.Overlay;
using ClickLightWin.Tray;

namespace ClickLightWin;

public sealed class AppController : IDisposable
{
    private readonly Settings _settings = Settings.Default;
    private readonly LowLevelMouseHook _hook = new();
    private OverlayManager? _overlays;
    private TrayIcon? _tray;
    private bool _enabled = true;

    public void Start()
    {
        _overlays = new OverlayManager(_settings);

        _tray = new TrayIcon();
        _tray.ToggleRequested += () => _enabled = !_enabled;
        _tray.QuitRequested += () => Application.Current.Shutdown();

        _hook.ClickDetected += OnClick;
        _hook.Install(); // installs on the UI thread => callback runs here
    }

    private void OnClick(ClickEvent click)
    {
        if (!_enabled) return;
        // If the hook ever moves off the UI thread, wrap this in Dispatcher.BeginInvoke.
        _overlays?.Dispatch(click);
    }

    public void Dispose()
    {
        _hook.Dispose();
        _overlays?.Dispose();
        _tray?.Dispose();
    }
}
```

A minimal `Settings` to compile against (expand later into the full model from `ClickSettingOptions.swift`):

```csharp
using System.Windows;
using System.Windows.Media;

namespace ClickLightWin;

public sealed class Settings
{
    public double BaseDiameterDips { get; init; } = 28;
    public double MaxScale { get; init; } = 2.2;
    public double StrokeThickness { get; init; } = 3;
    public Duration PulseDuration { get; init; } = new(TimeSpan.FromMilliseconds(450));

    public Color ColorFor(ClickButton button) => button switch
    {
        ClickButton.Left => Color.FromRgb(0x3B, 0x82, 0xF6),   // blue
        ClickButton.Right => Color.FromRgb(0xF9, 0x73, 0x16),  // orange
        ClickButton.Middle => Color.FromRgb(0x22, 0xC5, 0x5E), // green
        _ => Colors.White
    };

    public static Settings Default => new();
}
```

At this point you have a runnable v0.1: clicks anywhere draw a fading pulse, and the tray menu toggles and quits.

---

## Later milestones

Not needed for v0.1, but here is where each remaining feature slots in:

- **Global hotkeys** (toggle on/off, etc.): `RegisterHotKey(hwnd, id, modifiers, vk)` on a hidden message-only window, handle `WM_HOTKEY` in an `HwndSource` hook. Maps to `HotKeyManager.swift`.
- **Keyboard-shortcut display + screenshot handling**: add a `WH_KEYBOARD_LL` hook alongside the mouse hook. This is the only feature that would want an extra input hook.
- **Laser pointer**: use the `Drag` phase already emitted by the hook; render a fading freehand polyline in `PulseRenderer`.
- **Settings window**: a normal WPF window (not click-through) bound to the `Settings` model, opened from the tray menu. Persist to `%APPDATA%\ClickLightWin\settings.json`.
- **Activity chart / daily counts**: a small JSON store in `%APPDATA%`, mirroring `ClickActivityStore.swift`.
- **Launch at login**: write a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, or drop a shortcut in the Startup folder. Maps to `LaunchAtLoginController.swift`.
- **Auto-update**: integrate Velopack (recommended) or WinSparkle. Maps to Sparkle / `UpdateChecker.swift`.

## Known limitations to document for users

- Highlights will not appear over UAC prompts, the lock screen, or elevated (admin) windows unless ClickLight itself runs elevated. This is a Windows security boundary.
- A fullscreen exclusive app or another topmost window can cover the overlay.
- Test coordinate accuracy specifically on mixed-DPI multi-monitor setups.
