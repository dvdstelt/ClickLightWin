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
    private double _baseDiameterDips = 32;  // Medium preset
    private double _pulseDurationMs = 480;  // Normal preset
    private string _leftColorHex = "#3B82F6";   // blue
    private string _rightColorHex = "#F97316";  // orange
    private string _middleColorHex = "#22C55E"; // green

    // ---- Persisted, user-editable -------------------------------------------

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ShowDrag { get => _showDrag; set => Set(ref _showDrag, value); }
    public double BaseDiameterDips { get => _baseDiameterDips; set => Set(ref _baseDiameterDips, value); }
    public double PulseDurationMs { get => _pulseDurationMs; set => Set(ref _pulseDurationMs, value); }
    public string LeftColorHex { get => _leftColorHex; set => Set(ref _leftColorHex, value); }
    public string RightColorHex { get => _rightColorHex; set => Set(ref _rightColorHex, value); }
    public string MiddleColorHex { get => _middleColorHex; set => Set(ref _middleColorHex, value); }

    // ---- Computed render constants (not persisted, not user-editable yet) ----

    [JsonIgnore] public double MaxScale => 2.2;
    [JsonIgnore] public double StrokeThickness => 3;
    [JsonIgnore] public Duration PulseDuration => new(TimeSpan.FromMilliseconds(_pulseDurationMs));

    [JsonIgnore] public double DragDotDiameter => 10;
    [JsonIgnore] public Duration DragDuration => new(TimeSpan.FromMilliseconds(360));
    [JsonIgnore] public double DragMinSpacingDips => 6;
    [JsonIgnore] public Color DragColor => Color.FromRgb(0xEB, 0xD6, 0x38); // yellow

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
