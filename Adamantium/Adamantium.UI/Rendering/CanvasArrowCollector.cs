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

// An ARROW, and a plain LINE with it: ONE quad whose every pixel asks how far it is from the shaft and from either head
// at once. A CanvasArrowBrush fill routes here.
//
// A line is the same record with nothing on its ends, deliberately: the two differ by a number in a field, and giving
// them two paths would be two places for the same shaft to be drawn slightly differently.
//
// What this replaces, and why it is a pass and not a fix. An arrow was a stroked line plus a tessellated mesh per head:
//   - a mesh handed to the renderer is read LATER, so one kept and rewritten can be read mid-write, which is a head
//     drawn where the arrow used to be and a flicker at every move;
//   - the barbs met at the tip in a MITRE, which on a sharp corner throws a spike far past the point it closes, and at
//     a fat thickness stopped reading as an arrow at all;
//   - three shapes over the same pixels BLEND three times, so a translucent arrow had dark seams at the head.
// One distance, one coverage, one blend, and nothing kept between frames answers all three at once.
//
// Its OWN effect, not the shapes' or the brushes'. The driver's shader-object compiler has a ceiling per effect, and
// adding shaders to one that works has killed vkCreateShadersEXT before - see the note at the top of InkEffect.fx.
internal sealed class CanvasArrowCollector : SdfBatchCollector<CanvasArrowItem>
{
    public static bool Enabled = true;

    private ArrowEffect Effect;

    // A drawing holds arrows by the dozen, not by the thousand, and every render cache - including every off-screen one
    // a bake or a test opens - allocates its collectors whether or not anything draws through them.
    public CanvasArrowCollector() : base(64) { }

    protected override IEffectPass DrawPass => Effect.ArrowRunPass;

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new ArrowEffect(device);
        ProjectionParam = Effect.Projection;
        ViewportSizeParam = Effect.ViewportSize;
        InstancesAddressParam = Effect.InstancesAddress;
        TransformsAddressParam = Effect.TransformsAddress;
    }

    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not CanvasArrowBrush arrow) return false;

        // A pen would be a border round the arrow, which is not a thing an arrow has - its own thickness is the shape.
        return p.Pen == null && arrow.Thickness > 0;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, fadeSlot, out var item)) return false;

        item.Clip = new Vector4F(clipSlot, 0, 0, 0);
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Bake one arrow WITHOUT appending it - the paint fast-path re-bakes an existing slot in place, which is
    /// what every pan and every step of a zoom is: the camera moved, the arrow did not.</summary>
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        out CanvasArrowItem item)
    {
        item = default;
        if (p.Brush is not CanvasArrowBrush arrow) return false;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        item.Ends = new Vector4F(
            (float)(arrow.From.X * sx + tx), (float)(arrow.From.Y * sy + ty),
            (float)(arrow.To.X * sx + tx), (float)(arrow.To.Y * sy + ty));

        // Scaled by the SAME factor the ends are, so the shaft keeps its proportion to its own length under any zoom.
        var scale = Math.Abs(sx);

        item.Params = new Vector4F(
            (float)(Math.Max(arrow.Thickness, 1e-6) * 0.5 * scale), transformSlot, fadeSlot,
            (float)(Math.Max(arrow.HeadLength, 0) * scale));

        item.Head = new Vector4F((float)(Math.Max(arrow.HeadWidth, 0) * scale),
            Math.Clamp(arrow.StartHead, 0, 2), Math.Clamp(arrow.EndHead, 0, 2), 0);

        item.Color = Straight(arrow.Color, (float)(opacity * arrow.Opacity));

        // -1, never 0: zero is a valid clip slot belonging to somebody else. Stamped by TryAdd/TryStage.
        item.Clip = new Vector4F(-1, 0, 0, 0);
        return true;
    }

    /// <summary>Bake one unit into the patch stage - see BatchArena.</summary>
    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot, int ownerTag, int clipSlot = -1)
    {
        if (unit is not RenderUnits.RectangleRenderUnit u || !CanBatch(u.RectPayload)) return false;
        if (!BakeItem(u.RectPayload, world, u.FillOpacity, transformSlot, unit.FadeSlot, out var item)) return false;

        item.Clip = new Vector4F(clipSlot, 0, 0, 0);
        Stage.Add(item);
        return true;
    }

    private static Vector4F Straight(Color color, float opacity) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f * opacity);
}
