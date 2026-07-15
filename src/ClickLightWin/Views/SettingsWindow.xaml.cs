using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

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
    private readonly ProfileStore _profiles;
    private readonly ActivityStore _activity;
    private bool _suppressProfileLoad;
    private Point _menuDragStart;
    private MenuLayoutEntry? _menuDragItem;

    /// <summary>True when the user closed with OK; the caller then applies the draft.</summary>
    public bool Committed { get; private set; }

    public SettingsWindow(Settings settings, ProfileStore profiles, ActivityStore activity)
    {
        InitializeComponent();
        DataContext = settings;
        _profiles = profiles;
        _activity = activity;
        ProfileCombo.ItemsSource = profiles.Profiles;
        // Reflect the current profile without reloading it, so any unsaved live tweaks
        // that differ from the profile's snapshot are preserved on open.
        SelectProfileByName(settings.CurrentProfileName);
        PopulateActivity();
        _panes = [PaneGeneral, PaneEvents, PaneStyle, PaneShortcuts, PaneProfiles, PaneActivity, PaneMenu];
        Nav.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Committed = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

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

    private ClickProfile? CurrentProfile => ProfileCombo.SelectedItem as ClickProfile;

    private static bool IsDefault(ClickProfile p) =>
        string.Equals(p.Name, ProfileStore.DefaultProfileName, StringComparison.OrdinalIgnoreCase);

    // The user picked another profile: load its snapshot into the draft (previews in
    // the pad, commits on OK).
    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileLoad) return;
        if (CurrentProfile is not { } p || DataContext is not Settings s) return;
        p.ApplyTo(s);
        s.CurrentProfileName = p.Name;
        UpdateDeleteButton();
        ShowProfileStatus($"Loaded \"{p.Name}\".");
    }

    // Update the currently selected profile in place with the current draft.
    private void OnSaveChanges(object sender, RoutedEventArgs e)
    {
        if (CurrentProfile is not { } p || DataContext is not Settings s) return;
        _profiles.Save(p.Name, s);
        ShowProfileStatus($"Saved changes to \"{p.Name}\".");
    }

    // Create a new profile from the current draft and make it the current one.
    private void OnCreateProfile(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Settings s) return;
        var name = NewProfileName.Text.Trim();
        if (name.Length == 0) return;
        var existed = _profiles.Contains(name);
        var profile = _profiles.Save(name, s);
        NewProfileName.Clear();
        SelectProfile(profile);
        s.CurrentProfileName = profile.Name;
        ShowProfileStatus(existed ? $"Updated \"{name}\"." : $"Created \"{name}\".");
    }

    // Delete the selected profile (never Default) and fall back to Default, keeping the
    // current draft settings.
    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (CurrentProfile is not { } p || IsDefault(p) || DataContext is not Settings s) return;
        var name = p.Name;
        _suppressProfileLoad = true;
        _profiles.Delete(p); // removing the item clears the combo selection
        SelectProfileByName(ProfileStore.DefaultProfileName);
        s.CurrentProfileName = ProfileStore.DefaultProfileName;
        ShowProfileStatus($"Deleted \"{name}\".");
    }

    private void SelectProfileByName(string name)
    {
        var match = _profiles.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? _profiles.Profiles.FirstOrDefault();
        SelectProfile(match);
    }

    private void SelectProfile(ClickProfile? profile)
    {
        _suppressProfileLoad = true;
        ProfileCombo.SelectedItem = profile;
        _suppressProfileLoad = false;
        UpdateDeleteButton();
    }

    private void UpdateDeleteButton() =>
        DeleteProfileButton.IsEnabled = CurrentProfile is { } p && !IsDefault(p);

    private void OnExportProfiles(object sender, RoutedEventArgs e)
    {
        if (_profiles.Profiles.Count == 0) { ShowProfileStatus("No profiles to export."); return; }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "ClickLight Profiles.json",
            Filter = "JSON files (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _profiles.Export(dialog.FileName);
            ShowProfileStatus($"Exported {_profiles.Profiles.Count} profile(s).");
        }
        catch
        {
            ShowProfileStatus("Export failed.");
        }
    }

    private void OnImportProfiles(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var count = _profiles.Import(dialog.FileName);
            ShowProfileStatus($"Imported {count} profile(s).");
        }
        catch
        {
            ShowProfileStatus("Import failed. Is it a ClickLight profiles file?");
        }
    }

    private void ShowProfileStatus(string text)
    {
        ProfileStatus.Text = text;
        ProfileStatus.Visibility = Visibility.Visible;
    }

    // Fill the Activity pane: today's totals plus a 7-day bar chart built by hand.
    private void PopulateActivity()
    {
        var days = _activity.LastSevenDays();
        var today = days[^1];
        TodayTotal.Text = today.TotalClicks.ToString();
        MetricLeft.Text = today.Left.ToString();
        MetricRight.Text = today.Right.ToString();
        MetricMiddle.Text = today.Middle.ToString();
        MetricDrags.Text = today.Drags.ToString();

        var accent = (Brush)FindResource("Accent");
        var muted = (Brush)FindResource("Muted");

        ActivityChart.Children.Clear();
        var max = Math.Max(1, days.Max(d => d.TotalClicks));
        const double maxBar = 96;

        foreach (var day in days)
        {
            var column = new Grid { Margin = new Thickness(5, 0, 5, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var count = new TextBlock
            {
                Text = day.TotalClicks.ToString(),
                Foreground = muted,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(count, 0);

            var height = day.TotalClicks == 0 ? 3 : Math.Max(6, day.TotalClicks / (double)max * maxBar);
            var bar = new Border
            {
                Height = height,
                Background = accent,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(bar, 1);

            var label = new TextBlock
            {
                Text = day.Label,
                Foreground = muted,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(label, 2);

            column.Children.Add(count);
            column.Children.Add(bar);
            column.Children.Add(label);
            ActivityChart.Children.Add(column);
        }
    }

    private void OnResetActivity(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Remove all click counts stored on this PC?",
            "Reset activity history", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;
        _activity.Reset();
        PopulateActivity();
        ActivityStatus.Text = "Activity history cleared.";
        ActivityStatus.Visibility = Visibility.Visible;
    }

    // ---- Menu Layout drag-to-reorder ----

    private void OnMenuItemPreviewDown(object sender, MouseButtonEventArgs e)
    {
        _menuDragStart = e.GetPosition(null);
        _menuDragItem = (e.OriginalSource as FrameworkElement)?.DataContext as MenuLayoutEntry;
    }

    private void OnMenuItemPreviewMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _menuDragItem is null) return;
        var pos = e.GetPosition(null);
        // Only begin a drag once past the system threshold, so a plain click still
        // toggles the checkbox instead of starting a reorder.
        if (Math.Abs(pos.X - _menuDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _menuDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(MenuList, _menuDragItem, DragDropEffects.Move);
        _menuDragItem = null;
    }

    private void OnMenuItemDrop(object sender, DragEventArgs e)
    {
        if (_menuDragItem is null || DataContext is not Settings s) return;
        var target = (e.OriginalSource as FrameworkElement)?.DataContext as MenuLayoutEntry;
        var from = s.MenuLayout.IndexOf(_menuDragItem);
        var to = target is null ? s.MenuLayout.Count - 1 : s.MenuLayout.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to) s.MenuLayout.Move(from, to);
        _menuDragItem = null;
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
