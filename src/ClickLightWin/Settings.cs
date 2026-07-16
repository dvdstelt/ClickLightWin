using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
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
    private bool _enableAnnotations = true;
    private bool _showShortcuts = false;
    private double _baseDiameterDips = 32;  // Medium preset
    private double _pulseDurationMs = 480;  // Normal preset
    private double _pulseIntensity = 1.0;   // Normal preset (matches the original look)
    private string _leftColorHex = "#3B82F6";   // blue
    private string _rightColorHex = "#F97316";  // orange
    private string _middleColorHex = "#22C55E"; // green
    private string _annotationColorHex = "#EF4444"; // red (shared by arrows and boxes)
    private string _laserOuterHex = "#FF2905";      // laser outer ring (red)
    private string _laserInnerHex = "#FFFFFF";      // laser inner core (white)
    private HotKeyBinding _toggleHotKey = HotKeyBinding.DefaultToggle;
    private HotKeyBinding _clearHotKey = HotKeyBinding.DefaultClear;
    private HotKeyBinding _drawModeHotKey = HotKeyBinding.DefaultDrawMode;
    private HotKeyBinding _shortcutsHotKey = HotKeyBinding.DefaultShortcuts;
    private string _currentProfileName = ProfileStore.DefaultProfileName;
    private ShortcutPosition _shortcutPosition = ShortcutPosition.BottomCenter;
    private ShortcutSize _shortcutSize = ShortcutSize.Medium;

    // ---- Persisted, user-editable -------------------------------------------

    /// <summary>Bump when the persisted shape changes, so future loads can migrate.</summary>
    public int SchemaVersion { get; set; } = 1;

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ShowDrag { get => _showDrag; set => Set(ref _showDrag, value); }
    public bool ShowRelease { get => _showRelease; set => Set(ref _showRelease, value); }
    public bool ShowLaserPointer { get => _showLaserPointer; set => Set(ref _showLaserPointer, value); }
    public bool EnableAnnotations { get => _enableAnnotations; set => Set(ref _enableAnnotations, value); }
    public bool ShowShortcuts { get => _showShortcuts; set => Set(ref _showShortcuts, value); }
    public ShortcutPosition ShortcutPosition { get => _shortcutPosition; set => Set(ref _shortcutPosition, value); }
    public ShortcutSize ShortcutSize { get => _shortcutSize; set => Set(ref _shortcutSize, value); }
    public double BaseDiameterDips { get => _baseDiameterDips; set => Set(ref _baseDiameterDips, value); }
    public double PulseDurationMs { get => _pulseDurationMs; set => Set(ref _pulseDurationMs, value); }
    public double PulseIntensity { get => _pulseIntensity; set => Set(ref _pulseIntensity, value); }
    public string LeftColorHex { get => _leftColorHex; set => Set(ref _leftColorHex, value); }
    public string RightColorHex { get => _rightColorHex; set => Set(ref _rightColorHex, value); }
    public string MiddleColorHex { get => _middleColorHex; set => Set(ref _middleColorHex, value); }
    public string AnnotationColorHex { get => _annotationColorHex; set => Set(ref _annotationColorHex, value); }
    public string LaserOuterHex { get => _laserOuterHex; set => Set(ref _laserOuterHex, value); }
    public string LaserInnerHex { get => _laserInnerHex; set => Set(ref _laserInnerHex, value); }
    public HotKeyBinding ToggleHotKey { get => _toggleHotKey; set => Set(ref _toggleHotKey, value); }
    public HotKeyBinding ClearHotKey { get => _clearHotKey; set => Set(ref _clearHotKey, value); }
    public HotKeyBinding DrawModeHotKey { get => _drawModeHotKey; set => Set(ref _drawModeHotKey, value); }
    public HotKeyBinding ShortcutsHotKey { get => _shortcutsHotKey; set => Set(ref _shortcutsHotKey, value); }

    /// <summary>Name of the profile currently selected in the settings window.</summary>
    public string CurrentProfileName { get => _currentProfileName; set => Set(ref _currentProfileName, value); }

    /// <summary>Order and visibility of the system-tray menu items (Quit always shown).</summary>
    public ObservableCollection<MenuLayoutEntry> MenuLayout { get; set; } = TrayMenu.DefaultLayout();

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
    // Outer ring and inner core are user-set; the middle is the blend of the two,
    // so the defaults (red outer, white inner) reproduce the original salmon middle.
    [JsonIgnore] public Color LaserColor => ParseHex(_laserOuterHex);
    [JsonIgnore] public Color LaserCoreColor => ParseHex(_laserInnerHex);
    [JsonIgnore] public Color LaserMidColor => Blend(LaserColor, LaserCoreColor);
    [JsonIgnore] public double LaserGlowDiameter => 26; // soft outer aura
    [JsonIgnore] public double LaserRedDiameter => 14;  // solid red disc
    [JsonIgnore] public double LaserMidDiameter => 8;   // salmon disc
    [JsonIgnore] public double LaserCoreDiameter => 4;  // small white center
    [JsonIgnore] public double LaserStrokeThickness => 5;
    [JsonIgnore] public double LaserMinSpacingDips => 3;
    [JsonIgnore] public int LaserIdleFadeMs => 130;      // hide the halo after movement stops
    [JsonIgnore] public Duration LaserStrokeFade => new(TimeSpan.FromMilliseconds(900));

    // Annotations (arrows and boxes): a persistent colored shape with a dark
    // outline for contrast on any background. Color is shared by both shapes.
    [JsonIgnore] public Color AnnotationColor => ParseHex(_annotationColorHex);
    [JsonIgnore] public double AnnotationMinLengthDips => 12; // ignore tiny accidental drags
    [JsonIgnore] public double ArrowThickness => 4;
    [JsonIgnore] public double ArrowHeadLength => 18;
    [JsonIgnore] public double ArrowHeadWidth => 15;
    [JsonIgnore] public double BoxThickness => 3;
    [JsonIgnore] public double BoxCornerRadius => 3;

    // Live shortcut display: a bottom-center stack of key-cap pills that hold, then fade.
    [JsonIgnore] public double ShortcutFontSize => ShortcutDisplay.FontSize(_shortcutSize);
    [JsonIgnore] public Duration ShortcutHold => new(TimeSpan.FromMilliseconds(1100));
    [JsonIgnore] public Duration ShortcutFade => new(TimeSpan.FromMilliseconds(350));
    [JsonIgnore] public int ShortcutStackMax => 6;

    /// <summary>A detached copy of the user-editable settings, for editing in a draft.</summary>
    public Settings Clone()
    {
        var copy = new Settings();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>
    /// Copy all persisted, user-editable values from <paramref name="other"/> into this
    /// instance through the setters, so bindings, overlays, and hotkeys react. Used to
    /// commit a settings-window draft back onto the live settings.
    /// </summary>
    public void CopyFrom(Settings other)
    {
        SchemaVersion = other.SchemaVersion;
        Enabled = other.Enabled;
        ShowDrag = other.ShowDrag;
        ShowRelease = other.ShowRelease;
        ShowLaserPointer = other.ShowLaserPointer;
        EnableAnnotations = other.EnableAnnotations;
        ShowShortcuts = other.ShowShortcuts;
        ShortcutPosition = other.ShortcutPosition;
        ShortcutSize = other.ShortcutSize;
        BaseDiameterDips = other.BaseDiameterDips;
        PulseDurationMs = other.PulseDurationMs;
        PulseIntensity = other.PulseIntensity;
        LeftColorHex = other.LeftColorHex;
        RightColorHex = other.RightColorHex;
        MiddleColorHex = other.MiddleColorHex;
        AnnotationColorHex = other.AnnotationColorHex;
        LaserOuterHex = other.LaserOuterHex;
        LaserInnerHex = other.LaserInnerHex;
        ToggleHotKey = other.ToggleHotKey;
        ClearHotKey = other.ClearHotKey;
        DrawModeHotKey = other.DrawModeHotKey;
        ShortcutsHotKey = other.ShortcutsHotKey;
        CurrentProfileName = other.CurrentProfileName;

        // Deep-copy the menu layout into this instance's own collection (kept, not
        // replaced, so any UI bound to it stays live).
        MenuLayout.Clear();
        foreach (var entry in other.MenuLayout) MenuLayout.Add(entry.Clone());
    }

    public Color ColorFor(ClickButton button) => ParseHex(button switch
    {
        ClickButton.Left => _leftColorHex,
        ClickButton.Right => _rightColorHex,
        ClickButton.Middle => _middleColorHex,
        _ => "#FFFFFF"
    });

    private static Color Blend(Color a, Color b) =>
        Color.FromRgb((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));

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
