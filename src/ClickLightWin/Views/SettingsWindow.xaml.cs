using System.Windows;

namespace ClickLightWin.Views;

/// <summary>
/// A normal (non-overlay) window that binds two-way to the live <see cref="Settings"/>,
/// so edits apply to the overlays immediately. Persistence happens when the window
/// closes. Maps to SettingsWindowController.swift / ClickLightSettingsView.swift.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
