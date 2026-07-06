# Build Checklist

Work top to bottom. Each milestone is runnable, so you always have something to demo and a small surface to debug. Check items off as you go.

## Milestone 0: Project skeleton

- [ ] On a Windows machine with .NET 10 SDK (`dotnet --version` shows `10.x`)
- [ ] Create folder layout from [02-project-setup.md](02-project-setup.md)
- [ ] Add `global.json`, `ClickLightWin.csproj`, `app.manifest`
- [ ] Add `App.xaml` / `App.xaml.cs` with `ShutdownMode="OnExplicitShutdown"`
- [ ] `dotnet new sln` and `dotnet sln add`
- [ ] Add a temporary `MessageBox.Show("hi")` in `App.OnStartup`
- [ ] `dotnet build` succeeds
- [ ] `dotnet run` shows the message box
- [ ] Remove the temporary message box

## Milestone 1: Tray presence

- [ ] Add `ClickEvent.cs` (button/phase/coords model)
- [ ] Add `Settings.cs` (minimal version from the guide)
- [ ] Add `Tray/TrayIcon.cs`
- [ ] Add a minimal `AppController` that only creates the tray
- [ ] App runs with **no window**, shows a tray icon
- [ ] Tray "Quit" exits the process cleanly (check Task Manager: no orphan)
- [ ] Tray "Enabled" toggle flips a bool (verify with a debug log)

## Milestone 2: The overlay window

- [ ] Add `Interop/NativeMethods.cs`
- [ ] Add `Overlay/OverlayWindow.xaml` + `.cs`
- [ ] Temporarily give the canvas a semi-transparent background to see it
- [ ] Overlay covers the full primary monitor
- [ ] Overlay is topmost (stays above other windows)
- [ ] Overlay does **not** appear in alt-tab or the taskbar
- [ ] Clicks pass **through** the overlay to the app underneath (click-through works)
- [ ] Revert the temporary background to `Transparent`

## Milestone 3: The mouse hook

- [ ] Add `Interop/LowLevelMouseHook.cs`
- [ ] Install the hook from `AppController.Start` (on the UI thread)
- [ ] Log every `ClickDetected` with button/phase/coords
- [ ] Left, right, and middle press/release all logged
- [ ] Cursor feels responsive (no lag) with the hook active
- [ ] Hook uninstalls cleanly on quit (no crash, no lingering lag)

## Milestone 4: Pulses (the v0.1 payoff)

- [ ] Add `Rendering/CoordinateMapper.cs`
- [ ] Add `Rendering/PulseRenderer.cs`
- [ ] Wire `OverlayWindow.ShowPulse` and route a click to it
- [ ] A click draws a fading pulse **at the cursor** on the primary monitor
- [ ] Pulse color differs per button (left/right/middle)
- [ ] Pulse animates (grows + fades) and is removed after it finishes
- [ ] Toggling "Enabled" off stops pulses; on resumes them
- [ ] **Tag v0.1** here: this is the minimum viable ClickLight for Windows

## Milestone 5: Multi-monitor + DPI correctness

- [ ] Add `Overlay/OverlayManager.cs`; one overlay per `Screen.AllScreens`
- [ ] Expose `OverlayWindow.ScreenBounds` for hit-testing
- [ ] Clicks on a secondary monitor draw on that monitor
- [ ] Pulse lands exactly under the cursor at 100% DPI
- [ ] Pulse lands exactly under the cursor at 150%/175% DPI
- [ ] Correct on a **mixed-DPI** setup (e.g. 150% laptop + 100% external)
- [ ] Plugging/unplugging a monitor rebuilds overlays (`DisplaySettingsChanged`)
- [ ] Changing scale in Display Settings still lands pulses correctly

## Milestone 6: Polish for daily use

- [ ] Real `.ico` tray asset (replace `SystemIcons.Application`)
- [ ] Single-instance guard (named `Mutex`) so it cannot launch twice
- [ ] Global hotkey to toggle on/off (`RegisterHotKey` + `WM_HOTKEY`)
- [ ] Persist settings to `%APPDATA%\ClickLightWin\settings.json`
- [ ] Launch-at-login option (HKCU `Run` key or Startup shortcut)
- [ ] Document the elevated-window / UAC limitation in the README

## Milestone 7: Feature parity (optional, incremental)

- [ ] Separate press vs. release visuals
- [ ] Right-click and drag variants
- [ ] Laser-pointer mode (fading freehand stroke on drag)
- [ ] Settings window (WPF, bound to the settings model)
- [ ] Menu-bar quick presets (size / duration / intensity / color)
- [ ] Live keyboard-shortcut display (`WH_KEYBOARD_LL`)
- [ ] Screenshot-shortcut handling
- [ ] Daily click activity chart + 7-day history
- [ ] Auto-update via Velopack or WinSparkle

## Cross-cutting checks before any release

- [ ] No lingering process after Quit
- [ ] No cursor lag under sustained clicking
- [ ] Overlay never steals focus (typing is uninterrupted while clicking)
- [ ] Works after sleep/resume and after RDP disconnect/reconnect
- [ ] Behaves sanely when all monitors are unplugged then replugged
- [ ] Memory stays flat over a long session (no pulse/handle leak)

## Reference behavior

When unsure how a feature should look or feel, read the corresponding Swift file in [`../ClickLight/Sources/ClickLight/`](../ClickLight/Sources/ClickLight/) rather than inventing behavior. The mapping from Swift files to Windows components is in [01-architecture.md](01-architecture.md).
