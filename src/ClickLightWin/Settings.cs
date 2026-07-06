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
    private double _baseDiameterDips = 28;
    private double _pulseDurationMs = 450;

    // ---- Persisted, user-editable -------------------------------------------

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ShowDrag { get => _showDrag; set => Set(ref _showDrag, value); }
    public double BaseDiameterDips { get => _baseDiameterDips; set => Set(ref _baseDiameterDips, value); }
    public double PulseDurationMs { get => _pulseDurationMs; set => Set(ref _pulseDurationMs, value); }

    // ---- Computed render constants (not persisted, not user-editable yet) ----

    [JsonIgnore] public double MaxScale => 2.2;
    [JsonIgnore] public double StrokeThickness => 3;
    [JsonIgnore] public Duration PulseDuration => new(TimeSpan.FromMilliseconds(_pulseDurationMs));

    [JsonIgnore] public double DragDotDiameter => 10;
    [JsonIgnore] public Duration DragDuration => new(TimeSpan.FromMilliseconds(360));
    [JsonIgnore] public double DragMinSpacingDips => 6;
    [JsonIgnore] public Color DragColor => Color.FromRgb(0xEB, 0xD6, 0x38); // yellow

    public Color ColorFor(ClickButton button) => button switch
    {
        ClickButton.Left => Color.FromRgb(0x3B, 0x82, 0xF6),   // blue
        ClickButton.Right => Color.FromRgb(0xF9, 0x73, 0x16),  // orange
        ClickButton.Middle => Color.FromRgb(0x22, 0xC5, 0x5E), // green
        _ => Colors.White
    };

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
