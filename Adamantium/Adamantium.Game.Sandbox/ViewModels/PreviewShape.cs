namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Which figure the live brush stands paint their brush onto. A brush is not tied to a shape: the rectangle,
/// the ellipse and the regular polygon each go through their own SDF batch when the fill is a plain colour, and the same
/// shapes fall back to tessellated geometry the moment the fill is a brush - which is what these stands paint. The star
/// is tessellated either way, so switching between them is also the demo that the SAME brush rides every path.</summary>
public enum PreviewShape
{
    Rectangle,
    Ellipse,
    Polygon,
    Star
}
