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
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A dash pattern is not always one ON/GAP pair: dash-dot-dot is six runs, and until now anything past the first pair
/// left the batch for the compute expander - a GPU buffer per element for what is a handful of numbers.
/// The instance carries up to six runs (0 and 1 in Stroke0.zw, 2..5 in Dash, the count packed with the cap codes), and
/// the shader walks them.
/// <para>The tests assert the two halves of that: the RULE (which patterns the batch will take, and that an odd or
/// over-long one is honestly refused rather than silently drawn wrong), and the PICTURE (a long-short pattern is not
/// the same ring as a plain one, and the same pattern written as its own repetition draws identically).</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class DashPatternRenderTests
{
    private const int Dim = 64;

    // No contour fit: the fit stretches the pattern to close on itself, which would hide a mis-walked run behind a
    // scale factor. These tests want the runs exactly as written.
    private static Pen Dashed(IEnumerable<double> pattern) => new(Brushes.White, 2, dashStrokeArray: pattern);

    private static byte[] Render(Pen pen)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
        stage.RenderAction = s => s.DrawRectangle(Brushes.Transparent, new Rect(8, 8, Dim - 16, Dim - 16), new CornerRadius(6), pen);

        var root = new VisualRoot(stage, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        RenderDirty.Clear();

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int LitPixels(byte[] px)
    {
        var count = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            if (px[i] > 96 || px[i + 1] > 96 || px[i + 2] > 96) count++;
        }

        return count;
    }

    private static int DifferingPixels(byte[] a, byte[] b)
    {
        var count = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) count++;
        }

        return count;
    }

    [Test]
    public void PatternsOfTwoFourAndSixRuns_AllBatch()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RectBatchCollector.IsPenBatchable(Dashed([4.0, 2.0])), Is.True, "the plain ON/GAP pair");
            Assert.That(RectBatchCollector.IsPenBatchable(Dashed([6.0, 2.0, 1.0, 2.0])), Is.True, "dash-dot");
            Assert.That(RectBatchCollector.IsPenBatchable(Dashed([6.0, 2.0, 1.0, 2.0, 1.0, 2.0])), Is.True, "dash-dot-dot");
        });
    }

    // NEGATIVE: a pattern the record cannot hold is REFUSED, not truncated. Drawing the first six runs of a longer
    // pattern would be a different picture than the one that was asked for, and silently so.
    [Test]
    public void OddOrOverlongPattern_IsRefused_NotTruncated()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RectBatchCollector.IsPenBatchable(Dashed([4.0, 2.0, 1.0])), Is.False,
                "an odd number of runs swaps ON and OFF every lap - the batch must not pretend otherwise");
            Assert.That(RectBatchCollector.IsPenBatchable(Dashed([4.0, 2.0, 1.0, 2.0, 1.0, 2.0, 1.0, 2.0])), Is.False,
                "eight runs do not fit the record; the compute expander takes them");
        });
    }

    // The picture actually differs: dash-dot-dot leaves more gap than a plain dash of the same ON length, so it lights
    // FEWER pixels. Without the extra runs reaching the shader, both would draw the same ring.
    [Test]
    public void ExtraRuns_ChangeThePicture()
    {
        var plain = LitPixels(Render(Dashed([6.0, 2.0])));
        var dashDotDot = LitPixels(Render(Dashed([6.0, 2.0, 1.0, 2.0, 1.0, 2.0])));

        Assert.That(plain, Is.GreaterThan(0), "the plain dashed ring must draw something");
        Assert.That(dashDotDot, Is.LessThan(plain),
            "dash-dot-dot spends more of its period in gaps, so it must paint fewer pixels than the plain dash");
    }

    // A pattern written twice over is the SAME pattern - period 2P instead of P, but the same runs in the same order.
    // If the walk mis-indexed a run or the packed count leaked into the cap codes, these two would not agree.
    [Test]
    public void APatternRepeatedTwice_DrawsTheSameRing()
    {
        var once = Render(Dashed([4.0, 3.0]));
        var twice = Render(Dashed([4.0, 3.0, 4.0, 3.0]));

        Assert.That(DifferingPixels(once, twice), Is.Zero,
            "the same runs in the same order must draw the same ring, however many periods they are written as");
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
