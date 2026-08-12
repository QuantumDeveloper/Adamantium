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
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// The textured batch, end to end on the GPU: a 4x4 source whose four corners are four different colours is drawn as a
/// nine-slice over a much larger rect. Each corner of the RESULT must be the colour of the matching corner of the
/// source - which is the whole promise of a nine-slice, and something no amount of CPU-side rectangle arithmetic can
/// prove: it needs the shader to sample the right ninth.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class NineSliceRenderTests
{
    private const int Dim = 200;
    private const int Frame = 120;   // the drawn rect, centred-ish in the target

    private const int Src = 16;      // source size
    private const int Cut = 4;       // corner block = Slice 0.25 of it
    private const int Border = 30;   // how big the corners are DRAWN, so a pixel can be read well inside one

    // A 16x16 source whose four 4x4 corner blocks are four distinct colours and whose middle is mid-grey. Big enough
    // that a sample taken inside a drawn corner is unambiguously that corner's colour, and not a linear blend with its
    // neighbour.
    // ONE renderer and ONE source for the whole fixture: the graphics device is shared by the entire suite and its
    // allocator never gives blocks back, so a renderer per test is what tipped the run into ErrorOutOfDeviceMemory.
    private static OffscreenTestRenderer _renderer;
    private static OffscreenTestRenderer _renderer2;   // wider target for the demo-shaped seam test
    private static BitmapSource _source;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)), Dim, Dim)
        {
            ClearColor = Colors.Black
        };
        _source = BuildSource();
        _renderer2 = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)), Dim + 80, Dim)
        {
            ClearColor = Colors.Black
        };
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
        _renderer2?.Dispose();
        _renderer2 = null;
        _source?.Dispose();
        _source = null;
    }

    private static BitmapSource BuildSource()
    {
        var pixels = new byte[Src * Src * 4];
        void Set(int x, int y, byte r, byte g, byte b)
        {
            var i = (y * Src + x) * 4;
            pixels[i + 0] = b;   // BGRA
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255;
        }

        void Block(int x0, int y0, byte r, byte g, byte b)
        {
            for (var y = y0; y < y0 + Cut; y++)
            {
                for (var x = x0; x < x0 + Cut; x++) Set(x, y, r, g, b);
            }
        }

        for (var y = 0; y < Src; y++)
        {
            for (var x = 0; x < Src; x++) Set(x, y, 128, 128, 128);
        }

        Block(0, 0, 255, 0, 0);                     // top-left  RED
        Block(Src - Cut, 0, 0, 255, 0);             // top-right GREEN
        Block(0, Src - Cut, 0, 0, 255);             // bottom-left BLUE
        Block(Src - Cut, Src - Cut, 255, 255, 0);   // bottom-right YELLOW

        return new BitmapSource(Src, Src, 96, 96, SurfaceFormat.B8G8R8A8.UNorm, pixels);
    }

    // The DEMO's own shape: a 64x64 source cut at 0.25 (16px corners), no Border, drawn 240x90 with repeated edges - the
    // exact thing on screen. Built with dpi 1, like a picture loaded from a FILE: BitmapImage.FillData sets only the
    // pixel size and leaves DpiXScale at 1, so a test that passes 96 there is testing a different geometry than the app.
    private const int Skin = 64;
    private const int SkinCut = 16;

    // Its EDGE strips are one flat colour: tiled, a correct edge is a perfectly even band, so a seam is a pixel that
    // differs from its neighbours - nothing to argue about.
    private static BitmapSource FlatEdgeSource()
    {
        var pixels = new byte[Skin * Skin * 4];
        void Set(int x, int y, byte r, byte g, byte b)
        {
            var i = (y * Skin + x) * 4;
            pixels[i + 0] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255;
        }

        for (var y = 0; y < Skin; y++)
        {
            for (var x = 0; x < Skin; x++)
            {
                var corner = (x < SkinCut || x >= Skin - SkinCut) && (y < SkinCut || y >= Skin - SkinCut);
                if (corner) Set(x, y, 255, 0, 0);       // corners RED
                else Set(x, y, 0, 128, 255);            // everything else one flat blue
            }
        }

        return new BitmapSource(Skin, Skin, 1, 1, SurfaceFormat.B8G8R8A8.UNorm, pixels);
    }

    // The seam test: a tiled edge of a FLAT strip must come out flat. Every wrap of frac() is a chance to sample
    // something that is not the strip - the neighbouring texel, or a mip picked from a spiking derivative.
    [Test]
    public void ATiledEdgeOfAFlatStripHasNoSeam()
    {
        const int w = 240;
        const int h = 90;

        using var source = FlatEdgeSource();
        var brush = new NineSliceBrush(source)
        {
            Slice = new Thickness(0.25),
            EdgeMode = NineSliceEdgeMode.Repeat
        };
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, w, h))
        };
        control.Bounds = new Rect(0, 0, w, h);
        control.RenderSize = new Size(w, h);
        // A FRACTIONAL offset, which is what layout hands a frame standing in a WrapPanel with margins - and what an
        // integer-aligned test never sees. Piece boundaries then land between device pixels.
        control.RenderTransform = new Transform { TranslateX = 10.4, TranslateY = 10.4 };

        var root = new VisualRoot(control, Dim + 80, Dim);
        Assert.That(_renderer2 != null ? _renderer2.RenderFrame(root) : false, Is.True);

        using var img = _renderer2.RenderTarget.ResolveTexture.ReadbackToImage();
        var stride = Dim + 80;
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        // A row across the TOP edge, between the two 16px corners, well inside the strip's height.
        var y = 10 + SkinCut / 2;
        var worst = 0;
        var worstX = -1;
        var corner = SkinCut;
        for (var x = 10 + corner + 3; x < 10 + w - corner - 3; x++)
        {
            var i = (y * stride + x) * 4;
            var delta = Math.Abs(pixels[i + 2] - 0) + Math.Abs(pixels[i + 1] - 128) + Math.Abs(pixels[i + 0] - 255);
            if (delta > worst)
            {
                worst = delta;
                worstX = x;
            }
        }

        if (worst >= 12)
        {
            var dump = new System.Text.StringBuilder();
            for (var x = 10 + corner; x < 10 + w - corner; x++)
            {
                var i = (y * stride + x) * 4;
                dump.Append($"{x}:({pixels[i + 2]},{pixels[i + 1]},{pixels[i + 0]}) ");
            }
            TestContext.Out.WriteLine(dump.ToString());
        }

        Assert.That(worst, Is.LessThan(12), $"seam at x={worstX}: the tiled edge deviates from its flat strip by {worst}");

        // ...and the JOINTS. The nine pieces share edges that are not the shape's outline; where two of them meet, the
        // background must not show through. Walk DOWN the middle, crossing the top edge -> centre -> bottom edge cuts,
        // and look for the clear colour: any pixel neither piece claimed is a hairline on screen.
        var midX = 10 + w / 2;
        var darkest = 255;
        var darkestY = -1;
        for (var yy = 11 + 1; yy < 10 + h - 1; yy++)
        {
            var i = (yy * stride + midX) * 4;
            var sum = pixels[i + 2] + pixels[i + 1] + pixels[i + 0];
            if (sum < darkest)
            {
                darkest = sum;
                darkestY = yy;
            }
        }

        Assert.That(darkest, Is.GreaterThan(40),
            $"joint at y={darkestY}: the background shows through where two pieces meet (rgb sum {darkest})");
    }

    [Test]
    public void NineSlice_PutsEachCornerOfTheSourceInTheMatchingCornerOfTheShape()
    {
        // Border states how big the corners are DRAWN: without it they would be the source's own 4px, too small to read
        // a pixel well inside one.
        var brush = new NineSliceBrush(_source)
        {
            Slice = new Thickness(0.25),
            Border = new Thickness(Border)
        };
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, Frame, Frame))
        };
        control.Bounds = new Rect(0, 0, Frame, Frame);
        control.RenderSize = new Size(Frame, Frame);
        control.RenderTransform = new Transform { TranslateX = 20, TranslateY = 20 };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        (int R, int G, int B) At(int x, int y)
        {
            var i = (y * Dim + x) * 4;
            return (pixels[i + 2], pixels[i + 1], pixels[i + 0]);
        }

        // Well inside each drawn corner (30px), away from its edges so no linear blend with the neighbouring strip.
        const int In = 8;
        var topLeft = At(20 + In, 20 + In);
        var topRight = At(20 + Frame - In, 20 + In);
        var bottomLeft = At(20 + In, 20 + Frame - In);
        var bottomRight = At(20 + Frame - In, 20 + Frame - In);

        Assert.Multiple(() =>
        {
            Assert.That(topLeft.R, Is.GreaterThan(topLeft.G + 60), "top-left samples the source's RED corner");
            Assert.That(topRight.G, Is.GreaterThan(topRight.R + 60), "top-right samples GREEN");
            Assert.That(bottomLeft.B, Is.GreaterThan(bottomLeft.R + 60), "bottom-left samples BLUE");
            Assert.That(bottomRight.R, Is.GreaterThan(60).And.GreaterThan(bottomRight.B + 60), "bottom-right samples YELLOW");
            Assert.That(bottomRight.G, Is.GreaterThan(bottomRight.B + 60));
        });
    }

    // It has to keep drawing, on the rebuilding frame AND on the clean ones an idle window spends its life on.
    // HONEST NOTE: this did NOT reproduce the bug it was written for - the collector is built on the first textured fill
    // and was left out of the per-frame reset, so in the real app it drew for exactly ONE frame and vanished. Sabotaging
    // the fix leaves this test green: the offscreen harness drives the cache differently enough that a collector with no
    // frame reset still draws here. Kept as a guard against the coarser regression (later frames go blank), not as
    // cover for that one.
    [Test]
    public void ItStillDrawsOnLaterFrames()
    {
        var brush = new NineSliceBrush(_source)
        {
            Slice = new Thickness(0.25),
            Border = new Thickness(Border)
        };
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, Frame, Frame))
        };
        control.Bounds = new Rect(0, 0, Frame, Frame);
        control.RenderSize = new Size(Frame, Frame);
        control.RenderTransform = new Transform { TranslateX = 20, TranslateY = 20 };

        var root = new VisualRoot(control, Dim, Dim);

        for (var frame = 1; frame <= 4; frame++)
        {
            // Frame 1 walks the tree; the rest are CLEAN frames that replay the recorded stream - the path an idle
            // window spends its life on, and the one a missing per-frame reset silently kills.
            var drawn = frame == 1 ? _renderer.RenderFrame(root) : _renderer.RenderAgain(root);
            Assert.That(drawn, Is.True, $"frame {frame} rendered");

            using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var pixels = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

            var i = ((20 + 8) * Dim + 20 + 8) * 4;
            Assert.That(pixels[i + 2], Is.GreaterThan(pixels[i + 1] + 60), $"frame {frame} still draws its RED corner");
        }
    }

    // The plain textured fill: the same source stretched across the shape. Its corners land in the same places, so this
    // asserts the ONE-instance path draws at all (and that the batch is not, say, drawing nothing).
    [Test]
    public void ImageBrush_DrawsTheSourceAcrossTheShape()
    {
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(new ImageBrush(_source), new Rect(0, 0, Frame, Frame))
        };
        control.Bounds = new Rect(0, 0, Frame, Frame);
        control.RenderSize = new Size(Frame, Frame);
        control.RenderTransform = new Transform { TranslateX = 20, TranslateY = 20 };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        var centre = ((y: 20 + Frame / 2, x: 20 + Frame / 2));
        var i = (centre.y * Dim + centre.x) * 4;
        var grey = pixels[i + 2];

        Assert.That(grey, Is.GreaterThan(60), "the shape's middle carries the source's mid-grey, not the black clear");
        Assert.That(pixels[i + 3], Is.GreaterThan(200), "and it is opaque");
    }

}
