using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace ClickLightWin;

/// <summary>
/// The app's single settings model: observable so the settings window binds to it
/// and edits apply live to the overlays (which read it at draw time), and
/// serializable so it round-trips through <see cref="SettingsStore"/>. Only the
/// plain settable properties are persisted; the computed render constants below
/// are marked <see cref="JsonIgnoreAttribute"/>. Expands toward
/// ClickSettingOptions.swift / SettingsStore.swift as more options are exposed.
/// </summary>
public sealed class Settings : INotifyPropertyChanged
{
    private bool _enabled = true;
    private bool _showDrag = true;
    private bool _showRelease = true;
    private bool _showLaserPointer = false;
    private double _baseDiameterDips = 32;  // Medium preset
    private double _pulseDurationMs = 480;  // Normal preset
    private string _leftColorHex = "#3B82F6";   // blue
    private string _rightColorHex = "#F97316";  // orange
    private string _middleColorHex = "#22C55E"; // green

    // ---- Persisted, user-editable -------------------------------------------

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ShowDrag { get => _showDrag; set => Set(ref _showDrag, value); }
    public bool ShowRelease { get => _showRelease; set => Set(ref _showRelease, value); }
    public bool ShowLaserPointer { get => _showLaserPointer; set => Set(ref _showLaserPointer, value); }
    public double BaseDiameterDips { get => _baseDiameterDips; set => Set(ref _baseDiameterDips, value); }
    public double PulseDurationMs { get => _pulseDurationMs; set => Set(ref _pulseDurationMs, value); }
    public string LeftColorHex { get => _leftColorHex; set => Set(ref _leftColorHex, value); }
    public string RightColorHex { get => _rightColorHex; set => Set(ref _rightColorHex, value); }
    public string MiddleColorHex { get => _middleColorHex; set => Set(ref _middleColorHex, value); }

    // ---- Computed render constants (not persisted, not user-editable yet) ----

    [JsonIgnore] public double MaxScale => 2.2;
    [JsonIgnore] public double StrokeThickness => 3;
    [JsonIgnore] public Duration PulseDuration => new(TimeSpan.FromMilliseconds(_pulseDurationMs));

    // Release ring: a ring that starts wide and contracts inward as it fades,
    // shown when a held button is let go. Mirrors the leftUp/rightUp/middleUp
    // contraction in ClickOverlayView.swift.
    [JsonIgnore] public Duration ReleaseDuration => new(TimeSpan.FromMilliseconds(_pulseDurationMs * 0.7));
    [JsonIgnore] public double ReleaseStartScale => 1.7;
    [JsonIgnore] public double ReleaseEndScale => 0.5;
    // Minimum hold before a release ring is shown, so quick clicks stay clean.
    [JsonIgnore] public long ReleaseMinHoldMs => 150;

    [JsonIgnore] public double DragDotDiameter => 10;
    [JsonIgnore] public Duration DragDuration => new(TimeSpan.FromMilliseconds(360));
    [JsonIgnore] public double DragMinSpacingDips => 6;
    [JsonIgnore] public Color DragColor => Color.FromRgb(0xEB, 0xD6, 0x38); // yellow

    // Laser pointer: a glowing cursor built from concentric solid discs (soft
    // outer glow, solid red, salmon, small white core) that follows movement,
    // plus a fading freehand stroke while dragging. Colors and proportions match
    // the macOS laser in ClickOverlayView.swift / SettingsStore.swift.
    [JsonIgnore] public Color LaserColor => Color.FromRgb(0xFF, 0x29, 0x05);    // red   (macOS laserColor)
    [JsonIgnore] public Color LaserMidColor => Color.FromRgb(0xFF, 0x94, 0x82); // salmon (macOS middle)
    [JsonIgnore] public Color LaserCoreColor => Colors.White;                   // white  (macOS inner)
    [JsonIgnore] public double LaserGlowDiameter => 26; // soft outer aura
    [JsonIgnore] public double LaserRedDiameter => 14;  // solid red disc
    [JsonIgnore] public double LaserMidDiameter => 8;   // salmon disc
    [JsonIgnore] public double LaserCoreDiameter => 4;  // small white center
    [JsonIgnore] public double LaserStrokeThickness => 5;
    [JsonIgnore] public double LaserMinSpacingDips => 3;
    [JsonIgnore] public int LaserIdleFadeMs => 130;      // hide the halo after movement stops
    [JsonIgnore] public Duration LaserStrokeFade => new(TimeSpan.FromMilliseconds(900));

    public Color ColorFor(ClickButton button) => ParseHex(button switch
    {
        ClickButton.Left => _leftColorHex,
        ClickButton.Right => _rightColorHex,
        ClickButton.Middle => _middleColorHex,
        _ => "#FFFFFF"
    });

    private static Color ParseHex(string hex)
    {
        try
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.White; // tolerate a hand-edited or malformed value
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        // PulseDuration is derived from PulseDurationMs; re-notify so bindings refresh.
        if (name == nameof(PulseDurationMs))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseDuration)));
    }
}
