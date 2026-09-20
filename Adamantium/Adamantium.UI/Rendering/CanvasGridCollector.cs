using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// The canvas grid: ONE rectangle whose every pixel decides for itself, from the world coordinate under it, whether it
// is on a mark. A CanvasGridBrush fill routes here.
//
// Unlike every sibling in this family, this collector is not really a batch - a window holds one canvas, maybe two, so
// what it saves is not the cost of many draws but the cost of MANY MARKS. The canvas used to emit a rectangle per mark:
// about a thousand a frame at 1:1, growing as viewport area over pitch squared. Here that is one quad and nothing is
// generated for the grid at all.
//
// Its OWN effect, not the brushes'. Putting these shaders in BrushEffect is what killed vkCreateShadersEXT on the
// gradient pass once already - the driver's shader-object compiler has a ceiling per effect. See the note at the top of
// MaterialEffect.fx, which is a third effect for exactly this reason.
internal sealed class CanvasGridCollector : SdfBatchCollector<CanvasGridItem>
{
    public static bool Enabled = true;

    private GridEffect Effect;

    // Four, not the thousand the other collectors size for: a window has a canvas, not a wall of them.
    public CanvasGridCollector() : base(4) { }

    protected override IEffectPass DrawPass => Effect.CanvasGridMarksPass;

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new GridEffect(device);
        ProjectionParam = Effect.Projection;
        ViewportSizeParam = Effect.ViewportSize;
        InstancesAddressParam = Effect.InstancesAddress;
        TransformsAddressParam = Effect.TransformsAddress;
    }

    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not CanvasGridBrush) return false;

        // No pen and no corners: the grid is the GROUND an element stands on. A stroke or a rounded corner on it would
        // be chrome, and chrome belongs to whatever frames the canvas, drawn as itself.
        return p.Pen == null;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, fadeSlot, out var item)) return false;

        // The SLOT only: .yz carry where the axes are, and stamping the whole vector took them away with it.
        item.Clip = new Vector4F(clipSlot, item.Clip.Y, item.Clip.Z, 0);
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Bake one grid WITHOUT appending it - the paint fast-path re-bakes an existing slot in place, which is
    /// what every pan and every step of a zoom is: the camera moved, the element did not.</summary>
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        out CanvasGridItem item)
    {
        item = default;
        if (p.Brush is not CanvasGridBrush grid) return false;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        var dest = p.DestinationRect;
        item.Bounds = new Vector4F((float)(dest.X * sx + tx), (float)(dest.Y * sy + ty),
            (float)(dest.Width * sx), (float)(dest.Height * sy));

        item.Params = new Vector4F(transformSlot, (float)grid.Marks, fadeSlot, (float)Math.Max(grid.MarkSize, 0.5));

        // THE CAMERA NEVER LEAVES THE ORIGIN, and what the grid is given is a PHASE - never how far anybody has
        // travelled. Handing over "where the world's origin sits on screen" put a number that grows without bound into
        // a float32: a million pixels out, the gap between one float and the next is a quarter of a pixel, sixteen
        // million out it is two - so neighbouring fragments resolved to the SAME world point. Dots smeared into lines,
        // and a pan stepped the lattice instead of sliding it. Nothing is wrong with the arithmetic that got us there:
        // it is all double, and it stays exact. It is the handover that cannot carry the number.
        //
        // The grid is PERIODIC, so it does not want that number: everything it draws repeats every coarse cell, and
        // reducing the offset by whole cells - IN DOUBLE, where it is still exact - leaves the picture identical and
        // hands the shader a value that never exceeds one cell.
        var scale = Math.Max(grid.Scale, 1e-6);
        var period = Math.Max(grid.Spacing, 1e-6) * Math.Max(grid.Coarsening, 2) * scale;

        item.Camera = new Vector4F((float)Phase(grid.Offset.X, period), (float)Phase(grid.Offset.Y, period),
            (float)scale, 0);
        // The pitch as a RECIPROCAL: the shader must not divide - adding a division to it is what stopped the driver
        // creating the pass at all - so the one division happens here, once per bake.
        item.Step = new Vector4F((float)Math.Max(grid.Spacing, 1e-6), (float)Math.Max(grid.Coarsening, 2),
            (float)(1.0 / Math.Max(grid.MinPitch, 1)), 0);

        var alpha = (float)(opacity * grid.Opacity);
        item.Background = Straight(grid.Background, alpha);
        item.GridColor = Straight(grid.Color, alpha);
        item.AxisColor = Straight(grid.AxisColor, alpha);

        // THE AXES are the one thing that does want to know where the origin is - they ARE the origin - and they are
        // the reason the offset cannot simply be dropped. Kept, but PENNED IN: past the element there is no fragment
        // for an axis to cover, so a value beyond it says everything a larger one would and stays exact in a float.
        // Far from home the axes are off screen, which is the truth; near home the number is small and untouched.
        var reach = Math.Abs(dest.Width) + Math.Abs(dest.Height) + 64;

        // -1, never 0: zero is a valid clip slot belonging to somebody else. Stamped by TryAdd/TryStage, which keep
        // the axis in place.
        item.Clip = new Vector4F(-1, (float)Penned(grid.Offset.X, reach), (float)Penned(grid.Offset.Y, reach), 0);
        return true;
    }

    // Where the lattice stands within ONE cell. The grid repeats every cell, so this is the whole of what the shader
    // needs; taken in double, where the distance travelled is still exact.
    private static double Phase(double offset, double period)
    {
        var wrapped = offset - Math.Floor(offset / period) * period;

        return Double.IsFinite(wrapped) ? wrapped : 0;
    }

    private static double Penned(double offset, double reach) =>
        Double.IsFinite(offset) ? Math.Clamp(offset, -reach, reach) : 0;

    /// <summary>Bake one unit into the patch stage - see BatchArena.</summary>
    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot, int ownerTag, int clipSlot = -1)
    {
        if (unit is not RenderUnits.RectangleRenderUnit u || !CanBatch(u.RectPayload)) return false;
        if (!BakeItem(u.RectPayload, world, u.FillOpacity, transformSlot, unit.FadeSlot, out var item)) return false;

        // The SLOT only: .yz carry where the axes are, and stamping the whole vector took them away with it.
        item.Clip = new Vector4F(clipSlot, item.Clip.Y, item.Clip.Z, 0);
        Stage.Add(item);
        return true;
    }

    private static Vector4F Straight(Color color, float opacity) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f * opacity);
}
