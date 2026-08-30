using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the BACKDROP MATERIAL batch (BrushEffect.fx, technique Material): a shape whose fill is made from
/// what was already drawn behind it. Matches the shader's <c>MaterialRectData</c> field for field.
///
/// <para>Like the textured batch, the SOURCE is not in the record: one capture is bound per SEGMENT, because a capture
/// is a copy of one region of the frame and two elements in different places cannot share it. Unlike the textured
/// batch, that source is not an asset the application supplied - it is produced during the frame, immediately before
/// this segment draws (see <see cref="BackdropCapture"/>).</para>
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

    /// <summary>Where the capture came from, in DEVICE pixels of the frame: x, y, w, h. The pixel shader maps a fragment
    /// back into the copy with it, which is a subtraction and a divide - the capture is stated in the same space
    /// SV_Position already arrives in.</summary>
    public Vector4F CaptureRect;
}
