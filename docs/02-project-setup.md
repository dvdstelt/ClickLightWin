# Project Setup

Copy-paste-ready project files. Run all commands on **Windows** (WPF does not build elsewhere).

## Prerequisites

- Windows 10 21H2+ or Windows 11
- .NET 10 SDK: verify with `dotnet --version` (expect `10.x`)
- Optional: Visual Studio 2022 17.12+ with the ".NET desktop development" workload, or VS Code with the C# Dev Kit

## Folder layout

Create this structure at the repo root (the `docs/` folder and the reference `ClickLight/` clone already exist):

```
ClickLightWin/
├── ClickLightWin.sln
├── global.json
├── docs/                      <- these documents
├── ClickLight/                <- macOS reference clone (git-ignored)
└── src/
    └── ClickLightWin/
        ├── ClickLightWin.csproj
        ├── app.manifest
        ├── App.xaml
        ├── App.xaml.cs
        ├── AppController.cs
        ├── ClickEvent.cs
        ├── Settings.cs
        ├── Interop/
        │   ├── NativeMethods.cs
        │   └── LowLevelMouseHook.cs
        ├── Overlay/
        │   ├── OverlayManager.cs
        │   ├── OverlayWindow.xaml
        │   └── OverlayWindow.xaml.cs
        ├── Rendering/
        │   ├── CoordinateMapper.cs
        │   └── PulseRenderer.cs
        └── Tray/
            └── TrayIcon.cs
```

## Bootstrap commands

```powershell
# from the repo root
dotnet new sln -n ClickLightWin
mkdir src\ClickLightWin
# create the files below, then:
dotnet sln add src\ClickLightWin\ClickLightWin.csproj
dotnet build
dotnet run --project src\ClickLightWin
```

## `global.json`

Pin the SDK with feature-level roll-forward so a newer 10.x patch still works.

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestFeature"
  }
}
```

## `src/ClickLightWin/ClickLightWin.csproj`

WPF plus WinForms (for the tray icon and `Screen`), unpackaged desktop app.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>latest</LangVersion>
    <RootNamespace>ClickLightWin</RootNamespace>
    <AssemblyName>ClickLightWin</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64;arm64</Platforms>
  </PropertyGroup>

</Project>
```

## `src/ClickLightWin/app.manifest`

Per-Monitor v2 DPI awareness is mandatory for the overlay to line up on mixed-DPI setups.

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="0.1.0.0" name="ClickLightWin.app" />

  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>

  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

## `.gitignore`

At minimum, ignore build output and the reference clone (it has its own git history):

```gitignore
bin/
obj/
.vs/
*.user

# macOS reference clone, kept locally for reference only
/ClickLight/
```

## App startup wiring

WPF's default `StartupUri` opens a window. This app has no main window, so remove it and drive startup from code.

`src/ClickLightWin/App.xaml`:

```xml
<Application x:Class="ClickLightWin.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources />
</Application>
```

`src/ClickLightWin/App.xaml.cs`:

```csharp
using System.Windows;

namespace ClickLightWin;

public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _controller = new AppController();
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
```

`ShutdownMode="OnExplicitShutdown"` is important: with no visible window, the default `OnLastWindowClose` would quit the app the instant a transient overlay closes. Quit is driven only by the tray menu calling `Application.Current.Shutdown()`.

With these files in place, move on to [03-implementation-guide.md](03-implementation-guide.md) for the component code.
