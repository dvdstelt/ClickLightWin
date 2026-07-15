using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ClickLightWin.Views;

/// <summary>
/// A sidebar-navigation settings window (General / Event Visibility / Visual Style /
/// Keyboard Shortcuts / Profiles / Activity / Menu Layout) that binds two-way to the
/// live <see cref="Settings"/>, so edits apply to the overlays immediately.
/// Persistence happens when the window closes. Maps to ClickLightSettingsView.swift.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly StackPanel[] _panes;
    private readonly Random _rng = new();

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        DataContext = settings;
        _panes = [PaneGeneral, PaneEvents, PaneStyle, PaneShortcuts, PaneProfiles, PaneActivity, PaneMenu];
        Nav.SelectedIndex = 0;
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = Nav.SelectedIndex;
        for (var i = 0; i < _panes.Length; i++)
            _panes[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnResetShortcut(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Settings settings || sender is not FrameworkElement { Tag: string which }) return;
        switch (which)
        {
            case "toggle": settings.ToggleHotKey = HotKeyBinding.DefaultToggle; break;
            case "clear": settings.ClearHotKey = HotKeyBinding.DefaultClear; break;
            case "draw": settings.DrawModeHotKey = HotKeyBinding.DefaultDrawMode; break;
        }
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Settings s) return;
        var confirm = MessageBox.Show(
            "Restore size, duration, colors, and visibility toggles to their defaults?",
            "Reset ClickLight settings", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        var d = new Settings();
        s.BaseDiameterDips = d.BaseDiameterDips;
        s.PulseDurationMs = d.PulseDurationMs;
        s.LeftColorHex = d.LeftColorHex;
        s.RightColorHex = d.RightColorHex;
        s.MiddleColorHex = d.MiddleColorHex;
        s.AnnotationColorHex = d.AnnotationColorHex;
        s.ShowDrag = d.ShowDrag;
        s.ShowRelease = d.ShowRelease;
        s.ShowLaserPointer = d.ShowLaserPointer;
        s.EnableAnnotations = d.EnableAnnotations;
        s.ShowShortcuts = d.ShowShortcuts;
    }

    // Pick random presets and colors, in the spirit of the macOS "Randomize" button.
    private void OnRandomize(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Settings s) return;
        var colors = Palette.Colors;
        s.BaseDiameterDips = Presets.Sizes[_rng.Next(Presets.Sizes.Length)].Value;
        s.PulseDurationMs = Presets.Durations[_rng.Next(Presets.Durations.Length)].Value;
        s.LeftColorHex = colors[_rng.Next(colors.Length)].Hex;
        s.RightColorHex = colors[_rng.Next(colors.Length)].Hex;
        s.MiddleColorHex = colors[_rng.Next(colors.Length)].Hex;
    }
}
