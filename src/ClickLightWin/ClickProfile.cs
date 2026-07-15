using System.Text.Json.Serialization;

namespace ClickLightWin;

/// <summary>
/// A saved visual setup: the pulse size, duration, colors, and visibility toggles,
/// under a user-given name. Deliberately excludes hotkeys, launch-at-login, and menu
/// layout, matching the macOS ClickProfile so setups move cleanly between machines.
/// </summary>
public sealed class ClickProfile
{
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public double BaseDiameterDips { get; set; }
    public double PulseDurationMs { get; set; }
    public double PulseIntensity { get; set; } = 1.0;
    public string LeftColorHex { get; set; } = "";
    public string RightColorHex { get; set; } = "";
    public string MiddleColorHex { get; set; } = "";
    public string AnnotationColorHex { get; set; } = "";
    public bool ShowDrag { get; set; }
    public bool ShowRelease { get; set; }
    public bool ShowLaserPointer { get; set; }
    public bool EnableAnnotations { get; set; }
    public bool ShowShortcuts { get; set; }

    /// <summary>Local, human-friendly creation time for the list subtitle.</summary>
    [JsonIgnore] public string CreatedDisplay => $"Created {CreatedUtc.ToLocalTime():g}";

    /// <summary>Snapshot the visual settings of <paramref name="s"/> under a name.</summary>
    public static ClickProfile Capture(string name, Settings s, DateTime nowUtc)
    {
        var profile = new ClickProfile { Name = name };
        profile.SnapshotFrom(s, nowUtc);
        return profile;
    }

    /// <summary>Overwrite this profile's visual values from <paramref name="s"/> (update in place).</summary>
    public void SnapshotFrom(Settings s, DateTime nowUtc)
    {
        CreatedUtc = nowUtc;
        BaseDiameterDips = s.BaseDiameterDips;
        PulseDurationMs = s.PulseDurationMs;
        PulseIntensity = s.PulseIntensity;
        LeftColorHex = s.LeftColorHex;
        RightColorHex = s.RightColorHex;
        MiddleColorHex = s.MiddleColorHex;
        AnnotationColorHex = s.AnnotationColorHex;
        ShowDrag = s.ShowDrag;
        ShowRelease = s.ShowRelease;
        ShowLaserPointer = s.ShowLaserPointer;
        EnableAnnotations = s.EnableAnnotations;
        ShowShortcuts = s.ShowShortcuts;
    }

    /// <summary>Push this profile's visual values onto <paramref name="s"/> (e.g. the draft).</summary>
    public void ApplyTo(Settings s)
    {
        s.BaseDiameterDips = BaseDiameterDips;
        s.PulseDurationMs = PulseDurationMs;
        s.PulseIntensity = PulseIntensity;
        s.LeftColorHex = LeftColorHex;
        s.RightColorHex = RightColorHex;
        s.MiddleColorHex = MiddleColorHex;
        s.AnnotationColorHex = AnnotationColorHex;
        s.ShowDrag = ShowDrag;
        s.ShowRelease = ShowRelease;
        s.ShowLaserPointer = ShowLaserPointer;
        s.EnableAnnotations = EnableAnnotations;
        s.ShowShortcuts = ShowShortcuts;
    }
}
