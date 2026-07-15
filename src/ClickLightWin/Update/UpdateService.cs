using Velopack;
using Velopack.Sources;

namespace ClickLightWin.Update;

/// <summary>
/// Wraps Velopack's <see cref="UpdateManager"/> to check GitHub Releases for a
/// newer build and apply it on demand. Active only for the installed (Setup.exe)
/// build: the portable exe and `dotnet run` are not Velopack-managed, so
/// <see cref="IsSupported"/> is false and every method is a no-op. The user is
/// never updated silently, they choose when to apply via the tray.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/dvdstelt/ClickLightWin";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, prerelease: false));
    private UpdateInfo? _pending;

    /// <summary>True only when running as a Velopack-installed app (not portable / dev).</summary>
    public bool IsSupported => _manager.IsInstalled;

    /// <summary>Version of a found-but-not-yet-applied update, or null if none is pending.</summary>
    public string? PendingVersion => _pending?.TargetFullRelease?.Version?.ToString();

    /// <summary>
    /// Ask GitHub whether a newer stable release exists. Returns its version when
    /// one is available (and remembers it for <see cref="DownloadAndRestartAsync"/>),
    /// or null when up to date or unsupported.
    /// </summary>
    public async Task<string?> CheckAsync()
    {
        if (!IsSupported) return null;
        _pending = await _manager.CheckForUpdatesAsync();
        return PendingVersion;
    }

    /// <summary>
    /// Download the pending update and restart into it. No-op when nothing is
    /// pending. This does not return on success: the process is replaced.
    /// </summary>
    public async Task DownloadAndRestartAsync()
    {
        if (_pending is null) return;
        await _manager.DownloadUpdatesAsync(_pending);
        _manager.ApplyUpdatesAndRestart(_pending);
    }
}
