using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A BORDER is its own primitive: a fill plus a ring of its own thickness on each side, composited from two outlines in
/// ONE SDF pass. A pen cannot express it - a pen is one width offset from a contour - so unequal sides used to leave the
/// batch for a per-unit CombinedGeometry ring, which is a different class of cost for the commonest chrome in a theme
/// AND over-blended the outline it shares with the fill.
/// <para>Asserted where each half can fail on its own: the sides are honoured INDEPENDENTLY (a mix-up of left for top
/// survives any single-side test), the ring is the border's colour while the middle stays the fill's, and the batched
/// picture agrees with the tessellated fallback about where the ring is.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class BorderFrameRenderTests
{
    private const int Dim = 64;

    private static byte[] Render(Thickness thickness, CornerRadius corners, bool batched = true)
        => Render(thickness, corners, Brushes.Blue, Brushes.Red, batched);

    private static byte[] Render(Thickness thickness, CornerRadius corners, Brush fill, Brush border, bool batched)
    {
        var wasEnabled = RectBatchCollector.Enabled;
        RectBatchCollector.Enabled = batched;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            stage.RenderAction = s => s.DrawBorder(fill, new Rect(0, 0, Dim, Dim), corners, border, thickness);

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

    // BGRA on this target: the border is red, the fill blue, the ground black. Named rather than compared by channel at
    // each site - a test that says "red" and means "channel 2 above 128" is a test nobody can check.
    private static string ColourAt(byte[] px, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        var b = px[i]; var g = px[i + 1]; var r = px[i + 2];
        if (r > 128 && b < 96) return "border";
        if (b > 128 && r < 96) return "fill";
        if (r < 32 && g < 32 && b < 32) return "ground";
        return $"mixed({r},{g},{b})";
    }

    // How deep the border reaches on one side, measured along a line through the middle of that side.
    private static int BorderRunFromLeft(byte[] px, int y)
    {
        var run = 0;
        while (run < Dim && ColourAt(px, run, y) == "border") run++;
        return run;
    }

    private static int BorderRunFromTop(byte[] px, int x)
    {
        var run = 0;
        while (run < Dim && ColourAt(px, x, run) == "border") run++;
        return run;
    }

    [Test]
    public void AUniformBorder_RingsTheShapeAndLeavesTheFillInside()
    {
        var px = Render(new Thickness(6), CornerRadius.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(ColourAt(px, Dim / 2, 2), Is.EqualTo("border"), "top edge");
            Assert.That(ColourAt(px, Dim / 2, Dim - 3), Is.EqualTo("border"), "bottom edge");
            Assert.That(ColourAt(px, 2, Dim / 2), Is.EqualTo("border"), "left edge");
            Assert.That(ColourAt(px, Dim - 3, Dim / 2), Is.EqualTo("border"), "right edge");
            Assert.That(ColourAt(px, Dim / 2, Dim / 2), Is.EqualTo("fill"), "and the middle is the fill, not the border");
        });
    }

    // The point of the whole exercise: four sides, four numbers, no pen could carry them.
    [Test]
    public void EachSideKeepsItsOwnThickness()
    {
        var px = Render(new Thickness(2, 10, 2, 10), CornerRadius.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(BorderRunFromTop(px, Dim / 2), Is.EqualTo(10).Within(1), "top was asked for 10");
            Assert.That(BorderRunFromLeft(px, Dim / 2), Is.EqualTo(2).Within(1), "left was asked for 2");
        });
    }

    // NEGATIVE, and the one that catches a left/top mix-up: an ASYMMETRIC pair. Thickness(a, b) is (left+top,
    // right+bottom) in this engine, so these four are all different on purpose.
    [Test]
    public void TheSidesAreNotConfusedWithEachOther()
    {
        var px = Render(new Thickness(3, 12, 3, 12), CornerRadius.Empty);
        var top = BorderRunFromTop(px, Dim / 2);
        var left = BorderRunFromLeft(px, Dim / 2);

        Assert.That(top, Is.GreaterThan(left),
            $"the thick pair is top/bottom - measured top={top}, left={left}; equal or swapped means the sides are read in the wrong order");
    }

    // A border with unequal sides has to BATCH now - that is the whole gain. If it fell back, the picture would still be
    // right and the cost would be silently a class higher, which is exactly the regression nobody notices.
    [Test]
    public void AnUnequalBorder_IsTakenByTheBatch()
    {
        var payload = new RectanglePayload(Brushes.Blue, new Rect(0, 0, Dim, Dim), new CornerRadius(8),
            Brushes.Red, new Thickness(2, 10, 2, 10));

        Assert.That(RectBatchCollector.WantsBatch(payload), Is.True,
            "four thicknesses ride in the instance - there is nothing here to tessellate per unit");
    }

    // The fallback exists for a rotated world, and it must cut the SAME ring: both paths deflate the box by the sides and
    // shrink each corner by the thicker of its two. Probed away from the outlines, so a pixel of AA disagreement between
    // an SDF edge and a tessellated one is not mistaken for a different shape.
    [Test]
    public void BatchedAndTessellatedPutTheRingInTheSamePlace()
    {
        var thickness = new Thickness(4, 12, 4, 12);
        var corners = new CornerRadius(10, 0, 6, 0);

        var batched = Render(thickness, corners);
        var perUnit = Render(thickness, corners, batched: false);

        int[] xs = [2, Dim / 2, Dim - 3];
        int[] ys = [2, Dim / 2, Dim - 3];
        Assert.Multiple(() =>
        {
            foreach (var x in xs)
            {
                foreach (var y in ys)
                {
                    Assert.That(ColourAt(batched, x, y), Is.EqualTo(ColourAt(perUnit, x, y)), $"at ({x},{y})");
                }
            }
        });
    }

    // A TRANSLUCENT border is the case the old ring got wrong. Drawn as its own shape it blended its two coincident
    // edges twice and every corner darkened; drawn from one field it is one layer, so a half-alpha red over black must
    // read as exactly half red - not three quarters.
    [Test]
    public void ATranslucentBorder_BlendsOnce()
    {
        var half = new SolidColorBrush(new Color(255, 0, 0, 128));
        var px = Render(new Thickness(8), CornerRadius.Empty, Brushes.Transparent, half, batched: true);

        var i = ((Dim / 2) * Dim + 3) * 4;   // inside the left side, away from any corner
        Assert.That(px[i + 2], Is.EqualTo(128).Within(6),
            $"half-alpha red over black is ~128, twice-blended would be ~192 - got {px[i + 2]}");
    }

    // The CORNER is where a twice-blended ring showed worst: the two edges meet, and a shape that blends per edge counts
    // that pixel twice. One field cannot - so the corner has to read the same as the straight run.
    [Test]
    public void ATranslucentBordersCorner_IsNoDarkerThanItsSides()
    {
        var half = new SolidColorBrush(new Color(255, 0, 0, 128));
        var px = Render(new Thickness(8), CornerRadius.Empty, Brushes.Transparent, half, batched: true);

        var side = px[((Dim / 2) * Dim + 3) * 4 + 2];
        var corner = px[(3 * Dim + 3) * 4 + 2];
        Assert.That(corner, Is.EqualTo(side).Within(6),
            $"the corner must be the same one layer as the side - side={side}, corner={corner}");
    }

    // And the border does not pick up the element's OWN fill: the fill stops where the border starts (WPF's rule), so a
    // translucent border shows what is BEHIND the element, not its background tinting the ring from underneath.
    [Test]
    public void ATranslucentBorder_DoesNotShowTheFillThroughItself()
    {
        var half = new SolidColorBrush(new Color(255, 0, 0, 128));
        var px = Render(new Thickness(8), CornerRadius.Empty, Brushes.Blue, half, batched: true);

        var i = ((Dim / 2) * Dim + 3) * 4;
        Assert.That(px[i], Is.LessThan(24), $"no blue may show through the ring - got b={px[i]}");
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
