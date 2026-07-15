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
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path));
                if (loaded is not null) return Normalize(loaded);
            }
        }
        catch
        {
            // Corrupt or unreadable file: fall back to defaults.
        }
        return new Settings();
    }

    /// <summary>
    /// Snap persisted numeric values to the nearest preset so hand-edited files or
    /// values from before the preset UI still select a segment in the settings window.
    /// </summary>
    internal static Settings Normalize(Settings settings)
    {
        settings.BaseDiameterDips = Nearest(Presets.Sizes, settings.BaseDiameterDips);
        settings.PulseDurationMs = Nearest(Presets.Durations, settings.PulseDurationMs);
        TrayMenu.Normalize(settings.MenuLayout); // fill in any items added since this file was written
        return settings;
    }

    internal static double Nearest(NumericPreset[] presets, double value) =>
        presets.MinBy(p => Math.Abs(p.Value - value))!.Value;

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
