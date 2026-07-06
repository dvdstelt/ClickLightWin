# Architecture

## The core idea

A single background process that:

1. Installs a **system-wide low-level mouse hook** so it sees every click regardless of which app is focused.
2. Owns one **transparent, always-on-top, click-through overlay window per monitor**.
3. On each click, draws a short **animated pulse** at the cursor position, then fades it out.
4. Sits in the **system tray** with a menu to enable/disable and quit.

No main window is ever shown. The app runs headless apart from the tray icon and the transient overlays.

## macOS to Windows API mapping

Every macOS primitive the original uses has a Windows counterpart. This table is the heart of the port.

| Concern | macOS (original) | Windows (this build) |
| --- | --- | --- |
| Capture clicks system-wide | `CGEvent.tapCreate` (Quartz event tap) + `NSEvent.addGlobalMonitorForEvents` | `SetWindowsHookEx(WH_MOUSE_LL)` low-level mouse hook |
| Capture keys system-wide | Same event tap, `keyDown` | `SetWindowsHookEx(WH_KEYBOARD_LL)` (later, for shortcut display) |
| Permission to observe input | Accessibility + Input Monitoring prompts | **None required** for a non-elevated hook (caveat below) |
| Transparent overlay window | Borderless `NSWindow`, `backgroundColor = .clear`, `level = .screenSaver` | WPF `Window` with `AllowsTransparency=true`, `WindowStyle=None`, `Topmost=true` |
| Click-through (pass mouse events under it) | `ignoresMouseEvents = true` | Extended styles `WS_EX_LAYERED | WS_EX_TRANSPARENT` |
| Hide from alt-tab / taskbar | `NSWindow` non-activating | `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, `ShowInTaskbar=false` |
| Show above all spaces / fullscreen | `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]` | `Topmost=true` per-monitor overlay (see caveats) |
| Custom drawing | `NSView` + CoreGraphics | WPF `Canvas` + `Shape`/`DrawingVisual`, animated with `Storyboard` |
| Menu-bar item | `NSStatusBar` / `NSStatusItem` | `System.Windows.Forms.NotifyIcon` (tray) |
| Global hotkeys | Carbon `RegisterEventHotKey` (`HIToolbox`) | `RegisterHotKey` (user32) |
| Multi-monitor geometry | `NSScreen.screens`, Quartz/AppKit flip | `System.Windows.Forms.Screen.AllScreens` / `EnumDisplayMonitors` |
| DPI model | Uniform backing-scale per screen | **Per-monitor DPI v2** (the main new complexity, see below) |
| Launch at login | `SMAppService` (ServiceManagement) | `Run` registry key or a Startup-folder shortcut |
| Auto-update | Sparkle | WinSparkle, Velopack, Squirrel, or MSIX |
| Build system | Swift Package Manager | .NET SDK (`dotnet` / MSBuild) |

## Two things that are genuinely different on Windows

### 1. Permissions are simpler, with one caveat

macOS gates click capture behind Accessibility and Input Monitoring permissions and a system prompt. Windows low-level hooks need **no consent prompt**. You install the hook and you receive events.

The one caveat: a hook running **without elevation cannot observe input** directed at an **elevated (admin) window** or at secure surfaces (the UAC consent screen, the lock screen, Ctrl+Alt+Del). During those moments the overlay simply will not fire. This is a Windows security boundary, not a bug. If you want highlights over admin apps, the app itself must run elevated, which is usually not worth it for a demo tool. Document the limitation and move on.

### 2. Per-monitor DPI is the real work

macOS gives every screen a single clean backing-scale factor and AppKit handles the rest. Windows lets each monitor run a different scale (100%, 150%, 175%, ...), and mouse-hook coordinates arrive in **physical virtual-screen pixels** while WPF draws in **device-independent units (DIPs)**. If you ignore this, pulses land in the wrong spot on any non-100% monitor.

The strategy that works:

- Declare **Per-Monitor v2** DPI awareness in the app manifest (already in the setup docs).
- Create **one overlay window per monitor**, placed with `SetWindowPos` in physical pixels so WPF does not second-guess the position.
- Convert each incoming physical click point into that window's local DIPs using the window's current DPI (`VisualTreeHelper.GetDpi`, refreshed on `DpiChanged`).

Get this seam right once in a `CoordinateMapper` helper and the rest of the app stays simple. This mirrors the role of `CoordinateMapper.swift` in the original, which solves the analogous Quartz/AppKit flip problem.

## Component design

Keep the OS-integration seams isolated behind small interfaces so the drawing and settings logic stays testable and portable.

```
AppController                     app lifetime, wires everything together
├── LowLevelMouseHook             installs WH_MOUSE_LL, raises ClickEvent
│     └── event: ClickDetected(ClickEvent)   physical pixels, button, phase
├── OverlayManager                one OverlayWindow per Screen; routes clicks
│     └── OverlayWindow (xN)      transparent, click-through, topmost
│           ├── CoordinateMapper  physical px -> window-local DIPs
│           └── PulseRenderer     spawns + animates + reaps pulses
├── TrayIcon                      NotifyIcon: enable/disable, quit
└── Settings                      colors, size, duration, per-button visuals
```

Data flow for one click:

```
mouse click
  -> WH_MOUSE_LL callback (background)  -> ClickEvent{button, phase, x, y}
  -> marshal to UI thread (Dispatcher)
  -> OverlayManager picks the monitor whose bounds contain (x, y)
  -> that OverlayWindow maps (x, y) to local DIPs
  -> PulseRenderer adds a pulse, animates scale + opacity, removes on finish
```

### Why one window per monitor rather than one giant window

A single window spanning the whole virtual desktop renders at one DPI, so it distorts on mixed-DPI setups and can misbehave across monitors with different scale factors. Per-monitor windows each adopt their own monitor's DPI cleanly. It is a little more bookkeeping (create/destroy on display changes via `SystemEvents.DisplaySettingsChanged`) but it is the correct model.

### Threading

The hook callback fires on the thread that installed it (keep it on the UI thread by pumping a message loop, which WPF already does). Do the absolute minimum in the callback: read the point and button, build a `ClickEvent`, and hand off. Never draw or block inside the hook callback; a slow hook makes the whole system's mouse feel laggy and Windows will silently drop you from the hook chain if you exceed the timeout.

## Recommended tech choices

- **WPF** over WinUI 3 or Win32/GDI for the overlay: WPF's `AllowsTransparency` layered window plus retained-mode drawing and `Storyboard` animations makes the fading-pulse effect almost free, and per-monitor DPI support is mature.
- **WinForms `NotifyIcon`** for the tray (enable via `<UseWindowsForms>true</UseWindowsForms>` alongside WPF): zero extra dependency, unlike Hardcodet.NotifyIcon.Wpf.
- **P/Invoke via `LibraryImport`** (source-generated, AOT-friendly) for the three things WPF does not wrap: the hook, `RegisterHotKey`, and the extended window styles.
- **Velopack** for updates when you get there: it is the modern, low-ceremony successor to Squirrel and is the closest spiritual match to Sparkle's "just works" updater.
