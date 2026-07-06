# ClickLightWin

A Windows reimplementation of [ClickLight](https://github.com/aurorascharff/ClickLight), a menu-bar app that highlights your mouse clicks on screen during live demos, screen sharing, and UX reviews.

The original is a native macOS app (Swift 6 / AppKit). Windows shares none of that stack, so this is a **ground-up rewrite in C# / .NET (WPF)**, not a port. Every capability the macOS app relies on has a clean Windows equivalent, and several are simpler on Windows (no accessibility permission prompt to capture clicks).

## What lives in this folder

These docs are a self-contained build blueprint. You can hand them to a fresh agent or pick them up yourself on a Windows box with zero prior context.

| File | Purpose |
| --- | --- |
| [01-architecture.md](01-architecture.md) | The macOS to Windows API mapping and the component design. Read this first. |
| [02-project-setup.md](02-project-setup.md) | Prerequisites and copy-paste-ready project files (`.csproj`, manifest, `global.json`). |
| [03-implementation-guide.md](03-implementation-guide.md) | The hard parts, with reference code: global mouse hook, click-through overlay, DPI and coordinate mapping, pulse rendering, tray icon, hotkeys, launch-at-login, auto-update. |
| [04-build-checklist.md](04-build-checklist.md) | A milestone-by-milestone checklist to drive the build and track progress. |

## Reference implementation

The original macOS source is cloned alongside these docs at [`../ClickLight`](../ClickLight) for reference. The Swift files under `ClickLight/Sources/ClickLight/` are the source of truth for behavior (pulse shapes, timing, settings, presets). When a behavior question comes up, read the corresponding Swift file rather than guessing.

Useful entry points in the reference:

- `Sources/ClickLight/ClickEventTap.swift` : how clicks are captured (maps to the Windows hook)
- `Sources/ClickLight/ClickOverlayWindow.swift` + `ClickOverlayView.swift` : the transparent overlay and drawing (maps to the WPF overlay)
- `Sources/ClickLight/ClickSettingOptions.swift` + `SettingsStore.swift` : the settings model and presets
- `Sources/ClickLight/HotKeyManager.swift` : global hotkeys

## Target platform

- Windows 10 21H2+ / Windows 11
- .NET 10 SDK (WPF requires building on Windows; it will not build on Linux or macOS)
- Visual Studio 2022+ or `dotnet` CLI on Windows

## Scope for v0.1

The first working milestone is deliberately small: capture clicks system-wide and draw a fading pulse at the cursor, with a tray icon to toggle and quit. Everything else (laser pointer, keyboard-shortcut display, settings window, activity chart, auto-update) layers on top. See the checklist for the staged plan.
