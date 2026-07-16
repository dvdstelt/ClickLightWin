namespace ClickLightWin;

/// <summary>Where the live keyboard-shortcut pills appear.</summary>
public enum ShortcutPosition
{
    BottomCenter,
    NearPointer
}

/// <summary>How large the live keyboard-shortcut pills are.</summary>
public enum ShortcutSize
{
    Small,
    Medium,
    Large
}

/// <summary>A titled choice for the shortcut-display segmented pickers.</summary>
public sealed record ShortcutOption(string Title, object Value);

/// <summary>Options and sizing for the live keyboard-shortcut display.</summary>
public static class ShortcutDisplay
{
    public static readonly ShortcutOption[] Positions =
    [
        new("Bottom Center", ShortcutPosition.BottomCenter),
        new("Near Pointer", ShortcutPosition.NearPointer)
    ];

    public static readonly ShortcutOption[] Sizes =
    [
        new("Small", ShortcutSize.Small),
        new("Medium", ShortcutSize.Medium),
        new("Large", ShortcutSize.Large)
    ];

    /// <summary>Cap font size for each size choice (Medium 15 matches the original).</summary>
    public static double FontSize(ShortcutSize size) => size switch
    {
        ShortcutSize.Small => 13,
        ShortcutSize.Large => 18,
        _ => 15
    };
}
