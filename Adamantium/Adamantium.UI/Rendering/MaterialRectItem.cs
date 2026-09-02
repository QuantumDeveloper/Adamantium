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
    /// .y = transform-table slot; .z = the element's own alpha; .w = opacity slot.
    ///
    /// <para>.z used to carry the material KIND, which nothing read: both the pass and the source are chosen on the CPU,
    /// so by the time a fragment runs there is nothing left to branch on.</para></summary>
    public Vector4F Params;

    /// <summary>The four corner radii: x = TL, y = TR, z = BR, w = BL.</summary>
    public Vector4F Radii;

    /// <summary>The tint laid over the capture, straight RGBA. <c>.w</c> is the tint's STRENGTH, not the element's
    /// alpha: at 0 the material is clear glass, at 1 it is a painted panel.</summary>
    public Vector4F Tint;

    /// <summary>.x = extra blur radius in device px, .y = grain amount, .z = refraction in device px (LiquidGlass only),
    /// .w = 1 when Source is pinned to the ELEMENT (the shader then takes its coordinates from the shape).</summary>
    public Vector4F Knobs;

    /// <summary>The pen, in the same three slots every other SDF batch bakes it into (see RectBatchCollector.BakeStroke),
    /// so the shared CompositeFillStroke draws it: colour, then .x width / .y alignment / .zw dash run, then dash offset,
    /// trim and flags. Zero width = no pen.</summary>
    public Vector4F StrokeColor;
    public Vector4F Stroke0;
    public Vector4F Stroke1;

    /// <summary>.x = the ROUNDED CLIP's slot, or -1; .yzw spare. Its own field rather than one of Stroke1's unused
    /// components: those are the pen's dash/trim/flags, unused only because this batch bakes solid pens today.</summary>
    public Vector4F Clip;

    /// <summary>SURFACES only (velvet, metal): .rgb what the surface is made of - the cloth's own colour, or the
    /// metal's reflectance at face-on incidence - and .a the grain's scale in device px.
    /// <para>Three fields of their own rather than borrowed room in <see cref="Tint"/> and <see cref="Knobs"/>. Those
    /// hold a tint and a blur, and a field that means one thing for one material and something else for another is the
    /// shape of bug this batch has already produced once. Shared across the surface BRANCH on purpose, though: velvet
    /// and steel describe one lit surface and differ only in the BRDF that reads it.</para></summary>
    public Vector4F Surface;

    /// <summary>SURFACES only: .rgb what the surface answers the light with - the grazing sheen for cloth, the studio
    /// environment for metal - and .a its roughness.</summary>
    public Vector4F Response;

    /// <summary>SURFACES only: .x grain direction in radians, .y light angle in radians, .z light elevation (0 grazing,
    /// 1 straight on), .w the FIGURE CODE - which way a board was sawn, plus whether it is varnished.
    ///
    /// <para>.w used to hold an anisotropy that nothing read, and that could never have worked on the mesh carrier at
    /// all: there the fourth component of this field is the rounded clip's slot. So on THIS carrier it was free, and
    /// the wood took it.</para>
    ///
    /// <para>Packed into a spare component rather than given a field of its own, and NOT because that is tidier - a
    /// thirteenth field was written, measured and works on its own. But adding it while a wood PASS exists loses the
    /// device every time, and neither alone does; the mechanism is not understood and is recorded in the tech debt.
    /// Reusing a component that is already there keeps the record at the size that is known to be safe.</para></summary>
    public Vector4F Light;
}
