using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ClickLightWin;

/// <summary>
/// Loads, holds, and persists saved <see cref="ClickProfile"/>s as JSON under
/// %APPDATA%\ClickLightWin\profiles.json. The list is observable so the settings
/// window updates live. Missing or unreadable state falls back to an empty list
/// rather than throwing. Mirrors the macOS ClickProfileStore.
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>The built-in profile that always exists and cannot be deleted.</summary>
    public const string DefaultProfileName = "Default";

    private readonly string _path;

    public ObservableCollection<ClickProfile> Profiles { get; } = [];

    public ProfileStore()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickLightWin", "profiles.json");
        Load();
        EnsureDefault();
    }

    /// <summary>Guarantee a "Default" profile exists (factory look) at the top of the list.</summary>
    private void EnsureDefault()
    {
        if (Profiles.Any(p => string.Equals(p.Name, DefaultProfileName, StringComparison.OrdinalIgnoreCase)))
            return;
        Profiles.Insert(0, ClickProfile.Capture(DefaultProfileName, new Settings(), DateTime.UtcNow));
        Persist();
    }

    /// <summary>True when a profile with this name (case-insensitive) already exists.</summary>
    public bool Contains(string name) =>
        Profiles.Any(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Save the current visual settings under a name. If a profile with that name
    /// already exists it is replaced (updated in place); otherwise a new one is added.
    /// </summary>
    public ClickProfile Save(string name, Settings settings)
    {
        name = name.Trim();
        var existing = Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.SnapshotFrom(settings, DateTime.UtcNow); // update in place, keeping the same instance
            Persist();
            return existing;
        }
        var profile = ClickProfile.Capture(name, settings, DateTime.UtcNow);
        Profiles.Add(profile);
        Persist();
        return profile;
    }

    /// <summary>Delete a profile. The built-in Default is permanent and never removed.</summary>
    public void Delete(ClickProfile profile)
    {
        if (string.Equals(profile.Name, DefaultProfileName, StringComparison.OrdinalIgnoreCase)) return;
        if (Profiles.Remove(profile)) Persist();
    }

    /// <summary>Write all profiles to an arbitrary path (Export). Throws on failure.</summary>
    public void Export(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(Profiles, Options));

    /// <summary>Read profiles from a file and append them. Returns how many were added.</summary>
    public int Import(string path)
    {
        var imported = JsonSerializer.Deserialize<List<ClickProfile>>(File.ReadAllText(path));
        if (imported is null) return 0;
        foreach (var profile in imported) Profiles.Add(profile);
        Persist();
        return imported.Count;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var loaded = JsonSerializer.Deserialize<List<ClickProfile>>(File.ReadAllText(_path));
            if (loaded is null) return;
            foreach (var profile in loaded) Profiles.Add(profile);
        }
        catch
        {
            // Corrupt or unreadable file: start with no profiles.
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Profiles, Options));
        }
        catch
        {
            // Best-effort persistence; never crash the app over a failed write.
        }
    }
}
