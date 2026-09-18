using System.Runtime.InteropServices;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>A PICTURE'S ROUNDED CORNER. It used to be cut out of the MESH - an arc broken into segments - so the edge
/// came out visibly stepped while a rounded rectangle beside it, cut by the SDF per fragment, was smooth. No amount of
/// tessellation fixes that: the edge stays hard whatever the segment count.
/// <para>A GPU test because nothing on the CPU can tell a stepped edge from a faded one - only the pixels across the
/// corner say which was drawn.</para></summary>
[TestFixture]
[Category("Gpu")]
public class ImageCornerRenderTests
{
    private const int Dim = 160;
    private const int Size = 120;
    private const int At = 20;
    private const int Radius = 40;

    private static OffscreenTestRenderer _renderer;
    private static BitmapSource _source;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)),
            Dim, Dim)
        {
            ClearColor = Colors.Black
        };
        _source = FlatRed();
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
        _source?.Dispose();
        _source = null;
    }

    private static BitmapSource FlatRed()
    {
        const int src = 16;
        var pixels = new byte[src * src * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 2] = 255;   // R
            pixels[i + 3] = 255;
        }
        return new BitmapSource(src, src, 1, 1, SurfaceFormat.B8G8R8A8.UNorm, pixels);
    }

    private static byte[] Drawn(CornerRadius corners)
    {
        var image = new Adamantium.UI.Controls.Image
        {
            Source = _source,
            Stretch = Stretch.Fill,
            CornerRadius = corners,
            Width = Size,
            Height = Size
        };

        image.Bounds = new Rect(0, 0, Size, Size);
        image.RenderSize = new Size(Size, Size);
        image.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(image, Dim, Dim);

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

    // THE CORNER IS CUT AT ALL: the picture's own corner is outside the rounding, so it stays clear.
    [Test]
    public void APicturesCornerIsRounded()
    {
        var pixels = Drawn(new CornerRadius(Radius));

        var corner = At_(pixels, At + 3, At + 3);
        var middle = At_(pixels, At + Size / 2, At + Size / 2);

        Assert.Multiple(() =>
        {
            Assert.That(middle.R, Is.GreaterThan(200), "the picture is not painted at all");
            Assert.That(corner.R + corner.G + corner.B, Is.LessThan(40),
                $"the corner was not cut: {corner} outside a radius of {Radius}");
        });
    }

    // THE GROUND IS A BRUSH, not a colour. It is drawn as an ordinary rounded-rect fill, so whatever the engine can
    // fill a rect with belongs there - a gradient here, and by the same route a picture, a nine-slice, a material.
    // Worth pinning: the control's old ground handling took a SolidColorBrush and nothing else.
    [Test]
    public void ItsGroundCanBeAnyBrush()
    {
        var image = new Adamantium.UI.Controls.Image
        {
            Background = new LinearGradientBrush
            {
                GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) }
            },
            Source = _source,
            ShowsForeground = false,   // the ground alone, so what is measured is the ground
            Width = Size,
            Height = Size
        };

        image.Bounds = new Rect(0, 0, Size, Size);
        image.RenderSize = new Size(Size, Size);
        image.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(image, Dim, Dim);

        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];

        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        var near = At_(pixels, At + 8, At + Size / 2);
        var far = At_(pixels, At + Size - 8, At + Size / 2);

        Assert.Multiple(() =>
        {
            Assert.That(near.R + near.G + near.B, Is.GreaterThan(60), "the ground was not painted at all");
            Assert.That(near.R, Is.Not.EqualTo(far.R),
                $"both ends of the ground are the same colour - a gradient was flattened: {near} / {far}");
        });
    }

    // ...AND THE CUT IS FADED, not stepped. Walked diagonally across the rounding: a mesh's edge jumps from nothing to
    // the picture between two neighbouring pixels, an analytic one spends a pixel or two on the way. At least one
    // in-between value is what separates them, and a stepped edge has none.
    [Test]
    public void AndItsEdgeIsFadedRatherThanStepped()
    {
        var pixels = Drawn(new CornerRadius(Radius));
        var between = 0;

        // OUT FROM THE MIDDLE OF THE ROUNDING at 45 degrees, which is the one direction that certainly crosses the arc
        // (it meets it at Radius/sqrt(2) from the centre). A walk along the corner's chord never crosses it at all -
        // the arc bows away inside - and reads black the whole way, which is a test that proves nothing.
        for (var s = 0; s < Radius; s++)
        {
            var r = At_(pixels, At + Radius - s, At + Radius - s).R;

            if (r is > 20 and < 235) between++;
        }

        Assert.That(between, Is.GreaterThan(0),
            "every pixel across the rounding is either fully the picture or fully nothing - the edge is stepped");
    }
}
