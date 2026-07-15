using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace ClickLightWin;

/// <summary>Identifies a configurable system-tray menu item.</summary>
public enum TrayMenuItem
{
    Enabled,
    LaserPointer,
    ReleaseRing,
    DragTrail,
    KeyboardShortcuts,
    Size,
    Duration,
    ClearAnnotations,
    LaunchAtLogin,
    Settings,
    About,
    Quit
}

/// <summary>One tray menu item's placement: which item it is, and whether it is shown.</summary>
public sealed class MenuLayoutEntry
{
    public TrayMenuItem Item { get; set; }
    public bool Visible { get; set; } = true;

    [JsonIgnore] public string Label => TrayMenu.Label(Item);
    [JsonIgnore] public bool CanHide => Item != TrayMenuItem.Quit; // Quit is always shown

    public MenuLayoutEntry Clone() => new() { Item = Item, Visible = Visible };
}

/// <summary>The set of tray menu items, their labels, and the default order.</summary>
public static class TrayMenu
{
    // Default order, matching the original hard-coded menu.
    public static readonly TrayMenuItem[] DefaultOrder =
    [
        TrayMenuItem.Enabled, TrayMenuItem.LaserPointer, TrayMenuItem.ReleaseRing,
        TrayMenuItem.DragTrail, TrayMenuItem.KeyboardShortcuts, TrayMenuItem.Size,
        TrayMenuItem.Duration, TrayMenuItem.ClearAnnotations, TrayMenuItem.LaunchAtLogin,
        TrayMenuItem.Settings, TrayMenuItem.About, TrayMenuItem.Quit
    ];

    public static string Label(TrayMenuItem item) => item switch
    {
        TrayMenuItem.Enabled => "Enabled",
        TrayMenuItem.LaserPointer => "Laser pointer mode",
        TrayMenuItem.ReleaseRing => "Show release ring",
        TrayMenuItem.DragTrail => "Show drag trail",
        TrayMenuItem.KeyboardShortcuts => "Show keyboard shortcuts",
        TrayMenuItem.Size => "Size",
        TrayMenuItem.Duration => "Duration",
        TrayMenuItem.ClearAnnotations => "Clear annotations",
        TrayMenuItem.LaunchAtLogin => "Launch at login",
        TrayMenuItem.Settings => "Settings...",
        TrayMenuItem.About => "About ClickLight",
        TrayMenuItem.Quit => "Quit",
        _ => item.ToString()
    };

    public static ObservableCollection<MenuLayoutEntry> DefaultLayout() =>
        new(DefaultOrder.Select(i => new MenuLayoutEntry { Item = i, Visible = true }));

    /// <summary>
    /// Ensure the layout has exactly one entry per menu item: drop unknown or duplicate
    /// entries and append any items missing (e.g. after an upgrade) in default order,
    /// always keeping Quit present and visible. Mutates the collection in place.
    /// </summary>
    public static void Normalize(ObservableCollection<MenuLayoutEntry> layout)
    {
        var seen = new HashSet<TrayMenuItem>();
        for (var i = 0; i < layout.Count;)
        {
            if (!Enum.IsDefined(layout[i].Item) || !seen.Add(layout[i].Item)) layout.RemoveAt(i);
            else i++;
        }
        foreach (var item in DefaultOrder)
            if (seen.Add(item))
                layout.Add(new MenuLayoutEntry { Item = item, Visible = true });

        var quit = layout.First(e => e.Item == TrayMenuItem.Quit);
        quit.Visible = true;
    }
}
