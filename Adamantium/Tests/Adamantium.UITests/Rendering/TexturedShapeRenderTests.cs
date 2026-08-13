using System;
using System.Runtime.InteropServices;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A textured brush is not tied to a rectangle. The textured batch used to claim RECTANGLES only, so an ImageBrush on an
/// ellipse fell through every batch AND was refused by the per-unit path (which paints solid colours only) - it drew
/// NOTHING. These are GPU tests because nothing on the CPU side can tell "sampled the texture into an ellipse" from
/// "sampled it into the bounding rect": only the pixels say which shape was cut.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class TexturedShapeRenderTests
{
    private const int Dim = 160;
    private const int Size = 120;   // the drawn shape
    private const int At = 20;      // its offset in the target

    private static OffscreenTestRenderer _renderer;
    private static BitmapSource _source;
    private static BitmapSource _tall;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)), Dim, Dim)
        {
            ClearColor = Colors.Black
        };
        _source = FlatRed();
        _tall = TwoHalves();
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
        _source?.Dispose();
        _source = null;
        _tall?.Dispose();
        _tall = null;
    }

    // One flat colour: the test is about WHERE the texture lands, not what it holds, and a flat source makes any sampled
    // pixel unambiguous - no blend with a neighbouring texel to argue about.
    private static BitmapSource FlatRed()
    {
        const int src = 16;
        var pixels = new byte[src * src * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = 0;     // B
            pixels[i + 1] = 0;     // G
            pixels[i + 2] = 255;   // R
            pixels[i + 3] = 255;
        }
        return new BitmapSource(src, src, 1, 1, SurfaceFormat.B8G8R8A8.UNorm, pixels);
    }

    // TALL (1:2) and two-coloured: Uniform fits it to HALF the square shape's width, and a wrapped edge then shows up
    // as the wrong half rather than as "some red".
    private static BitmapSource TwoHalves()
    {
        const int width = 16;
        const int height = 32;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 4;
            var left = x < width / 2;
            pixels[i + 0] = (byte)(left ? 255 : 0);   // B
            pixels[i + 1] = (byte)(left ? 0 : 255);   // G
            pixels[i + 3] = 255;
        }
        return new BitmapSource(width, height, 1, 1, SurfaceFormat.B8G8R8A8.UNorm, pixels);
    }

    private static byte[] Draw(Brush brush, bool ellipse)
    {
        var control = new TestControl
        {
            RenderAction = s => _ = ellipse
                ? s.DrawEllipse(new Rect(0, 0, Size, Size), brush, 0, 360, EllipseType.Sector)
                : s.DrawRectangle(brush, new Rect(0, 0, Size, Size))
        };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    private static (byte R, byte G, byte B) At_(byte[] pixels, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return (pixels[i + 2], pixels[i + 1], pixels[i + 0]);
    }

    [Test]
    public void AnImageBrushPaintsAnEllipse()
    {
        var brush = new ImageBrush { Source = _source };

        var pixels = Draw(brush, ellipse: true);

        var middle = At_(pixels, At + Size / 2, At + Size / 2);
        Assert.That(middle.R, Is.GreaterThan(200), "the middle of the ellipse is not painted at all");
        Assert.That(middle.G + middle.B, Is.LessThan(60), $"the middle sampled something other than the source: {middle}");
    }

    // ...and it is an ELLIPSE, not its bounding rectangle: the corner of the box is outside the curve, so it has to stay
    // clear. Without this the test above passes just as well for a rect drawn in the ellipse's place.
    [Test]
    public void AndLeavesTheCornersOfItsBoxClear()
    {
        var brush = new ImageBrush { Source = _source };

        var pixels = Draw(brush, ellipse: true);

        var corner = At_(pixels, At + 6, At + 6);
        Assert.That(corner.R + corner.G + corner.B, Is.LessThan(40),
            $"the box corner is painted, so the fill was cut as a rectangle, not an ellipse: {corner}");
    }

    // ARBITRARY tessellated geometry: no SDF and no usable uv0 on the mesh, so the picture is mapped across the shape's
    // own local box by the per-unit textured-fill pass. Same promise as the other two - the brush is not tied to a shape.
    [Test]
    public void AnImageBrushPaintsATriangle()
    {
        var brush = new ImageBrush { Source = _source };
        var geometry = new StreamGeometry();
        geometry.Open()
            .BeginFigure(new Vector2(Size / 2.0, 0), true, true)
            .PolylineLineTo([new Vector2(Size, Size), new Vector2(0, Size)], true);

        var control = new TestControl
        {
            RenderAction = s => s.DrawGeometry(brush, geometry)
        };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        // Well inside the triangle (low and central), and well outside it (the top-left corner of its box).
        var inside = At_(pixels, At + Size / 2, At + Size - 15);
        var outside = At_(pixels, At + 6, At + 6);

        Assert.That(inside.R, Is.GreaterThan(200), $"the triangle is not painted: {inside}");
        Assert.That(inside.G + inside.B, Is.LessThan(60), $"the triangle sampled something other than the source: {inside}");
        Assert.That(outside.R + outside.G + outside.B, Is.LessThan(40),
            $"the box corner is painted, so the fill was not cut to the triangle: {outside}");
    }

    // A CONCAVE shape: a star's notches are reflex corners, and the tessellation of one is nothing like a fan. The fill
    // has to stop at the notch, not bridge across it - which a convex-only assumption would.
    [Test]
    public void AnImageBrushPaintsAStarAndStopsAtItsNotches()
    {
        const double inner = 0.382;
        var c = Size / 2.0;
        var star = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var radius = i % 2 == 0 ? 1.0 : inner;
            star[i] = new Vector2(c + Math.Cos(angle) * c * radius, c + Math.Sin(angle) * c * radius);
        }

        var geometry = new StreamGeometry();
        geometry.Open().BeginFigure(star[0], true, true).PolylineLineTo(star[1..], true);

        var control = new TestControl { RenderAction = s => s.DrawGeometry(new ImageBrush { Source = _source }, geometry) };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        // Well OUTSIDE along a notch direction: the boundary there sits at 0.382 of the radius, so 0.85 is past it - yet
        // still deep inside the bounding box, so only the star's own concavity can leave it clear.
        var notchAngle = -Math.PI / 2 + Math.PI / 5;
        var notchX = (int)(At + c + Math.Cos(notchAngle) * c * 0.85);
        var notchY = (int)(At + c + Math.Sin(notchAngle) * c * 0.85);

        var middle = At_(pixels, At + (int)c, At + (int)c);
        var notch = At_(pixels, notchX, notchY);

        Assert.That(middle.R, Is.GreaterThan(200), $"the star's body is not painted: {middle}");
        Assert.That(notch.R + notch.G + notch.B, Is.LessThan(40),
            $"the notch at ({notchX},{notchY}) is painted, so the fill bridged a reflex corner: {notch}");
    }

    // --- Stretch.Uniform: the PICTURE is fitted, the SHAPE is not ------------------------------------------------
    // The fitted rect used to be baked as the instance's bounds, which resized the SDF itself - so a circle came out
    // an oval and the picture's own edge, now inside the shape, wrapped a column of the opposite edge onto itself.
    private const int DrawnLeft = 30;    // a 1:2 source fitted into the square shape: x 30..90, shape-local
    private const int DrawnRight = 90;

    [Test]
    public void UniformFitsThePictureAndLeavesTheRestOfTheShapeClear()
    {
        var pixels = Draw(new ImageBrush { Source = _tall, Stretch = Stretch.Uniform }, ellipse: false);

        var left = At_(pixels, At + DrawnLeft + 15, At + Size / 2);
        var right = At_(pixels, At + DrawnRight - 15, At + Size / 2);
        var outside = At_(pixels, At + DrawnLeft / 2, At + Size / 2);

        Assert.That(left.B, Is.GreaterThan(200), $"the picture's left half is not where the fit puts it: {left}");
        Assert.That(right.G, Is.GreaterThan(200), $"the picture's right half is not where the fit puts it: {right}");
        Assert.That(outside.R + outside.G + outside.B, Is.LessThan(40),
            $"the shape is painted outside the fitted picture, so Uniform stretched it after all: {outside}");
    }

    [Test]
    public void TheFittedPicturesFarEdgeDoesNotWrapToItsNearOne()
    {
        var pixels = Draw(new ImageBrush { Source = _tall, Stretch = Stretch.Uniform }, ellipse: false);

        for (var x = DrawnRight - 3; x <= DrawnRight + 3; x++)
        {
            var p = At_(pixels, At + x, At + Size / 2);
            Assert.That(p.B, Is.LessThan(120), $"column {x} shows the picture's LEFT edge, so the far edge wrapped: {p}");
        }
    }

    // Inside the circle, but outside an ellipse squeezed into the fitted rect - so it is painted only while the fit
    // leaves the shape alone.
    [Test]
    public void UniformDoesNotSqueezeTheShapeItself()
    {
        var pixels = Draw(new ImageBrush { Source = _tall, Stretch = Stretch.Uniform }, ellipse: true);

        var offAxis = At_(pixels, At + 85, At + 20);

        Assert.That(offAxis.G, Is.GreaterThan(200),
            $"the ellipse was cut to the fitted rect instead of staying a circle: {offAxis}");
    }

    // The rectangle it always could do - here to prove the shared pass did not lose it when the ellipse branch went in.
    [Test]
    public void TheRectangleStillPaintsItsCorners()
    {
        var brush = new ImageBrush { Source = _source };

        var pixels = Draw(brush, ellipse: false);

        var corner = At_(pixels, At + 6, At + 6);
        Assert.That(corner.R, Is.GreaterThan(200), $"the rect lost its corner: {corner}");
    }
}
