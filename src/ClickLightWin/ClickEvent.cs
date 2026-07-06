namespace ClickLightWin;

/// <summary>Which mouse button produced a <see cref="ClickEvent"/>.</summary>
public enum ClickButton
{
    Left,
    Right,
    Middle
}

/// <summary>The phase of a mouse interaction: press, release, drag-with-button-held, or move.</summary>
public enum ClickPhase
{
    Down,
    Up,
    Drag,
    Move
}

/// <summary>
/// A single mouse event delivered by the low-level hook. Coordinates are in
/// physical virtual-screen pixels (as the hook reports them); the overlay maps
/// them to monitor-local DIPs at draw time. Mirrors ClickEvent.swift.
/// </summary>
public readonly record struct ClickEvent(ClickButton Button, ClickPhase Phase, int ScreenX, int ScreenY);
