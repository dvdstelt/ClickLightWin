# Changelog

All notable changes to ClickLightWin are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- In-app auto-update (installer build only): checks GitHub Releases on startup
  and every six hours and, when a newer version is out, shows a "Restart to
  update" prompt in the tray. Nothing installs until you choose to.

### Fixed

- The tray menu now dismisses when you click away from it, instead of staying
  open until you pick an item.

## [1.2.0] - 2026-07-15

### Added

- Global shortcuts (toggle, clear, drawing mode) are now user-configurable via a
  click-to-record field in settings, each with a reset button.
- A tray toggle for the keyboard-shortcut display.

### Changed

- Replaced the color swatch rows in settings with compact color dropdowns.

### Removed

- The Ctrl+drag laser stroke, which had been swallowing Ctrl+click (so opening a
  link in a new tab or multi-selecting stopped working while the laser was on).
  The glowing laser cursor still trails the pointer.

## [1.1.0] - 2026-07-14

### Added

- Annotations: hold Ctrl+Shift and drag to draw an arrow (left) or a box
  (right); they persist until Ctrl+Shift+C clears them.
- A live keyboard-shortcut display for screencasts (modifier combos only, off by
  default).
- A frozen drawing mode (Ctrl+Shift+D) and a Ctrl+drag laser stroke.
- A unit-test project, an arm64 bare-app exe with each release, and an
  `.editorconfig` codifying the project conventions.

### Fixed

- Marshal mouse-hook events off the OS callback so drawing never runs inline.
- Stop processing the mouse-move stream while ClickLight is disabled.
- Detach the laser render loop when its overlay closes; complete laser strokes
  that cross monitors; re-render annotations after display-settings changes.
- Never show AltGr typing in the shortcut display.
- Warn via a tray balloon when a global hotkey is already in use.
- Keep the tray app alive by logging unhandled exceptions instead of vanishing.

## [1.0.0] - 2026-07-06

### Added

- Laser-pointer mode: a glowing cursor that trails the pointer.
- A modern dark tray menu with feature toggles and preset submenus, and pill
  toggle switches in settings.
- Release packaging: a small framework-dependent exe plus a .NET-bootstrapping
  installer, tag-driven GitHub releases, and MinVer versioning.

## [0.3.0] - 2026-07-06

### Added

- A contracting release ring on hold-and-release.
- A settings window with Size/Duration presets and per-button pulse colors.

### Changed

- Settings are now observable, persisted, and the single source of truth.

## [0.2.0] - 2026-07-06

### Added

- A fading drag trail.
- Multi-monitor, DPI-correct overlay routing.
- Branded tray icon, launch-at-login, a global toggle hotkey, persisted Enabled
  state, and a single-instance guard.

## [0.1.0] - 2026-07-06

### Added

- Initial release: fading click pulses drawn at the cursor, a system-tray
  presence, a transparent click-through overlay, and a system-wide mouse hook.

[Unreleased]: https://github.com/dvdstelt/ClickLightWin/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/dvdstelt/ClickLightWin/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/dvdstelt/ClickLightWin/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/dvdstelt/ClickLightWin/compare/v0.3.0...v1.0.0
[0.3.0]: https://github.com/dvdstelt/ClickLightWin/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/dvdstelt/ClickLightWin/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/dvdstelt/ClickLightWin/releases/tag/v0.1.0
