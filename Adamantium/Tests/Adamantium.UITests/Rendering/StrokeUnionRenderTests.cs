using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A TRANSLUCENT stroke that crosses itself must blend with the background ONCE - the crossing carries the coverage of
/// the UNION, not the sum of two blends. Nothing about this is visible from CPU state: both cases draw the same
/// triangles, and only the pixel that comes back says whether it was paid for once or twice.
/// The second test is the guard that stops the first from being "fixed" too far: two SEPARATE strokes on top of each
/// other are two elements, and their crossing is SUPPOSED to darken.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class StrokeUnionRenderTests
{
    private const int Dim = 240;

    // Alpha 128 over a transparent target: one blend leaves 128, two leave 128 + 128*(1-0.5) = 191. Far enough apart
    // that no rounding or AA argument can blur the two answers together.
    private static readonly Color Translucent = Color.FromRgba(34, 211, 238, 128);

    private const int SingleBlend = 128;
    private const int DoubleBlend = 191;

    // The two arms cross at (120,120): the first segment runs y=x, the third y=240-x. The middle segment is parked at
    // y=200 so it takes no part in the crossing.
    private static StreamGeometry SelfCrossing()
    {
        var g = new StreamGeometry();
        g.Open()
            .BeginFigure(new Vector2(40, 40), false, false)
            .PolylineLineTo([new Vector2(200, 200), new Vector2(40, 200), new Vector2(200, 40)], true);
        return g;
    }

    private static byte[] Render(System.Action<Adamantium.UI.Core.Graphics.IDrawingSession> draw)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var content = new TestControl { RenderAction = draw };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int Alpha(byte[] pixels, int x, int y) => pixels[(y * Dim + x) * 4 + 3];

    // ONE stroke may not blend ANY pixel twice - not at the crossing, and not along the seam where one piece's
    // anti-aliased edge runs over another's body. Checking only the crossing missed exactly that: the order two pieces
    // reach a pixel is fixed along their whole shared edge, so a single-pass scheme paints a hard line down its full
    // length while the crossing itself still measures correct.
    private static void AssertNeverBlendedTwice(byte[] pixels, string what)
    {
        int peak = 0, peakX = 0, peakY = 0, painted = 0;
        for (var y = 0; y < Dim; y++)
        {
            for (var x = 0; x < Dim; x++)
            {
                var a = Alpha(pixels, x, y);
                if (a > 0) painted++;
                if (a > peak) { peak = a; peakX = x; peakY = y; }
            }
        }

        Assert.That(painted, Is.GreaterThan(1000), $"{what}: the stroke must actually have drawn");
        Assert.That(peak, Is.LessThanOrEqualTo(SingleBlend + 3),
            $"{what}: brightest pixel {peak} at {peakX},{peakY} - that is a second blend on one stroke");
    }

    [Test]
    public void SelfCrossingTranslucentStrokeBlendsOnce()
    {
        var pen = new Pen(new SolidColorBrush(Translucent), 12);
        var pixels = Render(s => s.DrawGeometry(null, SelfCrossing(), pen));

        Assert.That(Alpha(pixels, 70, 70), Is.EqualTo(SingleBlend).Within(3), "a plain stretch of the stroke is one blend");
        Assert.That(Alpha(pixels, 120, 120), Is.EqualTo(SingleBlend).Within(3),
            "the crossing must carry the UNION's coverage, not two blends");
        AssertNeverBlendedTwice(pixels, "miter joins");
    }

    // What the sandbox stand actually draws: ROUND joins, which are real disc geometry laid over the ribbon, so a
    // crossing is not the only place two pieces meet on a pixel.
    [Test]
    public void SelfCrossingWithRoundJoinsBlendsOnce()
    {
        var pen = new Pen(new SolidColorBrush(Translucent), 12, penLineJoin: PenLineJoin.Round);
        var pixels = Render(s => s.DrawGeometry(null, SelfCrossing(), pen));

        Assert.That(Alpha(pixels, 70, 70), Is.EqualTo(SingleBlend).Within(3), "a plain stretch of the stroke is one blend");
        Assert.That(Alpha(pixels, 120, 120), Is.EqualTo(SingleBlend).Within(3),
            "the crossing must carry the UNION's coverage, not two blends");
        AssertNeverBlendedTwice(pixels, "round joins");
    }

    // Dashes: every dash boundary is a cap, and the round dash caps overlap the dash bodies.
    [Test]
    public void DashedSelfCrossingBlendsOnce()
    {
        var pen = new Pen(new SolidColorBrush(Translucent), 12, dashStrokeArray: [30.0, 18.0],
            penLineJoin: PenLineJoin.Round);
        var pixels = Render(s => s.DrawGeometry(null, SelfCrossing(), pen));

        AssertNeverBlendedTwice(pixels, "dashes");
    }

    [Test]
    public void TwoSeparateStrokesStillDarkenWhereTheyCross()
    {
        var pen = new Pen(new SolidColorBrush(Translucent), 12);
        var pixels = Render(s =>
        {
            s.DrawGeometry(null, new LineGeometry(new Vector2(40, 40), new Vector2(200, 200)), pen);
            s.DrawGeometry(null, new LineGeometry(new Vector2(40, 200), new Vector2(200, 40)), pen);
        });

        Assert.That(Alpha(pixels, 70, 70), Is.EqualTo(SingleBlend).Within(3), "a plain stretch of one line is one blend");
        Assert.That(Alpha(pixels, 120, 120), Is.EqualTo(DoubleBlend).Within(3),
            "two separate elements must still blend twice where they overlap");
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : Adamantium.UI.Core.Graphics.IResourceFactory
    {
        public Adamantium.Graphics.Core.ITexture CreateTexture(Adamantium.Graphics.Core.TextureDescription description, byte[] pixelData) => throw new System.NotSupportedException();
        public Adamantium.Graphics.Core.ITexture CreateTextureArray(Adamantium.Graphics.Core.TextureDescription description, System.Collections.Generic.IReadOnlyList<byte[]> layers) => throw new System.NotSupportedException();
        public Adamantium.Graphics.Core.ITexture ImportSharedSurface(Adamantium.Graphics.Core.SharedSurfaceDescriptor descriptor) => throw new System.NotSupportedException();
        public Adamantium.Graphics.Core.IRenderTarget CreateRenderTarget(uint width, uint height, Adamantium.Graphics.Core.MSAALevel msaa, Adamantium.Imaging.SurfaceFormat format, Adamantium.Vulkan.Core.ImageLayout desiredLayout) => throw new System.NotSupportedException();
        public Adamantium.Graphics.Fonts.FontRenderer GetFontRenderer(Adamantium.Graphics.Core.IGraphicsDevice graphicsDevice) => throw new System.NotSupportedException();
    }
}
