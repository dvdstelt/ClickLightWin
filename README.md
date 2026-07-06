# ClickLightWin

A Windows menu-bar (system-tray) app that highlights your mouse clicks on screen
during live demos, screen sharing, and UX reviews. It is a ground-up C# / .NET
(WPF) reimplementation of the macOS app [ClickLight](https://github.com/aurorascharff/ClickLight),
not a port: every macOS primitive is mapped to a clean Windows equivalent.

On each click, a short animated pulse is drawn at the cursor and fades out. The
app runs headless apart from a tray icon; there is no main window.

## Status

v0.1 and the daily-use polish milestone are done:

- System-wide click capture via a low-level mouse hook
- Transparent, click-through, topmost overlay per monitor
- Fading pulse at the cursor, colored per button (left blue, right orange, middle green)
- Per-monitor DPI-correct placement (Per-Monitor v2)
- Tray menu: Enabled toggle, Launch at login, Quit
- Global toggle hotkey: **Ctrl+Alt+L**
- Single-instance guard; settings persisted to `%APPDATA%\ClickLightWin\settings.json`

See [docs/04-build-checklist.md](docs/04-build-checklist.md) for the full roadmap.

## Requirements

- Windows 10 21H2+ or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` shows `10.x`)
- WPF builds on Windows only

## Build and run

```powershell
# from the repo root
dotnet build src\ClickLightWin.sln
dotnet run --project src\ClickLightWin\ClickLightWin.csproj
```

The app starts in the tray. Click anywhere to see a pulse. Right-click the tray
icon for the menu, or press **Ctrl+Alt+L** to toggle highlighting on and off.

## Known limitations

- **Elevated windows and secure surfaces.** A non-elevated hook cannot observe
  input directed at an elevated (admin) window, the UAC consent prompt, the lock
  screen, or Ctrl+Alt+Del. Over those surfaces no pulse appears. This is a
  Windows security boundary, not a bug. Running ClickLight elevated would let it
  highlight over admin apps, but that is rarely worth it for a demo tool.
- **Fullscreen exclusive apps.** A fullscreen exclusive game or another topmost
  window can cover the overlay.
- **Mixed-DPI multi-monitor.** Placement is verified at 100% and 150% on a single
  monitor. Cross-monitor routing and mixed-DPI setups (e.g. a 150% laptop panel
  plus a 100% external) should be verified on that hardware.

## Layout

```
ClickLightWin/
├── README.md                 this file
├── global.json               .NET SDK pin
├── docs/                      build blueprint and API mapping
├── ClickLight/                macOS reference clone (git-ignored)
└── src/
    ├── ClickLightWin.sln
    └── ClickLightWin/         the app (WPF + WinForms tray)
```

The macOS Swift sources under `ClickLight/Sources/ClickLight/` are the source of
truth for behavior (pulse shapes, timing, presets). The mapping from Swift files
to Windows components is in [docs/01-architecture.md](docs/01-architecture.md).
