using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A regular polygon is one shape whose only distinguishing number is how many corners it has: 3 a triangle, enough of
/// them a circle. Its own record and its own pass, because a polygon and an ellipse share a shape of RECORD, not a shape.
/// <para>The tests pin what a "polygon" has to mean rather than what any one of them looks like: a triangle's flat side
/// cuts a corner the circle keeps (which is how you tell the two apart at all), the count actually changes the shape,
/// many corners converge on the ellipse the batch already draws, a ring hollows it, and the batch agrees with the
/// tessellated fallback about which pixels are inside - INCLUDING the rotation, since the first vertex sits on the +x
/// axis in both.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class RegularPolygonRenderTests
{
    private const int Dim = 64;
    private const int Half = Dim / 2;

    private static byte[] Render(int corners, bool batched = true, double ringThickness = 0, Pen pen = null, Brush fill = null, double startAngle = 0, Rect? rect = null)
    {
        var wasEnabled = RegularPolygonCollector.Enabled;
        RegularPolygonCollector.Enabled = batched;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            var box = rect ?? new Rect(0, 0, Dim, Dim);
            stage.RenderAction = s => s.DrawRegularPolygon(fill ?? Brushes.White, box, corners, pen, ringThickness, startAngle);

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
            RegularPolygonCollector.Enabled = wasEnabled;
        }
    }

    private static bool IsLit(byte[] px, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return px[i] > 128 || px[i + 1] > 128 || px[i + 2] > 128;
    }

    private static int LitPixels(byte[] px)
    {
        var count = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            if (px[i] > 128) count++;
        }

        return count;
    }

    [Test]
    public void ATriangle_CutsTheCornerACircleKeeps()
    {
        var triangle = Render(3);
        var manyCorners = Render(64);

        // Up and to the LEFT: a triangle with its first vertex on the +x axis has its flat side facing that way, so this
        // spot is outside it and inside anything round.
        const int off = 18;
        Assert.Multiple(() =>
        {
            Assert.That(IsLit(manyCorners, Half - off, Half - off), Is.True, "64 corners is a circle here");
            Assert.That(IsLit(triangle, Half - off, Half - off), Is.False, "and a triangle has cut that corner off");
            Assert.That(IsLit(triangle, Half + 8, Half), Is.True, "while its own middle is solid");
        });
    }

    // The count is the shape: more corners means more area, monotonically, from a triangle up to the circle it converges
    // on. A test that only checked "three corners draws something" would pass with the count ignored entirely.
    [Test]
    public void MoreCorners_MeansMoreArea_UpToTheCircle()
    {
        var triangle = LitPixels(Render(3));
        var square = LitPixels(Render(4));
        var hexagon = LitPixels(Render(6));
        var many = LitPixels(Render(64));

        Assert.Multiple(() =>
        {
            Assert.That(triangle, Is.GreaterThan(0), "a triangle has to draw something at all");
            Assert.That(square, Is.GreaterThan(triangle), "4 > 3");
            Assert.That(hexagon, Is.GreaterThan(square), "6 > 4");
            Assert.That(many, Is.GreaterThan(hexagon), "and 64 corners fills more than a hexagon");
        });
    }

    // ...and the limit is not a metaphor: enough corners must be the SAME picture as the ellipse batch's circle, which is
    // a different record, a different shader and a different pass.
    [Test]
    public void EnoughCorners_IsTheCircleTheEllipseBatchDraws()
    {
        var polygon = Render(128);

        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };
        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
        stage.RenderAction = s => s.DrawEllipse(new Rect(0, 0, Dim, Dim), Brushes.White, 0, 360,
            Adamantium.ProceduralGeometry.Shapes.EllipseType.Sector);
        Assert.That(renderer.RenderFrame(new VisualRoot(stage, Dim, Dim)), Is.True);
        RenderDirty.Clear();
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var circle = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, circle, 0, circle.Length);

        var differing = 0;
        for (var i = 0; i < circle.Length; i += 4)
        {
            if (Math.Abs(polygon[i] - circle[i]) > 8) differing++;
        }

        Assert.That(differing, Is.LessThan(40),
            $"128 corners and a circle must agree to a handful of edge pixels - differing = {differing}");
    }

    // A RING hollows the shape as geometry: a hollow triangle is a chevron, and it costs the same one instance.
    [Test]
    public void ARing_HollowsThePolygon()
    {
        var px = Render(3, ringThickness: 6);

        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, Half + 24, Half), Is.True, "the band is there where the triangle reaches furthest");
            Assert.That(IsLit(px, Half, Half), Is.False, "and the middle is a hole");
        });
    }

    // The START ANGLE is where corner 0 sits. Half a turn swaps which side is a flat edge and which is a point, so the two
    // probes trade places - a rotation that quietly did nothing would leave both readings equal.
    [Test]
    public void AStartAngle_TurnsTheShape()
    {
        var pointingRight = Render(3);
        var pointingLeft = Render(3, startAngle: 180);

        const int off = 24;
        Assert.Multiple(() =>
        {
            Assert.That(IsLit(pointingRight, Half + off, Half), Is.True, "corner 0 on the +x axis");
            Assert.That(IsLit(pointingRight, Half - off, Half), Is.False, "and the flat side opposite it");
            Assert.That(IsLit(pointingLeft, Half - off, Half), Is.True, "half a turn puts the corner on the other side");
            Assert.That(IsLit(pointingLeft, Half + off, Half), Is.False, "and the flat side where the corner was");
        });
    }

    // Turning must not resize: the angle offsets the PARAMETER, so the polygon keeps filling the box it is inscribed in.
    // Rotating the finished shape instead would swing a squashed one out of its slot and clip it.
    [Test]
    public void TurningIt_KeepsItInsideItsBox()
    {
        // A SQUASHED box is where the two readings of "turn it" part company: offsetting the parameter walks the corners
        // along the ellipse the box inscribes and the area is unchanged, while rotating the finished shape would swing it
        // out of the slot and clip it.
        var box = new Rect(4, 16, 56, 32);
        var straight = LitPixels(Render(5, rect: box));

        Assert.Multiple(() =>
        {
            foreach (var angle in new[] { 17.0, 45.0, 90.0, 233.0 })
            {
                Assert.That(LitPixels(Render(5, startAngle: angle, rect: box)), Is.EqualTo(straight).Within(60),
                    $"area at {angle} deg");
            }
        });
    }

    // A TRANSLUCENT outline is the honest test of the composite. Fill and stroke come out of ONE field and are combined
    // analytically, so the half of the band that rides over the fill must be a SINGLE layer of stroke over it - and at a
    // corner, where the outline turns and a ribbon-based stroke overlaps itself, it must be that same single layer. The
    // straight run and the corner are compared against each other AND against the arithmetic, so neither a corner that
    // blends twice nor a band that blends twice everywhere can pass.
    [Test]
    public void ATranslucentStroke_IsOneLayer_AtACornerToo()
    {
        var fill = new SolidColorBrush(new Color((byte)37, (byte)99, (byte)235, (byte)255));
        var pen = new Pen(new SolidColorBrush(new Color((byte)255, (byte)255, (byte)255, (byte)77)), 8);
        // Corner 0 at 45 deg makes the square axis-aligned: edges to cross straight on, corners on the diagonals.
        var px = Render(4, pen: pen, fill: fill, startAngle: 45, rect: new Rect(10, 10, 44, 44));

        // 0.302 white over the fill, and over the black background. One layer each.
        var overFill = new[] { 103, 146, 241 };
        var overBack = new[] { 77, 77, 77 };

        Assert.Multiple(() =>
        {
            AssertTone(px, 45, Half, overFill, "the band where it rides over the fill, on a straight edge");
            AssertTone(px, 45, 19, overFill, "...and the same band at the CORNER, where the outline turns");
            AssertTone(px, 50, Half, overBack, "the band's outer half, over the background");
            AssertTone(px, 30, Half, [37, 99, 235], "the fill itself, well inside");
        });
    }

    private static void AssertTone(byte[] px, int x, int y, int[] expected, string what)
    {
        var i = (y * Dim + x) * 4;
        int[] actual = [px[i + 2], px[i + 1], px[i]];   // the readback is BGRA
        Assert.That(actual, Is.EqualTo(expected).Within(5),
            $"{what} at ({x},{y}): expected ({string.Join(",", expected)}), was ({string.Join(",", actual)})");
    }

    // A brush is not tied to a shape. A gradient, a pattern and a noise fill must paint the SAME polygon a plain colour
    // does - and the shape stays a FIELD while they do it (see the batch test below), which is the whole point of having
    // it in the SDF family rather than tessellating it the moment the fill stops being one colour.
    // COVERED, not lit: a gradient runs to a colour whose blue channel is dark, and LitPixels reads one channel - it
    // would count the far half of the gradient as empty and call a correct picture a hole.
    private static int CoveredPixels(byte[] px)
    {
        var count = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            if (px[i] > 20 || px[i + 1] > 20 || px[i + 2] > 20) count++;
        }

        return count;
    }

    [Test]
    public void EveryBrushPaintsTheSameShape()
    {
        var solid = CoveredPixels(Render(6));

        Assert.Multiple(() =>
        {
            foreach (var (name, brush) in new (string, Brush)[]
                     { ("gradient", Gradient()), ("noise", Noise()), ("pattern", Pattern()) })
            {
                Assert.That(CoveredPixels(Render(6, fill: brush)), Is.EqualTo(solid).Within(120), $"{name} fill");
            }
        });
    }

    // ...and each of those brushes is taken by ONE of the SDF batches - the polygon does not leave the family for a
    // tessellated mesh. Stated as the batches' own rules, so this cannot drift from what the walk actually does.
    [Test]
    public void TheBrushBatchesTakeThePolygon()
    {
        var box = new Rect(0, 0, Dim, Dim);
        var gradient = new RegularPolygonPayload(Gradient(), box, 6, null);
        var noise = new RegularPolygonPayload(Noise(), box, 6, null);
        var pattern = new RegularPolygonPayload(Pattern(), box, 6, null);
        var solid = new RegularPolygonPayload(Brushes.White, box, 6, null);

        Assert.Multiple(() =>
        {
            Assert.That(GradientRectCollector.WantsBatchPolygon(gradient), Is.True, "a gradient polygon rides the gradient pass");
            Assert.That(PatternRectCollector.WantsBatchPolygon(noise), Is.True, "a noise polygon rides the pattern pass");
            Assert.That(PatternRectCollector.WantsBatchPolygon(pattern), Is.True, "...and so does a pattern one");
            // Exactly ONE batch each: a solid fill belongs to the plain polygon pass and to none of the brush siblings.
            Assert.That(RegularPolygonCollector.WantsBatch(solid), Is.True);
            Assert.That(RegularPolygonCollector.WantsBatch(gradient), Is.False, "a gradient is not a colour the solid pass can paint");
            Assert.That(GradientRectCollector.WantsBatchPolygon(solid), Is.False);
            Assert.That(PatternRectCollector.WantsBatchPolygon(solid), Is.False);
        });
    }

    private static Brush Gradient() => new LinearGradientBrush
    {
        StartPoint = new Vector2(0, 0),
        EndPoint = new Vector2(1, 1),
        GradientStops =
        {
            new GradientStop(new Color((byte)37, (byte)99, (byte)235, (byte)255), 0),
            new GradientStop(new Color((byte)250, (byte)204, (byte)21, (byte)255), 1)
        }
    };

    private static Brush Noise() => new NoiseBrush
    {
        Scale = 24, Octaves = 3, Seed = 1, Lacunarity = 2, Gain = 0.5,
        Color1 = new Color((byte)20, (byte)30, (byte)60, (byte)255),
        Color2 = new Color((byte)125, (byte)211, (byte)252, (byte)255)
    };

    private static Brush Pattern() => new PatternBrush
    {
        Pattern = PatternType.Dots,
        CellSize = 8,
        Color1 = new Color((byte)15, (byte)23, (byte)42, (byte)255),
        Color2 = new Color((byte)56, (byte)189, (byte)248, (byte)255)
    };

    [Test]
    public void APolygon_IsTakenByTheBatch()
    {
        var payload = new RegularPolygonPayload(Brushes.White, new Rect(0, 0, Dim, Dim), 5, null);
        Assert.That(RegularPolygonCollector.WantsBatch(payload), Is.True,
            "a polygon is a field like the others - there is nothing here to tessellate per unit");
    }

    // NEGATIVE: a dashed pen needs an arc length along the contour, and this record bakes none. Refused, not approximated
    // with somebody else's arc length.
    [Test]
    public void ADashedPen_IsRefused()
    {
        var payload = new RegularPolygonPayload(Brushes.White, new Rect(0, 0, Dim, Dim), 5,
            new Pen(Brushes.Red, 2, dashStrokeArray: [4.0, 2.0]));

        Assert.That(RegularPolygonCollector.WantsBatch(payload), Is.False, "a dashed polygon goes to the tessellator");
    }

    // The batch and the tessellated fallback must draw the SAME polygon - including its rotation, which is the easy part
    // to get wrong: both put the first vertex on the +x axis (Shapes.Polygon walks 2*pi*i/N from there).
    [TestCase(0.0)]
    [TestCase(37.0)]
    public void BatchedAndTessellatedAgreeOnTheShape_RotationIncluded(double startAngle)
    {
        var batched = Render(5, startAngle: startAngle);
        var perUnit = Render(5, batched: false, startAngle: startAngle);

        Assert.That(LitPixels(batched), Is.EqualTo(LitPixels(perUnit)).Within(200),
            $"area: batched={LitPixels(batched)} perUnit={LitPixels(perUnit)}");
        int[] probes = [6, Half - 12, Half, Half + 12, Dim - 7];
        Assert.Multiple(() =>
        {
            foreach (var x in probes)
            {
                foreach (var y in probes)
                {
                    Assert.That(IsLit(batched, x, y), Is.EqualTo(IsLit(perUnit, x, y)), $"at ({x},{y})");
                }
            }
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
