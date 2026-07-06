namespace ClickLightWin;

/// <summary>Which annotation shape a Ctrl+Shift gesture draws (by mouse button).</summary>
public enum AnnotationTool
{
    Arrow, // left-drag
    Box    // right-drag (not drawn yet; reserved for a later milestone)
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
