namespace ClickLightWin;

/// <summary>Which shape a Ctrl(+Shift) gesture draws.</summary>
public enum AnnotationTool
{
    Arrow,       // Ctrl+Shift + left-drag  (permanent)
    Box,         // Ctrl+Shift + right-drag (permanent)
    LaserStroke  // Ctrl + left-drag        (temporary, fades; drawn by the laser renderer)
}

/// <summary>The phase of an annotation gesture.</summary>
public enum AnnotationPhase
{
    Begin,  // button down
    Update, // dragging
    Commit  // button up
}

/// <summary>
/// A Ctrl+Shift annotation gesture from the mouse hook, in physical virtual-screen
/// pixels. Arrows (and later boxes) are committed shapes that persist on the overlay
/// until cleared, unlike the transient click pulses.
/// </summary>
public readonly record struct AnnotationEvent(AnnotationTool Tool, AnnotationPhase Phase, int ScreenX, int ScreenY);
