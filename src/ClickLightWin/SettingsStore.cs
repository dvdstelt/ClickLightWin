using System.IO;
using System.Text.Json;

namespace ClickLightWin;

/// <summary>
/// Loads and saves <see cref="Settings"/> as JSON under
/// %APPDATA%\ClickLightWin\settings.json. Missing or unreadable state falls back
/// to defaults rather than throwing, so a corrupt file never blocks startup.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickLightWin");
        _path = Path.Combine(dir, "settings.json");
    }

    public Settings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings();
        }
        catch
        {
            // Corrupt or unreadable file: fall back to defaults.
        }
        return new Settings();
    }

    public void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Best-effort persistence; never crash the app over a failed write.
        }
    }
}
