using System.Runtime.InteropServices;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>ZOOMED ALL THE WAY IN AND BACK OUT AGAIN, asked of the pixels. Which items the layers hold is bookkeeping
/// a plain test can check; whether anything reaches the card is a different question, and a frame in which nothing is
/// visible is exactly the state a patched frame has nothing to patch from.</summary>
[TestFixture]
[Category("Gpu")]
public class CanvasZoomBackRenderTests
{
    private const int Dim = 200;

    private static OffscreenTestRenderer _renderer;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)),
            Dim, Dim)
        {
            ClearColor = Colors.Black
        };
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    private static ControlTemplate Template() => new(() =>
    {
        var layers = new Grid();
        var front = new CanvasFrontLayer();
        var root = new Grid();

        root.Children.Add(layers);
        root.Children.Add(front);

        var result = new TemplateResult { RootComponent = root };

        result.RegisterName("PART_Layers", layers);
        result.RegisterName("PART_Front", front);
        return result;
    });

    private static (byte R, byte G, byte B) Middle(InfiniteCanvas canvas, VisualRoot root)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            canvas.Measure(new Size(Dim, Dim));
            canvas.Arrange(new Rect(0, 0, Dim, Dim));
        }

        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];

        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        var i = (Dim / 2 * Dim + Dim / 2) * 4;

        return (pixels[i + 2], pixels[i + 1], pixels[i + 0]);
    }

    // THE SAME ROOT throughout, because that is what a running application has: a frame is patched from the one
    // before it, and a fresh root every time would replay from nothing and hide the very thing being asked about.
    [Test]
    public void TheDrawingComesBackWhenTheCameraDoes()
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Scene = new CanvasScene(),
            Ink = Brushes.Black,
            Width = Dim,
            Height = Dim,
            GridStyle = CanvasGridStyle.Transparent
        };

        canvas.Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(-40, -40, 80, 80),
            Brushes.Transparent, 0, new SolidColorBrush(Colors.Lime)));

        var root = new VisualRoot(canvas, Dim, Dim);

        canvas.Offset = new Vector2(Dim / 2.0, Dim / 2.0);

        var first = Middle(canvas, root);

        Assert.That(first.G, Is.GreaterThan(200), $"the shape was not drawn to begin with: {first}");

        // All the way in, onto a patch of plane with nothing on it.
        canvas.Scale = canvas.MaxScale;
        canvas.CenterOn(new Vector2(600, 600));

        var away = Middle(canvas, root);

        Assert.That(away.G, Is.LessThan(60), $"the deep zoom still showed the shape: {away}");

        canvas.Scale = 1;
        canvas.CenterOn(Vector2.Zero);

        var back = Middle(canvas, root);

        Assert.That(back.G, Is.GreaterThan(200), $"the drawing did not come back with the camera: {back}");
    }
}
