# Contributing to ClickLight for Windows

Thanks for helping improve ClickLight.

## Get Started

1. Fork the repository and create a branch for your change.
2. Build and run the app (see below).
3. Keep changes focused, and update the docs and [CHANGELOG.md](CHANGELOG.md) when behavior changes.
4. Open a pull request against `main`.

## Requirements

- Windows 10 21H2+ or Windows 11 (WPF builds and runs on Windows only)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` shows `10.x`)

## Build and Run

From the repository root:

```powershell
dotnet build src\ClickLightWin.sln
dotnet run --project src\ClickLightWin\ClickLightWin.csproj
```

The app starts in the system tray. Right-click the tray icon for the menu, or
press **Ctrl+Shift+L** to toggle highlighting.

## Verify Changes

Run the unit tests:

```powershell
dotnet test src\ClickLightWin.sln
```

The tests cover the pure seams (coordinate mapping, settings persistence, shortcut
formatting). Most of the app is UI and system hooks, so also **run the app and
manually exercise what you changed**. Useful checks:

- the tray menu and the Settings window (including the live Preview Pad)
- click highlights in another app (press, release, right-click, drag)
- laser-pointer and drawing modes, and any annotation gesture
- any new keyboard shortcut or pointer interaction

Include your manual test steps in the pull request.

## Code Style

- Style is enforced by [.editorconfig](.editorconfig); please keep to it.
- Nullable reference types are enabled, and Release builds treat warnings as errors.
- Match the surrounding code: the project uses current C# features and favors small,
  well-named methods over comments.
- Commit messages use short, imperative, gitmoji-style prefixes (e.g. `✨ Add ...`,
  `🐛 Fix ...`, `📝 Update ...`) — match the existing history.

## Pull Requests

- Explain what changed and why.
- Include screenshots or a short recording for visible UI changes.
- Do not commit signing credentials, private keys, build output, or local system files.
- Keep changes to the release workflow separate unless that is what you are working on.

For security-sensitive reports, please contact the maintainer privately rather than
opening a public issue.
