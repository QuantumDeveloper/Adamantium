using Adamantium.Engine.EntityServices;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

/// <summary>
/// Outlines what is selected, together with everything under it, so the part of a model being changed stands apart from
/// the rest; and, in another color, what a click would select under the pointer. Works with whatever tool is in use.
/// </summary>
public class SelectionOutline : EditorProcessor
{
    public Vector4F Color { get; set; } = Colors.Orange.ToVector4();

    public Vector4F HoverColor { get; set; } = Colors.White.ToVector4();

    /// <summary>How wide the outline is, in points: pixels at 100% scale.</summary>
    public float Pixels { get; set; } = 2;

    public override void DrawOverlay(EditorOverlayProcessor overlay)
    {
        var selected = Tools.Selection.Current;
        if (Tools.Hovered is { } hovered && hovered != selected)
        {
            overlay.DrawOutline(hovered, HoverColor, Pixels);
        }

        if (selected != null)
        {
            overlay.DrawOutline(selected, Color, Pixels);
        }
    }
}
