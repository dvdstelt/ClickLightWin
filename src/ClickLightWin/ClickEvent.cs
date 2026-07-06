namespace ClickLightWin;

/// <summary>Which mouse button produced a <see cref="ClickEvent"/>.</summary>
public enum ClickButton
{
    Left,
    Right,
    Middle
}

/// <summary>The phase of a mouse interaction: press, release, or drag-with-button-held.</summary>
public enum ClickPhase
{
    Down,
    Up,
    Drag
}

/// <summary>
/// A single mouse event delivered by the low-level hook. Coordinates are in
/// physical virtual-screen pixels (as the hook reports them); the overlay maps
/// them to monitor-local DIPs at draw time. Mirrors ClickEvent.swift.
/// </summary>
public readonly record struct ClickEvent(ClickButton Button, ClickPhase Phase, int ScreenX, int ScreenY);
