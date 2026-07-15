# ClickLight for Windows

A small Windows application in the system-tray that highlights your clicks during live demos, screen sharing, and other moments where people need to be able to follow what you are doing.

Screen recorders can add click effects after the fact. ClickLight is for the live moment itself, when you need the audience to see exactly when you clicked without interrupting your flow.

It is a ground-up C# / .NET (WPF) reimplementation of the macOS app [ClickLight](https://github.com/aurorascharff/ClickLight), not a port: every macOS primitive is mapped to a clean Windows equivalent.

On each click, a short animated pulse is drawn at the cursor and fades out. The app runs headless apart from a tray icon; there is no main window.

## Demo

-- soon --

## Features

- Click highlights across macOS apps
- Separate visuals for press, release, right-click, and drag
- Optional laser pointer mode with drawing mode
- Optional keyboard shortcuts
- Local daily click activity chart with a resettable seven-day history
- Dedicated settings window with presets, profiles, and a sidebar preview pad with Randomize
- Custom color picker in Settings
- One default ClickLight toggle shortcut, with optional shortcuts for other actions
- System-tray menu
  - Quick presets for size, duration
  - Customizable menu for hiding optional controls you do not use

- Test pulse for verifying overlay behavior

See [docs/04-build-checklist.md](docs/04-build-checklist.md) for the roadmap this was built against.

> Note: multi-monitor click routing and mixed-DPI placement are implemented but have only been verified on a single monitor (100% and 150%). Worth a pass on multi-monitor and mixed-DPI hardware.

## Keyboard Shortcuts

ClickLight includes one default global shortcut for quick toggles during demos. Other actions can be assigned shortcuts in Settings if you want them.

| Shortcut | Action |
| --- | --- |
| `Control + Shift + L` | Toggle ClickLight on/off |
| `Control + Shift + D` | Toggle drawing mode, cleared by exiting drawing mode. |
| `Control + Shift + left-click` & drag | Draw an arrow |
| `Control + Shift + right-click` & drag | Draw a box |
| `Control + SHift + C` | Clear drawings |

All shortcuts can be changed in Settings.

## Download

Grab the latest from the [Releases](../../releases) page. Two options:

- **`ClickLight-vX.Y.Z-Setup.exe`** (installer, ~7 MB) — recommended. Installs the app, adds Start-menu/desktop shortcuts, and installs the .NET 10 Desktop Runtime for you if it is missing.
- **`ClickLight-vX.Y.Z-win-x64.exe`** (bare app, ~250 KB) — for machines that already have the .NET 10 Desktop Runtime. Just run it.
- **`ClickLight-vX.Y.Z-win-arm64.exe`** — the same bare app for Windows-on-Arm (needs the arm64 .NET 10 Desktop Runtime).

> [!NOTE]
>
> Until code signing is set up (see below), both are unsigned, so on first launch Windows
> SmartScreen may say "Windows protected your PC" — click **More info -> Run anyway**.

## Known limitations

- **Elevated windows and secure surfaces.** A non-elevated hook cannot observe
  input directed at an elevated (admin) window, the UAC consent prompt, the lock
  screen, or Ctrl+Alt+Del. Over those surfaces no pulse appears. This is a
  Windows security boundary, not a bug. Running ClickLight elevated would let it
  highlight over admin apps, but that is rarely worth it for a demo tool.
- **Fullscreen exclusive apps.** A fullscreen exclusive game or another topmost
  window can cover the overlay.
- **Keyboard shortcut display privacy.** When enabled, it installs a keyboard hook
  that reacts *only* to modifier combinations (Ctrl / Alt / Win + key) and never to
  plain typing, so passwords and text are never captured. Nothing is stored or sent;
  each combo becomes a transient on-screen label. It is off by default and the hook
  is only installed while the feature is on.
- **Mixed-DPI multi-monitor.** Placement is verified at 100% and 150% on a single
  monitor. Cross-monitor routing and mixed-DPI setups (e.g. a 150% laptop panel
  plus a 100% external) should be verified on that hardware.
