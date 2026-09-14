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

// INK: a stroke drawn as ONE shape, whose fragment asks how far it is from the whole polyline. An InkBrush fill routes
// here.
//
// The one collector in this family whose payload is not one instance: a stroke writes a HEADER record plus one record
// per point, and only the header draws - the points are data its fragment shader reads. So a rectangle turns into as
// many instances as the stroke has points, all in the one draw call the clip group already had.
//
// Its OWN effect, like the materials and the grid - see the note at the top of InkEffect.fx for what this replaced and
// why one capsule per segment could not be it.
internal sealed class InkCollector : SdfBatchCollector<InkSegmentItem>
{
    public static bool Enabled = true;

    private InkEffect Effect;

    // SMALL to start with, and it grows. A collector is made for every render cache - including every off-screen one a
    // bake or a test opens - and its buffer is allocated whether or not anything ever draws through it. Asking for four
    // thousand segments up front took the device out of memory partway through a run: thirty off-screen tests failed on
    // AllocateMemory, none of them anything to do with ink.
    public InkCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.InkSegmentsPass;

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new InkEffect(device);
        ProjectionParam = Effect.Projection;
        ViewportSizeParam = Effect.ViewportSize;
        InstancesAddressParam = Effect.InstancesAddress;
        TransformsAddressParam = Effect.TransformsAddress;
    }

    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not InkBrush ink) return false;

        // A pen would be a border around the ink, which is not a thing ink has. Nothing to draw is not an error either:
        // a stroke with one point is a dot, which is a segment from a point to itself.
        return p.Pen == null && ink.Count > 0;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        if (p.Brush is not InkBrush ink || ink.Points == null) return false;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        var points = Math.Max(1, Math.Min(ink.Count, ink.Points.Length));

        // ONE header, then the points. Only the header draws; the points are read by its fragment shader and draw
        // nothing - see the layout note at the top of InkEffect.fx.
        EnsureCpuCapacity(Count + 1 + points);
        if (Count + 1 + points > GpuCapacity) return false;

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        var colour = Straight(ink.Color, (float)(opacity * ink.Opacity));
        var half = (float)(Math.Max(ink.Thickness, 0.1) * 0.5 * Math.Abs(sx));

        var header = Count++;
        var first = Count;

        // The BOX the stroke covers, in the same node-local units the points are written in: the header's quad, and the
        // only thing its fragment has to cover.
        var lowX = float.MaxValue;
        var lowY = float.MaxValue;
        var highX = float.MinValue;
        var highY = float.MinValue;

        for (var i = 0; i < points; i++)
        {
            var x = ink.Points[i].X * sx + tx;
            var y = ink.Points[i].Y * sy + ty;

            if (x < lowX) lowX = x;
            if (y < lowY) lowY = y;
            if (x > highX) highX = x;
            if (y > highY) highY = y;

            Items[Count++] = new InkSegmentItem { Segment = new Vector4F(x, y, 0, 0) };
        }

        Items[header] = new InkSegmentItem
        {
            Segment = new Vector4F(lowX, lowY, highX, highY),
            Params = new Vector4F(half, transformSlot, fadeSlot, 1),   // .w 1 = this record draws
            // RELATIVE to the header, never absolute. The draw offsets the buffer address by the first instance of the
            // run it is flushing, so inside the shader index 0 is the run's start and not the array's - an absolute
            // index is only right while a run happens to begin at zero, and reads somebody else's record as soon as one
            // does not.
            Clip = new Vector4F(clipSlot, points, first - header, 0),
            Color = colour
        };

        MarkPending(scissor, logicalBounds);

        return true;
    }

    /// <summary>Bake one unit into the patch stage - see BatchArena. Ink does not patch: a stroke is many instances and
    /// the stage repairs one, so a stroke that changed is re-recorded like anything else.</summary>
    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot, int ownerTag, int clipSlot = -1)
        => false;

    private static Vector4F Straight(Color colour, float opacity) =>
        new(colour.R / 255f, colour.G / 255f, colour.B / 255f, colour.A / 255f * opacity);
}
