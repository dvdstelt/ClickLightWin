using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ClickLightWin;

/// <summary>
/// Tracks per-day click tallies, persisted to %APPDATA%\ClickLightWin\activity.json.
/// Everything stays on this machine. Records are cheap in-memory increments; the file
/// is written on a throttle by the caller (and on exit). Mirrors ClickActivityStore.
/// </summary>
public sealed class ActivityStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<string, ClickActivityDay> _days = [];
    private bool _dirty;

    public ActivityStore()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickLightWin", "activity.json");
        Load();
    }

    private static string Key(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private ClickActivityDay Today()
    {
        var key = Key(DateTime.Now);
        if (!_days.TryGetValue(key, out var day))
        {
            day = new ClickActivityDay { Date = key };
            _days[key] = day;
        }
        return day;
    }

    public void RecordClick(ClickButton button)
    {
        var day = Today();
        switch (button)
        {
            case ClickButton.Left: day.Left++; break;
            case ClickButton.Right: day.Right++; break;
            case ClickButton.Middle: day.Middle++; break;
        }
        _dirty = true;
    }

    public void RecordDrag()
    {
        Today().Drags++;
        _dirty = true;
    }

    /// <summary>The seven days ending today, oldest first, with gaps filled by empty days.</summary>
    public IReadOnlyList<ClickActivityDay> LastSevenDays()
    {
        var list = new List<ClickActivityDay>(7);
        for (var offset = 6; offset >= 0; offset--)
        {
            var key = Key(DateTime.Now.AddDays(-offset));
            list.Add(_days.TryGetValue(key, out var day) ? day : new ClickActivityDay { Date = key });
        }
        return list;
    }

    public void Reset()
    {
        _days.Clear();
        _dirty = true;
        Save();
    }

    /// <summary>Write to disk if anything changed since the last save. Best-effort.</summary>
    public void Save()
    {
        if (!_dirty) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_days.Values, Options));
            _dirty = false;
        }
        catch
        {
            // Never crash the app over a failed activity write.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var loaded = JsonSerializer.Deserialize<List<ClickActivityDay>>(File.ReadAllText(_path));
            if (loaded is null) return;
            foreach (var day in loaded) _days[day.Date] = day;
        }
        catch
        {
            // Corrupt or unreadable file: start with no history.
        }
    }
}
