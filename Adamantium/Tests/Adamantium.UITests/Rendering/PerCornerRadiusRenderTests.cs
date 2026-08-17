using System;
using System.Runtime.InteropServices;
using Adamantium.Vulkan.Core;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A rounded rect's four corners are INDEPENDENT: each carries its own radius in the instance record and the shader
/// picks the one belonging to the fragment's own corner. Before this, a rect whose corners differed left the SDF batch
/// entirely and was tessellated per unit - a different class of cost for the commonest shape in a UI (a tab head, a
/// card with a flat bottom, a grouped button).
/// <para>Asserted three ways, because each can pass while another is broken: the corner that was asked to round is the
/// only one cut; the shape is the MIRROR of itself when the radii are mirrored (an arc-length or quadrant mix-up shows
/// up here and nowhere else); and the batched picture agrees with the tessellated one away from the AA edge.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class PerCornerRadiusRenderTests
{
    private const int Dim = 64;
    private const int Inset = 4;    // how far inside a corner the probe sits - past any AA, well inside a 24px arc
    private const double Radius = 24;

    private static byte[] Render(CornerRadius corners, Pen pen = null, bool batched = true)
    {
        var wasEnabled = RectBatchCollector.Enabled;
        RectBatchCollector.Enabled = batched;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            stage.RenderAction = s => s.DrawRectangle(Brushes.White, new Rect(0, 0, Dim, Dim), corners, pen);

            var root = new VisualRoot(stage, Dim, Dim);
            Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();

            using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var bytes = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            RectBatchCollector.Enabled = wasEnabled;
        }
    }

    private static bool IsLit(byte[] px, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return px[i] > 128 || px[i + 1] > 128 || px[i + 2] > 128;
    }

    // The four probes, one just inside each corner. A corner with radius 24 has no paint at (4,4); a square one does.
    private static (bool tl, bool tr, bool br, bool bl) Corners(byte[] px) => (
        IsLit(px, Inset, Inset),
        IsLit(px, Dim - 1 - Inset, Inset),
        IsLit(px, Dim - 1 - Inset, Dim - 1 - Inset),
        IsLit(px, Inset, Dim - 1 - Inset));

    [Test]
    public void OneRoundedCorner_CutsThatCornerAndNoOther()
    {
        var px = Render(new CornerRadius(Radius, 0, 0, 0));
        var c = Corners(px);

        Assert.Multiple(() =>
        {
            Assert.That(c.tl, Is.False, "the top-left corner was asked to round - it must be cut away");
            Assert.That(c.tr, Is.True, "the top-right corner was left square and must stay painted");
            Assert.That(c.br, Is.True, "the bottom-right corner was left square and must stay painted");
            Assert.That(c.bl, Is.True, "the bottom-left corner was left square and must stay painted");
        });
    }

    [Test]
    public void EachCornerIsAddressedByItsOwnValue()
    {
        var px = Render(new CornerRadius(Radius, 0, Radius, 0));
        var c = Corners(px);

        Assert.Multiple(() =>
        {
            Assert.That(c.tl, Is.False, "top-left was rounded");
            Assert.That(c.br, Is.False, "bottom-right was rounded - the two must not be confused with each other");
            Assert.That(c.tr, Is.True, "top-right was square");
            Assert.That(c.bl, Is.True, "bottom-left was square");
        });
    }

    // A quadrant mix-up survives the probes above (it still cuts SOME corner) but not this: mirroring which corners are
    // rounded must mirror the picture, pixel for pixel. The stroke is on, so the arc-length traversal is exercised too -
    // it walks corner by corner now, and a wrong anchor would show as an asymmetry here.
    [Test]
    public void MirroredRadii_DrawTheMirroredShape()
    {
        var pen = new Pen(Brushes.Red, 3);
        var left = Render(new CornerRadius(Radius, 0, 0, Radius), pen);
        var right = Render(new CornerRadius(0, Radius, Radius, 0), pen);

        var differing = 0;
        for (var y = 0; y < Dim; y++)
        {
            for (var x = 0; x < Dim; x++)
            {
                var a = (y * Dim + x) * 4;
                var b = (y * Dim + (Dim - 1 - x)) * 4;
                if (left[a] != right[b] || left[a + 1] != right[b + 1] || left[a + 2] != right[b + 2]) differing++;
            }
        }

        Assert.That(differing, Is.Zero, "rounding the other two corners must draw the mirror image");
    }

    // The batch and the tessellator must round the SAME shape - they clamp each corner to half the shorter side, and
    // nothing else. Probed away from the outline so a pixel of AA disagreement is not mistaken for a different shape.
    [Test]
    public void BatchedAndTessellatedAgreeOnWhichCornerIsCut()
    {
        var corners = new CornerRadius(Radius, 0, Radius, 0);
        var batched = Corners(Render(corners));
        var perUnit = Corners(Render(corners, batched: false));

        Assert.That(batched, Is.EqualTo(perUnit), "the SDF and the tessellated path must cut the same corners");
    }

    // NEGATIVE: an out-of-range radius is not an error and must not be rejected - it is CLAMPED, the way the tessellator
    // clamps it. Asking for 10x the box rounds the corner as far as the box allows and no further, so the opposite,
    // square corner still paints.
    [Test]
    public void OversizedRadius_IsClampedNotRefused()
    {
        var payload = new RectanglePayload(Brushes.White, new Rect(0, 0, Dim, Dim), new CornerRadius(Dim * 10, 0, 0, 0), null);
        Assert.That(RectBatchCollector.WantsBatch(payload), Is.True,
            "an oversized corner is a value to clamp, not a reason to leave the batch");

        var c = Corners(Render(new CornerRadius(Dim * 10, 0, 0, 0)));
        Assert.Multiple(() =>
        {
            Assert.That(c.tl, Is.False, "the huge corner is cut, clamped to half the box");
            Assert.That(c.br, Is.True, "and it must not spill into the corner that was never rounded");
        });
    }

    // A CAPSULE is not a primitive of its own: it is this rect with every corner rounded as far as the box allows, and
    // the clamp is what makes asking for "as round as possible" land exactly on the stadium. Probed at the ends' middle
    // (which a capsule paints) and at the corners (which it cuts).
    [Test]
    public void EveryCornerRoundedToTheLimit_IsACapsule()
    {
        var px = Render(new CornerRadius(Dim));   // more than the box allows on purpose - the clamp does the rest
        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, Dim / 2, Dim / 2), Is.True, "the middle is inside any capsule");
            Assert.That(IsLit(px, 2, Dim / 2), Is.True, "the left end's waist is painted - that is the round cap");
            Assert.That(IsLit(px, Dim - 3, Dim / 2), Is.True, "and so is the right end's");
            Assert.That(IsLit(px, 1, 1), Is.False, "no corner survives a capsule");
        });
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
