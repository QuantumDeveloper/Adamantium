namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Which figure the live brush stands paint their brush onto. A brush is not tied to a shape: the rectangle and
/// the ellipse go through their own SDF batches while the triangle is tessellated geometry, so switching between them is
/// also the demo that the SAME brush rides all three paths.</summary>
public enum PreviewShape
{
    Rectangle,
    Ellipse,
    Triangle,
    Star
}
