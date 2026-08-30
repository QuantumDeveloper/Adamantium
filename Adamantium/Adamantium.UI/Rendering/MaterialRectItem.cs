using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the BACKDROP MATERIAL batch (BrushEffect.fx, technique Material): a shape whose fill is made from
/// what was already drawn behind it. Matches the shader's <c>MaterialRectData</c> field for field.
///
/// <para>Like the textured batch, the SOURCE is not in the record: one image is bound per SEGMENT, and so is the
/// rectangle that maps fragments into it (the effect's <c>SourceRect</c>). Neither belongs to an instance - a draw binds
/// one image - and keeping the rectangle out of the record is what lets it be recomputed at DRAW time, which a mica
/// pane depends on: its rectangle is where the desktop put the wallpaper, and it changes when the WINDOW moves, without
/// anything in the recorded frame changing at all.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MaterialRectItem
{
    /// <summary>Node-local bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius (device px; NEGATIVE flags ellipse / regular polygon, as in the other SDF batches);
    /// .y = transform-table slot; .z = material kind; .w = opacity slot.</summary>
    public Vector4F Params;

    /// <summary>The four corner radii: x = TL, y = TR, z = BR, w = BL.</summary>
    public Vector4F Radii;

    /// <summary>The tint laid over the capture, straight RGBA. <c>.w</c> is the tint's STRENGTH, not the element's
    /// alpha: at 0 the material is clear glass, at 1 it is a painted panel.</summary>
    public Vector4F Tint;

    /// <summary>.x = extra blur radius in device px, .y = grain amount, .z = refraction in device px (LiquidGlass only),
    /// .w spare.</summary>
    public Vector4F Knobs;
}
